using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EAgentX.Core;
using EAgentX.Engines;
using EAgentX.Net;
using EAgentX.Ui;
using EAgentX.Util;

namespace EAgentX
{
	/// <summary>
	/// 自检入口。共七个模式：
	///   --selftest              离线自检（不联网、不启动浏览器）
	///   --shotcheck             实测「目标窗口被完全覆盖」时的截图
	///   --webcheck              启动浏览器并探测页面
	///   --chatcheck             网页版发一条消息看回复
	///   --agentcheck            端到端跑一个「创建文件」的小任务
	///   --longcheck             端到端跑一个「生成 3500 字符文件」的任务
	///   --uploadcheck [file]    网页附件上传
	/// 所有结果都会写进 exe 同目录的 *.log，并用退出码表示成功（0）/失败（1）。
	/// </summary>
	public static class SelfTest
	{
		private sealed class Runner
		{
			public int Pass;

			public int Fail;

			public readonly StringBuilder Report = new StringBuilder();

			public void Check(string name, Action test)
			{
				try
				{
					test();
					Pass++;
					Write("PASS", name);
				}
				catch (Exception ex)
				{
					Fail++;
					Write("FAIL", name + "  ->  " + ex.Message);
				}
			}

			public void Info(string name, string detail)
			{
				Write("INFO", name + ": " + detail);
			}

			public void Write(string tag, string message)
			{
				string line = "[" + tag + "] " + message;
				Report.AppendLine(line);
				try
				{
					Console.WriteLine(line);
				}
				catch
				{
				}
			}
		}

		/// <summary>离线测试用的假引擎：第一次回一条命令，第二次回 [TASK_DONE]。</summary>
		private class DelayTestEngine : IChatEngine, IDisposable
		{
			public readonly List<DateTime> Sends = new List<DateTime>();

			private int Count;

			public string Name
			{
				get
				{
					return "发送间隔测试引擎";
				}
			}

			public string Diagnostics
			{
				get
				{
					return "offline";
				}
			}

			public Task<string> StartAsync(CancellationToken ct)
			{
				return Task.FromResult("offline-ready");
			}

			public Task<string> SendAsync(string text, string[] attachments, CancellationToken ct)
			{
				Sends.Add(DateTime.Now);
				Count++;
				// 模拟网页版“回答本身要点时间”：发送间隔必须从回答结束开始算，
				// 而不是从发送开始算，否则这段等待会被回答耗时吞掉。
				Thread.Sleep(600);
				if (Count == 1)
				{
					return Task.FromResult("准备执行。\r\n```powershell\r\nWrite-Output eagentx-delay-ok\r\n```");
				}
				return Task.FromResult("[TASK_DONE] 完成");
			}

			public void Dispose()
			{
			}
		}

		/// <summary>只回文字、不给命令的假引擎：应该被自动催一次，然后正常收尾。</summary>
		private class ChattyTestEngine : IChatEngine, IDisposable
		{
			public int Sends;

			private int Count;

			public string Name
			{
				get
				{
					return "催继续测试引擎";
				}
			}

			public string Diagnostics
			{
				get
				{
					return "offline";
				}
			}

			public Task<string> StartAsync(CancellationToken ct)
			{
				return Task.FromResult("offline-ready");
			}

			public Task<string> SendAsync(string text, string[] attachments, CancellationToken ct)
			{
				Sends++;
				Count++;
				if (Count == 1)
				{
					return Task.FromResult("好的，我准备开始处理这个任务。");
				}
				if (Count == 2)
				{
					return Task.FromResult("```powershell\r\nWrite-Output eagentx-nudge-ok\r\n```");
				}
				return Task.FromResult("[TASK_DONE] 完成");
			}

			public void Dispose()
			{
			}
		}

		/// <summary>在等用户补充信息：不应该被自动催。</summary>
		private class NeedInputTestEngine : IChatEngine, IDisposable
		{
			public int Sends;

			public string Name
			{
				get
				{
					return "等待输入测试引擎";
				}
			}

			public string Diagnostics
			{
				get
				{
					return "offline";
				}
			}

			public Task<string> StartAsync(CancellationToken ct)
			{
				return Task.FromResult("offline-ready");
			}

			public Task<string> SendAsync(string text, string[] attachments, CancellationToken ct)
			{
				Sends++;
				return Task.FromResult("[NEED_INPUT] 请问要把文件放在哪个目录？");
			}

			public void Dispose()
			{
			}
		}

		/// <summary>测试版：第一轮调用截图工具，第二轮结束；记录收到的附件。</summary>
		private class ToolTestEngine : IChatEngine, IDisposable
		{
			public int Sends;

			public int Attachments;

			public string LastText = "";

			private int Count;

			public string Name
			{
				get
				{
					return "工具协议测试引擎";
				}
			}

			public string Diagnostics
			{
				get
				{
					return "offline";
				}
			}

			public Task<string> StartAsync(CancellationToken ct)
			{
				return Task.FromResult("offline-ready");
			}

			public Task<string> SendAsync(string text, string[] attachments, CancellationToken ct)
			{
				Sends++;
				Count++;
				if (attachments != null)
				{
					Attachments += attachments.Length;
				}
				LastText = text;
				if (Count == 1)
				{
					return Task.FromResult("我先截个屏看看。\r\n```agent_getjpg\r\n# tool_取屏幕截图_调用工具_识别头_本行注释一定要完全保留\r\nagent_getjpg_1_star(屏幕)_1_getjpg_end\r\n# tool_取屏幕截图_调用工具_识别尾_本行注释一定要完全保留\r\n```");
				}
				return Task.FromResult("[TASK_DONE] 完成");
			}

			public void Dispose()
			{
			}
		}

		private const int AttachParentProcess = -1;

		[DllImport("kernel32.dll")]
		private static extern bool AttachConsole(int processId);

		/// <summary>WinForms 程序没有控制台，自检时附加到父进程（cmd/PowerShell）才能看到输出。</summary>
		public static void AttachConsole()
		{
			try
			{
				AttachConsole(AttachParentProcess);
				StreamWriter stdout = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
				stdout.AutoFlush = true;
				Console.SetOut(stdout);
			}
			catch
			{
			}
		}

		private static void Assert(bool condition, string message)
		{
			if (!condition)
			{
				throw new Exception(message);
			}
		}

		/// <summary>取 AggregateException 里的真实异常，日志里只显示有用的一层。</summary>
		private static Exception Unwrap(Exception ex)
		{
			AggregateException aggregate = ex as AggregateException;
			if (aggregate != null && aggregate.InnerException != null)
			{
				return aggregate.InnerException;
			}
			return ex;
		}

		/// <summary>把报告写到 exe 同目录，并尽量回显到控制台。</summary>
		private static void WriteReport(string fileName, string content)
		{
			try
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName), content, Encoding.UTF8);
			}
			catch
			{
			}
			try
			{
				Console.WriteLine(content);
			}
			catch
			{
			}
		}

		public static int Run()
		{
			Runner r = new Runner();
			r.Write("HEAD", "EAgentX 自检开始 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
			r.Info("运行环境", Environment.OSVersion.VersionString + " / .NET " + Environment.Version);
			r.Info("程序目录", AppDomain.CurrentDomain.BaseDirectory);

			r.Check("JSON 编解码", delegate
			{
				string json = Json.Encode(new Dictionary<string, object>
				{
					{ "a", 1 },
					{ "b", "中文" },
					{ "c", true }
				});
				Dictionary<string, object> map = Json.Obj(Json.Decode(json));
				Assert(map != null, "解析结果为空");
				Assert(Json.GetInt(map, "a", 0) == 1, "整数丢失");
				Assert(Json.GetStr(map, "b") == "中文", "中文丢失");
				Assert(Json.GetBool(map, "c", false), "布尔丢失");
				List<object> items = Json.List(Json.Decode("[{\"x\":1},{\"x\":2}]"));
				Assert(items != null && items.Count == 2, "数组解析失败（CDP 目标列表会因此为空）");
				Assert(Json.GetInt(Json.Obj(items[1]), "x", 0) == 2, "数组元素解析失败");
			});

			r.Check("命令解析 · shell 代码块", delegate
			{
				ParsedCommand parsed = CommandParser.Parse("先看一下目录：\r\n```powershell\r\nGet-ChildItem -Force\r\n```");
				Assert(parsed.Command == "Get-ChildItem -Force", "命令提取失败: " + parsed.Command);
				Assert(!parsed.Done, "不应判定完成");
			});

			r.Check("命令解析 · 完成标记", delegate
			{
				ParsedCommand parsed = CommandParser.Parse("已经处理完了。\r\n[TASK_DONE]");
				Assert(parsed.Done, "未识别 [TASK_DONE]");
				Assert(parsed.Command == null, "完成时不应有命令");
			});

			r.Check("命令解析 · 无语言标记单块", delegate
			{
				ParsedCommand parsed = CommandParser.Parse("```\r\nGet-Date\r\n```");
				Assert(parsed.Command == "Get-Date", "单块兼容失败");
			});

			r.Check("命令解析 · 提示符前缀", delegate
			{
				ParsedCommand drive = CommandParser.Parse("```powershell\r\nPS C:\\> Get-Date\r\n```");
				Assert(drive.Command == "Get-Date", "PS 提示符未被剥掉: " + drive.Command);
				ParsedCommand prompt = CommandParser.Parse("```powershell\r\npowershell Get-Date\r\n```");
				Assert(prompt.Command == "Get-Date", "powershell 提示符未被剥掉: " + prompt.Command);
				// "powershell -Command ..." 是真实命令，不能当作提示符处理
				ParsedCommand real = CommandParser.Parse("```powershell\r\npowershell -NoProfile -Command Get-Date\r\n```");
				Assert(real.Command == "powershell -NoProfile -Command Get-Date", "真实 powershell 命令被误剥: " + real.Command);
			});

			r.Check("命令完整性检查", delegate
			{
				Assert(CommandShape.IsComplete("Get-ChildItem -Force"), "正常命令被误判");
				Assert(CommandShape.IsComplete("if($a){Write-Output 1}"), "if 块被误判");
				Assert(CommandShape.IsComplete("$a = @'\r\nabc\r\n'@"), "完整 here-string 被误判");
				Assert(CommandShape.IsComplete("Add-Content .\\x.txt 'a''b' -Encoding UTF8"), "转义引号被误判");
				Assert(CommandShape.IsComplete("(Get-Item .\\x.txt).Length"), "括号表达式被误判");
				Assert(!CommandShape.IsComplete("Get-ChildItem -"), "结尾 - 未识别");
				Assert(!CommandShape.IsComplete("New-Item -ItemType Directory -"), "New-Item 残片未识别");
				Assert(!CommandShape.IsComplete("("), "孤立左括号未识别");
				Assert(!CommandShape.IsComplete("$a = @'\r\nabc"), "未闭合 here-string 未识别");
				Assert(!CommandShape.IsComplete("Add-Content .\\x.txt 'abc"), "未闭合单引号未识别");
				Assert(!CommandShape.IsComplete("Get-ChildItem |"), "结尾管道未识别");
			});

			r.Check("PowerShell 错误解码", delegate
			{
				PowerShellRunner runner = new PowerShellRunner(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 30);
				RunResult syntaxError = runner.Run("if (");
				Assert(syntaxError.ExitCode != 0, "语法错误命令不应返回 0");
				Assert(syntaxError.Output.IndexOf("ParserError", StringComparison.OrdinalIgnoreCase) >= 0 || syntaxError.Output.IndexOf("缺少", StringComparison.Ordinal) >= 0, "没有拿到语法错误信息: " + syntaxError.Output);
				Assert(syntaxError.Output.IndexOf('\ufffd') < 0, "stderr 出现替换字符（乱码）: " + syntaxError.Output);
				RunResult chineseStderr = runner.Run("[Console]::Error.WriteLine('中文错误输出')");
				Assert(chineseStderr.Output.IndexOf("中文错误输出", StringComparison.Ordinal) >= 0, "中文 stderr 解码失败: " + chineseStderr.Output);
			});

			r.Check("配置序列化", delegate
			{
				Settings settings = Json.Decode<Settings>(Json.Encode(new Settings
				{
					WorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
					CdpPort = 9333,
					Engine = "api",
					SendIntervalSec = 7,
					Sel =
					{
						Input = "textarea"
					}
				}));
				Assert(settings != null, "反序列化为空");
				Assert(settings.CdpPort == 9333, "端口丢失");
				Assert(settings.Engine == "api", "引擎丢失");
				Assert(settings.SendIntervalSec == 7, "发送间隔丢失");
				Assert(settings.Sel != null && settings.Sel.Input == "textarea", "选择器丢失");
			});

			r.Check("限流参数默认值与收敛", delegate
			{
				Settings defaults = new Settings();
				Assert(defaults.RateLimitRetries == 2, "默认重试次数应为 2");
				Assert(defaults.RateLimitWaitSec == 60, "默认等待应为 60 秒");
				// 旧版 config.json 没有这两个字段：反序列化后必须拿到构造函数里的默认值
				Settings legacy = Json.Decode<Settings>("{\"Engine\":\"web\",\"SendIntervalSec\":3}");
				Assert(legacy != null, "旧配置解析失败");
				Assert(legacy.RateLimitRetries == 2 && legacy.RateLimitWaitSec == 60, "旧配置缺少限流字段时没有回落到默认值");
				Settings clamped = new Settings();
				clamped.RateLimitRetries = 99;
				clamped.RateLimitWaitSec = 1;
				clamped.Normalize();
				Assert(clamped.RateLimitRetries == 10, "重试次数未收敛到 10");
				Assert(clamped.RateLimitWaitSec == 10, "等待秒数未收敛到 10");
			});

			r.Check("提示词构建", delegate
			{
				Settings settings = new Settings();
				string prompt = PromptBuilder.System(settings);
				Assert(prompt.Contains("powershell_tool"), "没有采用 EAgent 的 powershell_tool 协议");
				Assert(prompt.Contains("agent_getjpg"), "缺少 EAgent 截图工具说明");
				Assert(prompt.Contains("agent_getfile"), "缺少 EAgent 上传工具说明");
				Assert(prompt.Contains("agent_mouse_simulate"), "缺少 EAgent 鼠标工具说明");
				Assert(prompt.Contains("agent_keyboard_simulate"), "缺少 EAgent 键盘工具说明");
				Assert(prompt.Contains("硬性规则"), "缺少 EAgent 的硬性规则段");
				Assert(prompt.Contains("全新的 PowerShell 进程中执行"), "缺少“每条命令不保留状态”的说明");
				Assert(!prompt.Contains("{{WORKDIR}}") && !prompt.Contains("{{TEMP_WORKDIR}}") && !prompt.Contains("{{SKILLS_DIR}}"), "占位符没有被替换");
				Assert(prompt.Contains(settings.WorkDir), "工作目录没有注入");
				Assert(prompt.Contains(Path.Combine(settings.WorkDir, ".eagentx", "skills")), "技能目录没有注入");
				string first = PromptBuilder.FirstTask(settings, "测试任务");
				Assert(first.Contains("测试任务"), "任务未注入");
				Assert(first.Contains("[TASK_DONE]"), "首轮消息缺少完成标记说明");
				Assert(PromptBuilder.Incomplete("(").Contains("重新输出"), "缺少不完整命令纠正提示");
				Settings custom = new Settings();
				custom.SystemPrompt = "自定义提示词";
				Assert(PromptBuilder.System(custom) == "自定义提示词", "自定义提示词没有整体覆盖内置提示词");
				Assert(PromptBuilder.BuiltIn(custom).Contains("powershell_tool"), "BuiltIn 应始终返回 EAgent 内置原文");

				// 换电脑/换目录：自定义提示词里的四行本机信息必须按当前机器刷新
				Settings stale = new Settings();
				stale.SystemPrompt = "自定义开头\r\n- 操作系统: 旧系统\r\n- 当前时间: 2000-01-01 00:00:00\r\n- 工作目录: C:\\旧目录\r\n- 用户身份: 别人\r\n保留这一行";
				string refreshed = PromptBuilder.System(stale);
				Assert(refreshed.Contains("- 用户身份: " + Environment.UserName), "自定义提示词里的用户名没有按本机刷新");
				Assert(refreshed.Contains("- 工作目录: " + stale.WorkDir), "自定义提示词里的工作目录没有按设置刷新");
				Assert(!refreshed.Contains("别人") && !refreshed.Contains("旧系统"), "旧机器的信息没有被替换");
				Assert(refreshed.Contains("保留这一行"), "自定义提示词的其它内容被改动了");
			});

			r.Check("PowerShell 执行", delegate
			{
				PowerShellRunner runner = new PowerShellRunner(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 30);
				RunResult result = runner.Run("Write-Output ('OK-' + (40 + 2))");
				Assert(result.ExitCode == 0, "退出码 " + result.ExitCode + "，输出: " + result.Output);
				Assert(result.Output.Contains("OK-42"), "输出不符: " + result.Output);
			});

			r.Check("发送间隔", delegate
			{
				string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-delay-" + DateTime.Now.ToString("HHmmssfff"));
				Directory.CreateDirectory(sandbox);
				try
				{
					Settings settings = new Settings
					{
						WorkDir = sandbox,
						SendIntervalSec = 1,
						MaxSteps = 4,
						ReplyTimeoutSec = 30,
						CommandTimeoutSec = 30
					};
					DelayTestEngine engine = new DelayTestEngine();
					using (AgentSession session = new AgentSession(settings, engine))
					{
						session.ApproveHandler = (string cmd) => true;
						session.RunTaskAsync("测试发送间隔", CancellationToken.None).Wait();
					}
					Assert(engine.Sends.Count >= 2, "只发送了 " + engine.Sends.Count + " 次");
					double gapMs = (engine.Sends[1] - engine.Sends[0]).TotalMilliseconds;
					// 假引擎每轮回答固定耗时 600ms；间隔 1 秒必须叠加在回答之后 → 至少 1.5 秒
					Assert(gapMs >= 1500.0, "发送间隔没有从回答结束开始算：两次发送间隔只有 " + gapMs.ToString("0") + " ms");
					r.Info("发送间隔", gapMs.ToString("0") + " ms（设置 1 秒 + 回答 0.6 秒）");
				}
				finally
				{
					try
					{
						Directory.Delete(sandbox, true);
					}
					catch
					{
					}
				}
			});

			r.Check("无命令自动继续", delegate
			{
				string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-nudge-" + DateTime.Now.ToString("HHmmssfff"));
				Directory.CreateDirectory(sandbox);
				try
				{
					Settings settings = new Settings
					{
						WorkDir = sandbox,
						SendIntervalSec = 0,
						MaxSteps = 5,
						ReplyTimeoutSec = 30,
						CommandTimeoutSec = 30
					};
					ChattyTestEngine chatty = new ChattyTestEngine();
					using (AgentSession session = new AgentSession(settings, chatty))
					{
						session.ApproveHandler = (string cmd) => true;
						session.RunTaskAsync("测试催继续", CancellationToken.None).Wait();
					}
					Assert(chatty.Sends == 3, "只回文字时应自动催 1 次再执行命令，实际发送 " + chatty.Sends + " 次");

					NeedInputTestEngine needInput = new NeedInputTestEngine();
					using (AgentSession session = new AgentSession(settings, needInput))
					{
						session.ApproveHandler = (string cmd) => true;
						session.RunTaskAsync("测试等待输入", CancellationToken.None).Wait();
					}
					Assert(needInput.Sends == 1, "声明 [NEED_INPUT] 时不应自动催，实际发送 " + needInput.Sends + " 次");
					r.Info("无命令自动继续", "催继续 1 次后正常收尾；[NEED_INPUT] 不催");
				}
				finally
				{
					try
					{
						Directory.Delete(sandbox, true);
					}
					catch
					{
					}
				}
			});

			r.Check("工具协议解析", delegate
			{
				ParsedCommand shot = CommandParser.Parse("看屏幕：\r\n```screenshot\r\nscreen\r\n```");
				Assert(shot.Tool == ToolKind.Screenshot && shot.ToolPayload == "screen", "截图协议解析失败");
				ParsedCommand upload = CommandParser.Parse("```upload\r\nC:\\a\\1.png\r\n```");
				Assert(upload.Tool == ToolKind.Upload && upload.ToolPayload.Contains("1.png"), "上传协议解析失败");
				ParsedCommand mouse = CommandParser.Parse("```mouse\r\n200,300,double\r\n```");
				Assert(mouse.Tool == ToolKind.Mouse && mouse.ToolPayload.Contains("200,300"), "鼠标协议解析失败");
				ParsedCommand keyboard = CommandParser.Parse("```keyboard\r\n91,82\r\n```");
				Assert(keyboard.Tool == ToolKind.Keyboard, "键盘协议解析失败");
				ParsedCommand command = CommandParser.Parse("```powershell\r\nGet-Date\r\n```");
				Assert(command.Tool == ToolKind.Command && command.Command == "Get-Date", "PowerShell 解析被工具协议影响");
				ParsedCommand done = CommandParser.Parse("看完了\r\n[TASK_DONE]");
				Assert(done.Tool == ToolKind.None && done.Done, "完成标记解析失败");

				// EAgent 原版协议
				ParsedCommand psTool = CommandParser.Parse("```powershell_tool\r\n# powershell_tool_调用工具_识别头_本行注释一定要完全保留\r\n# 描述：测试\r\nWrite-Output eagent-ok\r\n# powershell_tool_调用工具_识别尾_本行注释一定要完全保留\r\n```");
				Assert(psTool.Tool == ToolKind.Command, "powershell_tool 没有被当成命令");
				Assert(psTool.Command.Contains("Write-Output eagent-ok"), "powershell_tool 代码丢失: " + psTool.Command);
				Assert(!psTool.Command.Contains("识别头") && !psTool.Command.Contains("识别尾"), "powershell_tool 标记没有被剥掉: " + psTool.Command);
				ParsedCommand jpg = CommandParser.Parse("```agent_getjpg\r\n# tool_取屏幕截图_调用工具_识别头_本行注释一定要完全保留\r\n# 取全屏截图\r\nagent_getjpg_1_star(屏幕)_1_getjpg_end\r\n# tool_取屏幕截图_调用工具_识别尾_本行注释一定要完全保留\r\n```");
				Assert(jpg.Tool == ToolKind.Screenshot && jpg.ToolPayload.Contains("屏幕"), "agent_getjpg 解析失败");
				ParsedCommand file = CommandParser.Parse("```agent_getfile\r\nagent_getfile_head('D:\\1.jpg')agent_getfile_end\r\n```");
				Assert(file.Tool == ToolKind.Upload && file.ToolPayload.Contains("D:\\1.jpg"), "agent_getfile 解析失败");
				ParsedCommand mouse2 = CommandParser.Parse("```agent_mouse_simulate\r\nagent_mouse_simulate_1_start(200)_agent_mouse_simulate_1_end\r\nagent_mouse_simulate_2_start(300)_agent_mouse_simulate_2_end\r\nagent_mouse_simulate_3_start(0)_agent_mouse_simulate_3_end\r\n```");
				Assert(mouse2.Tool == ToolKind.Mouse && mouse2.ToolPayload.Contains("agent_mouse_simulate_1_start(200)"), "agent_mouse_simulate 解析失败");
				ParsedCommand keyboard2 = CommandParser.Parse("```agent_keyboard_simulate\r\nagent_keyboard_simulate_1_start(91)_agent_keyboard_simulate_1_end\r\nagent_keyboard_simulate_2_start(82)_agent_keyboard_simulate_2_end\r\n```");
				Assert(keyboard2.Tool == ToolKind.Keyboard && keyboard2.ToolPayload.Contains("91"), "agent_keyboard_simulate 解析失败");
			});

			r.Check("工具参数解析", delegate
			{
				int x;
				int y;
				string action;
				Assert(ToolRunner.TryParseMouse("点这里：200, 300,right", out x, out y, out action), "鼠标参数未解析");
				Assert(x == 200 && y == 300 && action == "right", "鼠标参数值不对: " + x + "," + y + "," + action);
				Assert(!ToolRunner.TryParseMouse("没有坐标", out x, out y, out action), "无效鼠标参数不应解析成功");
				string eagentMouse = "agent_mouse_simulate_1_start(500)_agent_mouse_simulate_1_end\r\nagent_mouse_simulate_2_start(134)_agent_mouse_simulate_2_end\r\nagent_mouse_simulate_3_start(1)_agent_mouse_simulate_3_end";
				Assert(ToolRunner.TryParseMouse(eagentMouse, out x, out y, out action), "EAgent 鼠标参数未解析");
				Assert(x == 500 && y == 134 && action == "double", "EAgent 鼠标参数值不对: " + x + "," + y + "," + action);
				int hwnd;
				List<int> keys = ToolRunner.ParseKeyboard("91,82,123456", out hwnd);
				Assert(keys.Count == 2 && keys[0] == 91 && keys[1] == 82 && hwnd == 123456, "键盘参数/窗口句柄解析失败");
				string eagentKeys = "agent_keyboard_simulate_1_start(91)_agent_keyboard_simulate_1_end\r\nagent_keyboard_simulate_2_start(82)_agent_keyboard_simulate_2_end\r\nagent_keyboard_simulate_4_start(1234)_agent_keyboard_simulate_4_end";
				List<int> keys2 = ToolRunner.ParseKeyboard(eagentKeys, out hwnd);
				Assert(keys2.Count == 2 && keys2[0] == 91 && keys2[1] == 82 && hwnd == 1234, "EAgent 键盘参数/窗口句柄解析失败");
				string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-toolargs-" + DateTime.Now.ToString("HHmmssfff"));
				Directory.CreateDirectory(sandbox);
				try
				{
					string file = Path.Combine(sandbox, "a b.txt");
					File.WriteAllText(file, "x");
					List<string> paths = ToolRunner.ParsePaths("请上传：\r\n\"" + file + "\"");
					Assert(paths.Count == 1 && paths[0] == file, "带空格/引号的路径解析失败: " + string.Join("|", paths.ToArray()));
					List<string> eagentPaths = ToolRunner.ParsePaths("agent_getfile_head('" + file + "')agent_getfile_end");
					Assert(eagentPaths.Count == 1 && eagentPaths[0] == file, "agent_getfile 路径解析失败: " + string.Join("|", eagentPaths.ToArray()));
				}
				finally
				{
					try
					{
						Directory.Delete(sandbox, true);
					}
					catch
					{
					}
				}
			});

			r.Check("技能包解压", delegate
			{
				string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-skills-" + DateTime.Now.ToString("HHmmssfff"));
				Directory.CreateDirectory(sandbox);
				try
				{
					Settings settings = new Settings();
					settings.WorkDir = sandbox;
					string dir = Skills.Ensure(settings);
					Assert(dir != null, "解压技能包返回 null");
					Assert(Directory.Exists(dir), "技能目录不存在: " + dir);
					int count = Skills.CountSkills(dir);
					Assert(count >= 20, "技能数量异常: " + count);
					Assert(File.Exists(Path.Combine(dir, ".version")), "缺少 .version 标记");
					string again = Skills.Ensure(settings);
					Assert(again == dir, "第二次解压路径不一致");
					r.Info("技能包", count + " 个技能 -> " + dir);
				}
				finally
				{
					try
					{
						Directory.Delete(sandbox, true);
					}
					catch
					{
					}
				}
			});

			r.Check("随机发送间隔", delegate
			{
				Settings settings = new Settings();
				settings.SendIntervalSec = 4;
				settings.SendIntervalMaxSec = 2;
				settings.Normalize();
				Assert(settings.SendIntervalMaxSec == 4, "上限小于下限时应抬到下限");
				string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-random-" + DateTime.Now.ToString("HHmmssfff"));
				Directory.CreateDirectory(sandbox);
				try
				{
					Settings random = new Settings
					{
						WorkDir = sandbox,
						SendIntervalSec = 1,
						SendIntervalMaxSec = 2,
						MaxSteps = 3,
						ReplyTimeoutSec = 30,
						CommandTimeoutSec = 30
					};
					DelayTestEngine engine = new DelayTestEngine();
					using (AgentSession session = new AgentSession(random, engine))
					{
						session.ApproveHandler = (string cmd) => true;
						session.RunTaskAsync("测试随机间隔", CancellationToken.None).Wait();
					}
					double gap = (engine.Sends[1] - engine.Sends[0]).TotalMilliseconds;
					Assert(gap >= 1500.0, "随机间隔没有叠加在回答之后: " + gap.ToString("0") + " ms");
					Assert(gap <= 600.0 + 2400.0, "随机间隔超出上限: " + gap.ToString("0") + " ms");
					r.Info("随机间隔", gap.ToString("0") + " ms（1–2 秒 + 回答 0.6 秒）");
				}
				finally
				{
					try
					{
						Directory.Delete(sandbox, true);
					}
					catch
					{
					}
				}
			});

			r.Check("截图与工具闭环", delegate
			{
				string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-toolrun-" + DateTime.Now.ToString("HHmmssfff"));
				Directory.CreateDirectory(sandbox);
				try
				{
					Settings settings = new Settings
					{
						WorkDir = sandbox,
						SendIntervalSec = 0,
						MaxSteps = 4,
						ReplyTimeoutSec = 30,
						CommandTimeoutSec = 30
					};
					string report = ToolRunner.SelfCheck(settings);
					Assert(report.Contains("截图"), "工具自检没有截图输出");
					ToolTestEngine engine = new ToolTestEngine();
					using (AgentSession session = new AgentSession(settings, engine))
					{
						session.ApproveHandler = (string cmd) => true;
						session.RunTaskAsync("先截图", CancellationToken.None).Wait();
					}
					Assert(engine.Sends == 2, "工具闭环应发送 2 次，实际 " + engine.Sends);
					Assert(engine.Attachments >= 1, "截图没有作为附件回传");
					Assert(engine.LastText.Contains("工具结果"), "工具结果没有回喂给模型");
					r.Info("工具闭环", "截图 -> 附件 -> 模型继续");
				}
				finally
				{
					try
					{
						Directory.Delete(sandbox, true);
					}
					catch
					{
					}
				}
			});

			r.Check("提示词包含工具说明", delegate
			{
				Settings enabled = new Settings();
				enabled.EnableTools = true;
				string withTools = PromptBuilder.System(enabled);
				Assert(withTools.Contains("agent_getjpg") && withTools.Contains("agent_getfile") && withTools.Contains("agent_mouse_simulate") && withTools.Contains("agent_keyboard_simulate"), "启用工具时提示词缺少 EAgent 工具说明");
				Settings disabled = new Settings();
				disabled.EnableTools = false;
				string withoutTools = PromptBuilder.System(disabled);
				Assert(withoutTools.Contains("powershell_tool"), "关闭扩展工具后 PowerShell 说明也被删了");
				Assert(!withoutTools.Contains("agent_getjpg"), "关闭工具后提示词仍出现截图说明");
			});

			r.Check("浏览器定位", delegate
			{
				using (EdgeLauncher launcher = new EdgeLauncher(new Settings()))
				{
					string browser = launcher.ResolveBrowser();
					Assert(browser != null && File.Exists(browser), "未找到 Edge/Chrome，可在设置里手动指定");
					r.Info("浏览器", browser);
				}
			});

			r.Check("CDP 客户端释放", delegate
			{
				CdpClient client = new CdpClient();
				client.Dispose();
				bool threw = false;
				try
				{
					client.SendAsync("Runtime.evaluate", null, 1000).Wait();
				}
				catch (AggregateException ex)
				{
					threw = ex.InnerException is ObjectDisposedException;
				}
				Assert(threw, "释放后仍能发送请求（应抛 ObjectDisposedException）");
			});

			r.Check("界面控件可用", delegate
			{
				Assert(Theme.Accent != Color.Empty, "主题未初始化");
				using (ApproveForm approveForm = new ApproveForm("Get-Date"))
				{
					Assert(approveForm.Text.Length > 0, "审批窗口构造失败");
				}
				using (SettingsForm settingsForm = new SettingsForm(new Settings()))
				{
					Assert(settingsForm.Text.Length > 0, "设置窗口构造失败");
				}
				using (DelayForm delayForm = new DelayForm(5, 8))
				{
					Assert(delayForm.Seconds == 5 && delayForm.MaxSeconds == 8, "发送间隔窗口构造失败");
				}
			});

			r.Write("HEAD", "自检结束: 通过 " + r.Pass + " 项，失败 " + r.Fail + " 项");
			try
			{
				File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log"), r.Report.ToString(), Encoding.UTF8);
			}
			catch
			{
			}
			try
			{
				Console.WriteLine("报告: " + Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.log"));
			}
			catch
			{
			}
			return (r.Fail == 0) ? 0 : 1;
		}

		public static int ChatCheck()
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 0;
			try
			{
				Settings settings = Settings.Load();
				settings.Normalize();
				settings.ReplyTimeoutSec = Math.Max(120, settings.ReplyTimeoutSec);
				report.AppendLine("EAgentX 网页对话检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				using (WebEngine engine = new WebEngine(settings))
				{
					report.AppendLine(engine.StartAsync(CancellationToken.None).Result);
					report.AppendLine("--- 发送测试消息 ---");
					string reply = engine.SendAsync("请只回复两个字：OK。不要调用任何工具，不要输出代码块。", null, CancellationToken.None).Result;
					report.AppendLine("回复: " + reply);
					report.AppendLine("结果: 对话链路正常。");
					exitCode = 0;
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
				exitCode = 1;
			}
			WriteReport("chatcheck.log", report.ToString());
			return exitCode;
		}

		public static int UploadCheck(string file)
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 0;
			try
			{
				if (string.IsNullOrEmpty(file))
				{
					file = Path.Combine(Path.GetTempPath(), "eagentx-upload-test.txt");
					File.WriteAllText(file, "EAgentX upload test\r\n" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"), Encoding.UTF8);
				}
				if (!File.Exists(file))
				{
					throw new Exception("文件不存在: " + file);
				}
				Settings settings = Settings.Load();
				settings.Normalize();
				report.AppendLine("EAgentX 附件检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				report.AppendLine("文件: " + Path.GetFullPath(file));
				using (WebEngine engine = new WebEngine(settings))
				{
					engine.StartAsync(CancellationToken.None).Wait();
					report.AppendLine(engine.AttachFilesAsync(new string[1] { file }, CancellationToken.None).Result);
					report.AppendLine("结果: 已调用网页附件接口，请在浏览器输入框上方确认附件缩略图。");
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
				exitCode = 1;
			}
			WriteReport("uploadcheck.log", report.ToString());
			return exitCode;
		}

		public static int AgentCheck()
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 1;
			string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-agentcheck-" + DateTime.Now.ToString("HHmmss"));
			try
			{
				Directory.CreateDirectory(sandbox);
				Settings settings = Settings.Load();
				settings.Normalize();
				settings.WorkDir = sandbox;
				settings.MaxSteps = 6;
				settings.CommandTimeoutSec = 60;
				settings.ReplyTimeoutSec = Math.Max(300, settings.ReplyTimeoutSec);
				settings.AutoApprove = false;
				report.AppendLine("EAgentX 智能体闭环检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				report.AppendLine("沙箱目录: " + sandbox);
				report.AppendLine("");
				using (WebEngine engine = new WebEngine(settings))
				{
					report.AppendLine("--- 新建对话 ---");
					engine.StartAsync(CancellationToken.None).Wait();
					engine.ResetAsync(CancellationToken.None).Wait();
					report.AppendLine(engine.Diagnostics);
					report.AppendLine("");
					AgentSession session = new AgentSession(settings, engine);
					session.StateChanged += delegate(string state)
					{
						report.AppendLine("[状态] " + state);
					};
					session.UserMessage += delegate(string text)
					{
						report.AppendLine("[用户] " + text);
					};
					session.CommandStarted += delegate(string command)
					{
						report.AppendLine("[命令] " + command);
					};
					session.AssistantMessage += delegate(string text)
					{
						report.AppendLine("[助手] " + text + "\r\n");
					};
					session.CommandFinished += delegate(RunResult result)
					{
						report.AppendLine("[结果] 退出码=" + result.ExitCode + "  " + result.ElapsedMs + "ms");
						report.AppendLine(result.Output);
					};
					session.ApproveHandler = delegate(string command)
					{
						// 无人值守测试里只放行安全命令，命中黑名单一律拒绝
						string lower = command.ToLowerInvariant();
						string[] blocked = new string[17]
						{
							"remove-item", "rmdir", " rd ", " del ", "format-", "shutdown", "restart-computer", "stop-process", "reg add", "reg delete",
							"set-executionpolicy", "invoke-webrequest", "invoke-expression", "start-process", "net user", "takeown", "cipher"
						};
						foreach (string word in blocked)
						{
							if (lower.Contains(word))
							{
								report.AppendLine("[审批] 拒绝（命中黑名单 " + word.Trim() + "）");
								return false;
							}
						}
						report.AppendLine("[审批] 允许");
						return true;
					};
					string task = "在当前工作目录创建一个名为 hello.txt 的文本文件，内容为 hello-eagentx。只用一条 PowerShell 命令，完成后输出 [TASK_DONE]。";
					session.RunTaskAsync(task, CancellationToken.None).Wait();
				}
				string productPath = Path.Combine(sandbox, "hello.txt");
				if (File.Exists(productPath))
				{
					string content = File.ReadAllText(productPath);
					report.AppendLine("");
					report.AppendLine("产物内容: " + content.Trim());
					if (content.IndexOf("hello-eagentx", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						report.AppendLine("结果: 通过，智能体闭环正常。");
						exitCode = 0;
					}
					else
					{
						report.AppendLine("结果: 文件内容不符。");
					}
				}
				else
				{
					report.AppendLine("");
					report.AppendLine("结果: 没有生成 hello.txt。");
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
			}
			WriteReport("agentcheck.log", report.ToString());
			return exitCode;
		}

		public static int LongCheck()
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 1;
			string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-longcheck-" + DateTime.Now.ToString("HHmmss"));
			int commandCount = 0;
			int incompleteEvents = 0;
			try
			{
				Directory.CreateDirectory(sandbox);
				Settings settings = Settings.Load();
				settings.Normalize();
				settings.WorkDir = sandbox;
				settings.MaxSteps = 8;
				settings.CommandTimeoutSec = 90;
				settings.ReplyTimeoutSec = Math.Max(300, settings.ReplyTimeoutSec);
				settings.AutoApprove = false;
				report.AppendLine("EAgentX 长文件闭环检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				report.AppendLine("沙箱目录: " + sandbox);
				report.AppendLine("");
				using (WebEngine engine = new WebEngine(settings))
				{
					report.AppendLine("--- 新建对话 ---");
					engine.StartAsync(CancellationToken.None).Wait();
					engine.ResetAsync(CancellationToken.None).Wait();
					report.AppendLine(engine.Diagnostics);
					report.AppendLine("");
					AgentSession session = new AgentSession(settings, engine);
					session.StateChanged += delegate(string state)
					{
						report.AppendLine("[状态] " + state);
						if (state != null && state.IndexOf("命令不完整", StringComparison.Ordinal) >= 0)
						{
							incompleteEvents++;
						}
					};
					session.UserMessage += delegate(string text)
					{
						report.AppendLine("[用户] " + text);
					};
					session.CommandStarted += delegate(string command)
					{
						commandCount++;
						report.AppendLine("[命令 " + commandCount + " · " + command.Length + " 字符]");
						report.AppendLine(command);
						report.AppendLine("");
					};
					session.AssistantMessage += delegate(string text)
					{
						report.AppendLine("[助手] " + text + "\r\n");
					};
					session.CommandFinished += delegate(RunResult result)
					{
						report.AppendLine("[结果] 退出码=" + result.ExitCode + "  " + result.ElapsedMs + "ms");
						report.AppendLine(result.Output);
						report.AppendLine("");
					};
					session.ApproveHandler = delegate(string command)
					{
						string lower = command.ToLowerInvariant();
						string[] blocked = new string[17]
						{
							"remove-item", "rmdir", " rd ", " del ", "format-", "shutdown", "restart-computer", "stop-process", "reg add", "reg delete",
							"set-executionpolicy", "invoke-webrequest", "invoke-expression", "start-process", "net user", "takeown", "cipher"
						};
						foreach (string word in blocked)
						{
							if (lower.Contains(word))
							{
								report.AppendLine("[审批] 拒绝（命中黑名单 " + word.Trim() + "）");
								return false;
							}
						}
						report.AppendLine("[审批] 允许");
						return true;
					};
					string task = "在当前工作目录创建 long.html。要求：文件至少 3500 字符，包含 <!DOCTYPE html> 和注释 <!-- EAGENTX-LONG -->。请尽量用一条 PowerShell 命令（here-string + Set-Content）写完，不要拆成很多 300 字符左右的小段，也不要用循环重复同一段文字来凑长度。完成后输出 [TASK_DONE]。";
					session.RunTaskAsync(task, CancellationToken.None).Wait();
				}
				string productPath = Path.Combine(sandbox, "long.html");
				report.AppendLine("");
				report.AppendLine("命令次数: " + commandCount + " · 不完整告警: " + incompleteEvents);
				if (File.Exists(productPath))
				{
					string content = File.ReadAllText(productPath);
					report.AppendLine("产物大小: " + content.Length + " 字符");
					if (content.Length >= 3000 && content.IndexOf("<!DOCTYPE html>", StringComparison.OrdinalIgnoreCase) >= 0 && content.IndexOf("EAGENTX-LONG", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						report.AppendLine("结果: 通过，长文件完整生成。");
						exitCode = 0;
					}
					else
					{
						report.AppendLine("结果: 文件存在，但长度或标记不符。");
					}
				}
				else
				{
					report.AppendLine("结果: 没有生成 long.html。");
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
			}
			WriteReport("longcheck.log", report.ToString());
			return exitCode;
		}

		/// <summary>实测「窗口置入」：启动浏览器 → 找到窗口 → SetParent 到一个临时窗体 → 校验 → 恢复。</summary>
		public static int EmbedCheck()
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 1;
			try
			{
				Settings settings = Settings.Load();
				settings.Normalize();
				settings.ReplyTimeoutSec = Math.Max(120, settings.ReplyTimeoutSec);
				report.AppendLine("EAgentX 窗口置入检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				report.AppendLine("起始网址: " + settings.StartUrl + "  CDP 端口: " + settings.CdpPort);
				using (Form host = new Form())
				{
					host.Text = "EAgentX embedcheck";
					host.ClientSize = new Size(900, 620);
					host.StartPosition = FormStartPosition.Manual;
					host.Location = new Point(80, 60);
					host.Show();
					Application.DoEvents();
					using (EdgeLauncher launcher = new EdgeLauncher(settings))
					{
						launcher.EnsureStarted();
						report.AppendLine("页面目标: " + launcher.WaitForChatTargetAsync(60000, CancellationToken.None).Result);
						IntPtr hwnd = IntPtr.Zero;
						for (int i = 0; i < 20 && hwnd == IntPtr.Zero; i++)
						{
							hwnd = Win32.FindBrowserWindow(launcher.ProcessId, "DeepSeek");
							if (hwnd == IntPtr.Zero)
							{
								Thread.Sleep(500);
							}
						}
						if (hwnd == IntPtr.Zero)
						{
							throw new Exception("没有找到浏览器窗口（类名 Chrome_WidgetWin_1 / 标题含 DeepSeek）");
						}
						report.AppendLine("窗口句柄: " + hwnd + "  标题: " + Win32.GetTitle(hwnd));
						int style = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
						Win32.RECT rect;
						Win32.GetWindowRect(hwnd, out rect);
						Win32.SetParent(hwnd, host.Handle);
						Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, (style | Win32.WS_CHILD) & ~Win32.WS_POPUP & ~Win32.WS_CAPTION & ~Win32.WS_THICKFRAME);
						Win32.MoveWindow(hwnd, 0, 0, host.ClientSize.Width, host.ClientSize.Height, true);
						Win32.ShowWindow(hwnd, Win32.SW_SHOW);
						Application.DoEvents();
						Thread.Sleep(1500);
						Application.DoEvents();
						IntPtr parent = Win32.GetParent(hwnd);
						report.AppendLine("嵌入后父窗口: " + parent + "（期望 " + host.Handle + "）");
						if (parent != host.Handle)
						{
							throw new Exception("SetParent 没有生效");
						}
						report.AppendLine("结果: 置入成功（1.5 秒后恢复独立窗口）。");
						exitCode = 0;
						Win32.SetParent(hwnd, IntPtr.Zero);
						Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, style);
						Win32.MoveWindow(hwnd, rect.Left, rect.Top, Math.Max(320, rect.Width), Math.Max(240, rect.Height), true);
						Win32.ShowWindow(hwnd, Win32.SW_SHOW);
					}
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
				exitCode = 1;
			}
			WriteReport("embedcheck.log", report.ToString());
			return exitCode;
		}

		/// <summary>实测「目标窗口被其它软件完全覆盖」时的截图：红色目标 + 蓝色遮挡窗体，截完检查中心仍是红色。</summary>
		public static int ShotCheck()
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 1;
			string sandbox = Path.Combine(Path.GetTempPath(), "eagentx-shotcheck-" + DateTime.Now.ToString("HHmmss"));
			try
			{
				Directory.CreateDirectory(sandbox);
				Settings settings = new Settings();
				settings.WorkDir = sandbox;
				settings.Normalize();
				report.AppendLine("EAgentX 遮挡截图检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				using (Form target = new Form())
				using (Form cover = new Form())
				{
					target.Text = "EAgentX shot target";
					target.FormBorderStyle = FormBorderStyle.FixedSingle;
					target.StartPosition = FormStartPosition.Manual;
					target.Location = new Point(320, 200);
					target.ClientSize = new Size(520, 360);
					target.BackColor = Color.FromArgb(220, 30, 30);
					Label label = new Label();
					label.Text = "EAGENTX-TARGET";
					label.Dock = DockStyle.Top;
					label.Height = 42;
					label.TextAlign = ContentAlignment.MiddleCenter;
					label.BackColor = Color.White;
					label.ForeColor = Color.Black;
					target.Controls.Add(label);
					target.Show();

					cover.Text = "EAgentX cover";
					cover.FormBorderStyle = FormBorderStyle.None;
					cover.StartPosition = FormStartPosition.Manual;
					cover.Location = new Point(280, 160);
					cover.ClientSize = new Size(620, 460);
					cover.BackColor = Color.FromArgb(20, 40, 200);
					cover.Show();
					cover.BringToFront();
					Application.DoEvents();
					Thread.Sleep(350);
					Application.DoEvents();

					ToolResult shot = ToolRunner.Screenshot(settings, target.Handle.ToInt64().ToString());
					if (!shot.Ok || shot.Attachments == null || shot.Attachments.Length == 0)
					{
						throw new Exception("截图失败: " + ((shot == null) ? "null" : shot.Text));
					}
					string file = shot.Attachments[0];
					using (Bitmap bitmap = new Bitmap(file))
					{
						Color center = bitmap.GetPixel(bitmap.Width / 2, bitmap.Height * 3 / 4);
						report.AppendLine("目标窗口: " + target.Handle + "  遮挡窗口: " + cover.Handle);
						report.AppendLine("截图: " + bitmap.Width + "×" + bitmap.Height + " -> " + file);
						report.AppendLine("取样颜色: R=" + center.R + " G=" + center.G + " B=" + center.B + "（期望红色）");
						report.AppendLine(shot.Text);
						if (center.R < 140 || center.G > 100 || center.B > 100)
						{
							throw new Exception("截到的是遮挡窗口或黑图，取样颜色 R=" + center.R + " G=" + center.G + " B=" + center.B);
						}
					}
					report.AppendLine("结果: 通过，目标被完全覆盖时仍截到了目标内容。");
					exitCode = 0;
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
				exitCode = 1;
			}
			finally
			{
				try
				{
					Directory.Delete(sandbox, true);
				}
				catch
				{
				}
			}
			WriteReport("shotcheck.log", report.ToString());
			return exitCode;
		}

		public static int WebCheck()
		{
			StringBuilder report = new StringBuilder();
			int exitCode = 0;
			try
			{
				Settings settings = Settings.Load();
				settings.Normalize();
				report.AppendLine("EAgentX 网页模式检查 · " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				report.AppendLine("起始网址: " + settings.StartUrl + "  CDP 端口: " + settings.CdpPort);
				using (WebEngine engine = new WebEngine(settings))
				{
					string probe = engine.ProbeAsync(CancellationToken.None).Result;
					report.AppendLine(probe);
					// 浏览器窗口即使被遮挡/最小化，也应能通过 CDP 直接截到页面
					IntPtr hwnd = Win32.FindBrowserWindow(engine.BrowserProcessId, "DeepSeek");
					if (hwnd != IntPtr.Zero)
					{
						byte[] pageShot = engine.TryCaptureWindow(hwnd);
						if (pageShot != null && pageShot.Length > 0)
						{
							string shotPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "webcheck-shot.jpg");
							File.WriteAllBytes(shotPath, pageShot);
							report.AppendLine("CDP 页面截图: " + shotPath + "（" + pageShot.Length + " 字节）");
						}
						else
						{
							report.AppendLine("CDP 页面截图: 未触发（窗口不匹配或页面还没渲染）");
						}
					}
					if (probe.IndexOf("输入框: 未找到", StringComparison.Ordinal) >= 0)
					{
						report.AppendLine("结果: 已连通，但页面上没有聊天输入框（通常表示尚未登录）。");
						exitCode = 2;
					}
					else
					{
						report.AppendLine("结果: 已连通，输入框正常，可以开始对话。");
						exitCode = 0;
					}
				}
			}
			catch (Exception ex)
			{
				report.AppendLine("结果: 失败 - " + Unwrap(ex).Message);
				exitCode = 1;
			}
			WriteReport("webcheck.log", report.ToString());
			return exitCode;
		}
	}
}
