using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace EAgentX.Core
{
	/// <summary>
	/// 从模型回复里提取要执行的命令。
	/// 规则（对应 README 第 4 节）：
	///   1. 优先取第一个带 shell 语言标记的代码块（powershell/ps1/pwsh/cmd/bat/shell…）；
	///   2. 只有一个无标记代码块时也当成命令（兼容模型偷懒不写语言）；
	///   3. 回复里出现 [TASK_DONE] 且没有新命令 → 本轮结束；
	///   4. 命令会剥掉 "$ "、"PS> " 之类的提示符前缀。
	/// </summary>
	public static class CommandParser
	{
		private static readonly Regex Fence = new Regex("```[ \\t]*(?<lang>[A-Za-z0-9_+#\\-]*)[ \\t]*\\r?\\n(?<code>[\\s\\S]*?)```", RegexOptions.Compiled);

		private static readonly HashSet<string> ShellLangs = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"powershell", "powershell_tool", "ps1", "pwsh", "ps", "cmd", "bat", "batch", "shell", "sh", "bash",
			"console"
		};

		/// <summary>代码块围栏是不是没闭合（长回复被截断的典型特征）。</summary>
		public static bool HasUnclosedFence(string reply)
		{
			if (string.IsNullOrEmpty(reply))
			{
				return false;
			}
			int fenceCount = 0;
			int index = 0;
			while (true)
			{
				int found = reply.IndexOf("```", index, StringComparison.Ordinal);
				if (found < 0)
				{
					break;
				}
				fenceCount++;
				index = found + 3;
			}
			return fenceCount % 2 != 0;
		}

		/// <summary>测试版：截图 / 上传 / 鼠标 / 键盘的工具代码块语言。</summary>
		private static ToolKind ToolFromLang(string lang)
		{
			switch (lang)
			{
			case "screenshot":
			case "shot":
			case "screen":
			case "capture":
			case "agent_getjpg":
				return ToolKind.Screenshot;
			case "upload":
			case "file":
			case "attach":
			case "agent_getfile":
				return ToolKind.Upload;
			case "mouse":
			case "click":
			case "agent_mouse_simulate":
				return ToolKind.Mouse;
			case "keyboard":
			case "key":
			case "keys":
			case "agent_keyboard_simulate":
				return ToolKind.Keyboard;
			default:
				return ToolKind.None;
			}
		}

		public static ParsedCommand Parse(string reply)
		{
			ParsedCommand parsed = new ParsedCommand();
			parsed.Reply = ((reply == null) ? "" : reply.Trim());
			parsed.Done = parsed.Reply.IndexOf("[TASK_DONE]", StringComparison.OrdinalIgnoreCase) >= 0;
			if (string.IsNullOrEmpty(parsed.Reply))
			{
				return parsed;
			}
			string shellCode = null;
			string untaggedCode = null;
			string toolPayload = null;
			ToolKind tool = ToolKind.None;
			int blockCount = 0;
			foreach (Match block in Fence.Matches(parsed.Reply))
			{
				string lang = NormalizeLang(block.Groups["lang"].Value);
				string code = Clean(block.Groups["code"].Value);
				if (string.Equals(lang, "powershell_tool", StringComparison.OrdinalIgnoreCase))
				{
					code = StripPowerShellToolMarkers(code);
				}
				if (string.IsNullOrEmpty(code))
				{
					continue;
				}
				blockCount++;
				ToolKind candidate = ToolFromLang(lang);
				if (candidate != ToolKind.None)
				{
					// 一个回复只用一个工具；第一个工具块生效
					if (tool == ToolKind.None)
					{
						tool = candidate;
						toolPayload = code;
					}
				}
				else if (ShellLangs.Contains(lang))
				{
					if (shellCode == null)
					{
						shellCode = code;
					}
				}
				else if (lang.Length == 0 && untaggedCode == null)
				{
					untaggedCode = code;
				}
			}
			if (shellCode != null)
			{
				parsed.Command = shellCode;
				parsed.Tool = ToolKind.Command;
			}
			else if (tool != ToolKind.None)
			{
				parsed.Tool = tool;
				parsed.ToolPayload = toolPayload;
			}
			else if (blockCount == 1 && untaggedCode != null)
			{
				// 只有一个没有语言标记的代码块：当命令用
				parsed.Command = untaggedCode;
				parsed.Tool = ToolKind.Command;
			}
			return parsed;
		}

		/// <summary>
		/// EAgent 的 powershell_tool 代码块头尾各有一行识别注释，网页版还可能把
		/// “powershell_tool复制下载” 工具栏文字一起抓进来。执行前只去掉这些标记，
		/// 用户自己的注释和代码原样保留。
		/// </summary>
		private static string StripPowerShellToolMarkers(string code)
		{
			if (string.IsNullOrEmpty(code))
			{
				return null;
			}
			string[] lines = code.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				string trimmed = line.Trim();
				if (trimmed.IndexOf("powershell_tool_调用工具_识别", StringComparison.Ordinal) >= 0)
				{
					continue;
				}
				if (trimmed.Equals("powershell_tool复制下载", StringComparison.OrdinalIgnoreCase) ||
					trimmed.Equals("powershell_tool复制", StringComparison.OrdinalIgnoreCase) ||
					trimmed.Equals("复制下载", StringComparison.OrdinalIgnoreCase))
				{
					continue;
				}
				sb.AppendLine(line);
			}
			string result = sb.ToString().Trim();
			return (result.Length == 0) ? null : result;
		}

		/// <summary>把 "PowerShell"、"PS1" 之类统一成小写语言名。</summary>
		private static string NormalizeLang(string raw)
		{
			if (string.IsNullOrEmpty(raw))
			{
				return "";
			}
			Match token = Regex.Match(raw, "[A-Za-z0-9_+#\\-]+");
			if (!token.Success)
			{
				return "";
			}
			return token.Value.ToLowerInvariant();
		}

		/// <summary>去掉首尾空白，并反复剥掉 "$ "、"PS> "、"PS C:\> " 之类提示符。</summary>
		private static string Clean(string code)
		{
			if (code == null)
			{
				return null;
			}
			string text = code.Trim();
			if (text.Length == 0)
			{
				return null;
			}
			string[] prompts = new string[4] { "$ ", "PS> ", "PS C:\\> ", "> " };
			string[] shells = new string[2] { "powershell ", "pwsh " };
			bool stripped = true;
			while (stripped && text.Length > 0)
			{
				stripped = false;
				foreach (string prompt in prompts)
				{
					if (text.StartsWith(prompt, StringComparison.OrdinalIgnoreCase))
					{
						text = text.Substring(prompt.Length).TrimStart();
						stripped = true;
					}
				}
				foreach (string shell in shells)
				{
					if (!text.StartsWith(shell, StringComparison.OrdinalIgnoreCase))
					{
						continue;
					}
					string rest = text.Substring(shell.Length).TrimStart();
					// "powershell -Command ..." 是一条真实命令，不能当成提示符剥掉
					if (rest.StartsWith("-", StringComparison.Ordinal))
					{
						continue;
					}
					text = rest;
					stripped = true;
				}
			}
			if (text.Length == 0)
			{
				return null;
			}
			return text;
		}
	}
}
