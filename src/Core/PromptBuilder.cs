using System;
using System.IO;
using System.Reflection;
using System.Text;
using EAgentX.Util;

namespace EAgentX.Core
{
	/// <summary>
	/// 提示词拼装。测试版内置系统提示词直接采用开源 EAgent 的原文
	/// （assets\eagent_prompt.txt，编译时嵌入 exe），只做三处运行期替换：
	/// 工作目录、临时目录、技能目录。工具协议也按 EAgent 的
	/// powershell_tool / agent_getjpg / agent_getfile / agent_mouse_simulate /
	/// agent_keyboard_simulate 标记解析。
	/// 设置里填了自定义提示词时，内置内容整体被替换（BuiltIn 仍返回原文，供设置窗口预填）。
	/// </summary>
	public static class PromptBuilder
	{
		private const string ResourceName = "eagent_prompt.txt";

		private static readonly object CacheGate = new object();

		private static string CachedTemplate;

		// 这四行是「本机信息」，自定义提示词里出现时按当前机器刷新
		private const string PrefixOs = "- 操作系统: ";
		private const string PrefixTime = "- 当前时间: ";
		private const string PrefixDir = "- 工作目录: ";
		private const string PrefixDirCn = "- 工作目录：";
		private const string PrefixTemp = "- 临时目录: ";
		private const string PrefixTempCn = "- 临时目录：";
		private const string PrefixUser = "- 用户身份: ";

		public static string System(Settings s)
		{
			if (s != null && !string.IsNullOrEmpty(s.SystemPrompt))
			{
				return RefreshRuntimeLines(s.SystemPrompt, s);
			}
			return BuiltIn(s);
		}

		/// <summary>内置系统提示词原文（EAgent 提示词 + 当前机器的目录替换）。</summary>
		public static string BuiltIn(Settings s)
		{
			string template = LoadEAgentPrompt();
			string workDir = WorkDir(s);
			string tempDir = Path.Combine(workDir, ".eagentx");
			string skillsDir = Path.Combine(tempDir, "skills");
			string prompt = template
				.Replace("{{SKILLS_DIR}}", skillsDir)
				.Replace("{{TEMP_WORKDIR}}", tempDir)
				.Replace("{{WORKDIR}}", workDir);
			if (s != null && !s.EnableTools)
			{
				prompt = WithoutTools(prompt);
			}
			return prompt;
		}

		/// <summary>关闭 4 个扩展工具时，把 EAgent 提示词里的工具 2–5 段落去掉，避免模型调用被宿主拒绝。</summary>
		private static string WithoutTools(string prompt)
		{
			int start = prompt.IndexOf("** 工具2：截图工具 **", StringComparison.Ordinal);
			int end = prompt.IndexOf("## 硬性规则", StringComparison.Ordinal);
			if (start < 0 || end <= start)
			{
				return prompt;
			}
			return prompt.Substring(0, start) +
				"（本客户端已在设置里关闭截图 / 上传 / 鼠标 / 键盘工具，只能使用 PowerShell。）\r\n\r\n" +
				prompt.Substring(end);
		}

		private static string LoadEAgentPrompt()
		{
			string cached = CachedTemplate;
			if (cached != null)
			{
				return cached;
			}
			lock (CacheGate)
			{
				if (CachedTemplate != null)
				{
					return CachedTemplate;
				}
				try
				{
					using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName))
					{
						if (stream != null)
						{
							using (StreamReader reader = new StreamReader(stream, Encoding.UTF8, true))
							{
								CachedTemplate = reader.ReadToEnd();
							}
						}
					}
				}
				catch
				{
					CachedTemplate = null;
				}
				if (string.IsNullOrEmpty(CachedTemplate))
				{
					CachedTemplate = Fallback;
				}
				return CachedTemplate;
			}
		}

		/// <summary>资源缺失时的兜底：保留 EAgent 的 5 个工具标记，不让主流程崩掉。</summary>
		private const string Fallback =
			"- 从现在开始，每轮对话，必须完全遵循以下规则。\r\n" +
			"- 你是世界顶尖的编程领域专家，通过调用工具完成 Windows 上的任务。\r\n\r\n" +
			"## 工作目录\r\n" +
			"- 工作目录：{{WORKDIR}}\r\n" +
			"- 临时目录：{{TEMP_WORKDIR}}\r\n" +
			"- skill：{{SKILLS_DIR}}\r\n\r\n" +
			"** 工具1：执行PowerShell命令 **\r\n" +
			"- 每条回复只给一个代码块，语言为 powershell_tool；代码在全新的 PowerShell 进程中执行。\r\n" +
			"- 单条代码不超过 4000 字符；读取大文件优先用上传工具。\r\n\r\n" +
			"** 工具2：截图工具 **\r\n" +
			"```agent_getjpg\r\nagent_getjpg_1_star(屏幕)_1_getjpg_end\r\n```\r\n\r\n" +
			"** 工具3：上传文件 **\r\n" +
			"```agent_getfile\r\nagent_getfile_head('D:\\1.jpg')agent_getfile_end\r\n```\r\n\r\n" +
			"** 工具4：模拟控制鼠标 **\r\n" +
			"```agent_mouse_simulate\r\nagent_mouse_simulate_1_start(200)_agent_mouse_simulate_1_end\r\nagent_mouse_simulate_2_start(300)_agent_mouse_simulate_2_end\r\nagent_mouse_simulate_3_start(0)_agent_mouse_simulate_3_end\r\n```\r\n\r\n" +
			"** 工具5：模拟控制键盘 **\r\n" +
			"```agent_keyboard_simulate\r\nagent_keyboard_simulate_1_start(91)_agent_keyboard_simulate_1_end\r\nagent_keyboard_simulate_2_start(82)_agent_keyboard_simulate_2_end\r\n```\r\n\r\n" +
			"完成任务后输出 [TASK_DONE] 并简短总结。\r\n";

		/// <summary>
		/// 把提示词里的本机信息替换为当前值。内置提示词走 BuildIn 的占位符替换；
		/// 自定义提示词（包括用户从内置提示词复制出来的 EAgent 原文）走这里。
		/// </summary>
		internal static string RefreshRuntimeLines(string prompt, Settings s)
		{
			if (string.IsNullOrEmpty(prompt))
			{
				return prompt;
			}
			string osLine = BuildOsLine();
			string timeLine = BuildTimeLine();
			string userLine = BuildUserLine();
			string workDir = WorkDir(s);
			string tempDir = Path.Combine(workDir, ".eagentx");
			string skillsDir = Path.Combine(tempDir, "skills");
			string[] lines = prompt.Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				if (line.StartsWith(PrefixOs, StringComparison.Ordinal))
				{
					lines[i] = osLine;
				}
				else if (line.StartsWith(PrefixTime, StringComparison.Ordinal))
				{
					lines[i] = timeLine;
				}
				else if (line.StartsWith(PrefixDirCn, StringComparison.Ordinal))
				{
					lines[i] = PrefixDirCn + workDir + " 用户想在该目录下工作，但你不局限于工作目录，有权读取电脑全盘范围。";
				}
				else if (line.StartsWith(PrefixDir, StringComparison.Ordinal))
				{
					lines[i] = PrefixDir + workDir;
				}
				else if (line.StartsWith(PrefixTempCn, StringComparison.Ordinal))
				{
					lines[i] = PrefixTempCn + tempDir + " 这是Agent缓存目录，用户一般用不到，这里面的所有东西会随时删除。";
				}
				else if (line.StartsWith(PrefixTemp, StringComparison.Ordinal))
				{
					lines[i] = PrefixTemp + tempDir;
				}
				else if (line.StartsWith(PrefixUser, StringComparison.Ordinal))
				{
					lines[i] = userLine;
				}
				else if (line.StartsWith("- 技能库: ", StringComparison.Ordinal))
				{
					lines[i] = "- 技能库: " + skillsDir + "（每个技能目录里的 SKILL.md 说明用途；能用就用，先读 SKILL.md）";
				}
				else if (line.StartsWith("- skill：", StringComparison.Ordinal))
				{
					lines[i] = "- skill：" + skillsDir + "（已解压，直接读各技能目录里的 SKILL.md）";
				}
			}
			return string.Join("\r\n", lines);
		}

		private static string WorkDir(Settings s)
		{
			return (s == null || string.IsNullOrEmpty(s.WorkDir)) ? Environment.CurrentDirectory : s.WorkDir;
		}

		private static string BuildOsLine()
		{
			return PrefixOs + Environment.OSVersion.VersionString + " (" + (Environment.Is64BitOperatingSystem ? "64 位" : "32 位") + ")";
		}

		private static string BuildTimeLine()
		{
			return PrefixTime + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
		}

		private static string BuildUserLine()
		{
			return PrefixUser + Environment.UserName;
		}

		/// <summary>一轮任务的第一条消息：系统设定 + 用户任务。</summary>
		public static string FirstTask(Settings s, string task)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("==================== 系统设定 ====================");
			sb.AppendLine(System(s));
			sb.AppendLine("==================== 用户任务 ====================");
			sb.AppendLine(task);
			sb.AppendLine();
			sb.AppendLine("请严格按上面的系统提示词工作：先说明你的计划（不超过 3 行），然后立刻给出第一条命令或工具调用，不要等确认。任务完成或无法继续时输出 [TASK_DONE] 并简短总结。");
			return sb.ToString();
		}

		/// <summary>命令执行结果回喂格式。</summary>
		public static string Feedback(RunResult r)
		{
			StringBuilder sb = new StringBuilder();
			if (r.TimedOut)
			{
				sb.AppendLine("【执行结果】超时（超过 " + r.ElapsedMs / 1000 + " 秒，进程已被终止）");
			}
			else
			{
				sb.AppendLine("【执行结果】退出码: " + r.ExitCode);
			}
			sb.AppendLine("【耗时】" + r.ElapsedMs + " ms");
			sb.AppendLine("【输出】");
			sb.AppendLine("```text");
			sb.AppendLine((r.Output == null) ? "" : r.Output);
			sb.AppendLine("```");
			sb.AppendLine("请根据以上真实输出决定下一步：需要继续就再给一条命令；已经完成或无法继续就输出 [TASK_DONE] 并总结。");
			sb.AppendLine("命令必须是完整的：引号/括号/here-string 要闭合，不要以 -、|、=、( 等符号结尾。");
			return sb.ToString();
		}

		/// <summary>测试版：截图 / 上传 / 鼠标 / 键盘工具的结果回喂格式。</summary>
		public static string ToolFeedback(string text, ToolKind kind, bool ok)
		{
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("【工具结果】" + (ok ? "成功" : "失败") + "：" + KindName(kind));
			sb.AppendLine("```text");
			sb.AppendLine((text == null) ? "" : text);
			sb.AppendLine("```");
			if (kind == ToolKind.Screenshot || kind == ToolKind.Upload)
			{
				sb.AppendLine("（截图/文件已作为本条消息的附件一起发给你，请直接查看。）");
			}
			sb.AppendLine("请根据以上真实结果决定下一步：继续就再给一个工具或命令，完成或无法继续就输出 [TASK_DONE] 并总结。一次只用一个代码块。");
			return sb.ToString();
		}

		public static string KindName(ToolKind kind)
		{
			switch (kind)
			{
			case ToolKind.Screenshot:
				return "屏幕截图";
			case ToolKind.Upload:
				return "上传文件";
			case ToolKind.Mouse:
				return "鼠标";
			case ToolKind.Keyboard:
				return "键盘";
			case ToolKind.Command:
				return "PowerShell";
			default:
				return "无";
			}
		}

		/// <summary>模型只回了几句话、既没有命令也没有结束时，催它继续。</summary>
		public static string Continue()
		{
			return "【继续】你上一条回复没有给出可执行的命令，也没有输出 [TASK_DONE]。请立刻动手：\r\n" +
				"- 还需要继续操作：直接给出下一条 ```powershell_tool 命令或其它工具代码块；一条命令里可以写多条语句，把这一步能做完的都做完。\r\n" +
				"- 任务已经完成：输出 [TASK_DONE] 并简短总结。\r\n" +
				"- 确实缺少只能由用户提供的信息：用一句话提问，并以 [NEED_INPUT] 结尾。\r\n" +
				"不要只复述计划，不要等待确认。";
		}

		/// <summary>命令被判定为残片时，要求模型重写。</summary>
		public static string Incomplete(string command)
		{
			string tail = ((command == null) ? "" : command.Trim());
			if (tail.Length > 1200)
			{
				tail = tail.Substring(tail.Length - 1200);
			}
			return "【命令不完整】上一条命令可能被截断，客户端没有执行它：\r\n```text\r\n" + tail + "\r\n```\r\n请重新输出一条完整、可直接执行的 PowerShell 命令，不要接着写残片。\r\n要求：引号/括号/here-string 必须闭合；不要以 -、|、,、=、+、&、( 结尾；\r\n如果内容太长，请分成 2–4 段，每段 2000–4000 字符，并在命令前标注“第 n/m 段”。";
		}

		public static string Denied(string command)
		{
			return "【用户拒绝执行】用户没有允许执行下面这条命令：\r\n```text\r\n" + command + "\r\n```\r\n请不要重复同一条命令，换一种更安全的方式，或者直接向用户说明原因并输出 [TASK_DONE]。";
		}

		public static string MaxStepsReached(int steps)
		{
			return "已达到本次任务的最大步数（" + steps + " 步），自动停止。你可以继续发消息让智能体接着做。";
		}
	}
}
