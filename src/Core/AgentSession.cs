using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAgentX.Engines;
using EAgentX.Util;

namespace EAgentX.Core
{
	/// <summary>
	/// 智能体主循环：发送 → 解析 → 审批 → 执行 → 回喂，直到 [TASK_DONE] 或步数用尽。
	/// 每一步都会写进会话轨迹（&lt;工作目录&gt;\.eagentx\session-时间戳.md），可审计。
	/// </summary>
	public class AgentSession : IDisposable
	{
		private readonly Settings S;

		private readonly PowerShellRunner Runner;

		private readonly object FileGate = new object();

		private readonly string TranscriptPath;

		private bool Started;

		/// <summary>是否已经跑过任务；决定首轮是发完整任务包装还是「新任务」追问。</summary>
		private bool HasRun;

		/// <summary>上一轮模型回答到达的时间。发送间隔从这里开始算（回答之后 → 下一次发送）。</summary>
		private DateTime LastReplyAt = DateTime.MinValue;

		/// <summary>模型只回文字、不给命令时自动催它继续的次数；给了命令就清零。</summary>
		private int ContinueNudges;

		private static readonly Random Rng = new Random();

		private static readonly object RngGate = new object();

		/// <summary>测试版：本次任务要随消息一起发送的附件（截图/上传）。</summary>
		private string[] PendingAttachments;

		/// <summary>命令审批回调，返回 false 表示用户拒绝执行。</summary>
		public Func<string, bool> ApproveHandler;

		public IChatEngine Engine { get; private set; }

		public string TranscriptFile
		{
			get
			{
				return TranscriptPath;
			}
		}

		public event Action<string> StateChanged;

		public event Action<string> AssistantMessage;

		public event Action<string> CommandStarted;

		public event Action<RunResult> CommandFinished;

		public event Action<string> UserMessage;

		public AgentSession(Settings settings, IChatEngine engine)
		{
			S = settings;
			Engine = engine;
			Runner = new PowerShellRunner(settings.WorkDir, settings.CommandTimeoutSec);
			// 测试版：先把技能包解压到工作目录，提示词里会带上技能库路径
			Skills.Ensure(settings);
			// 窗口截图被遮挡时的浏览器兜底：走 CDP 页面截图，不依赖窗口是否可见
			WebEngine web = engine as WebEngine;
			if (web != null)
			{
				ToolRunner.BrowserCapture = delegate(IntPtr hwnd)
				{
					return web.TryCaptureWindow(hwnd);
				};
			}
			else
			{
				ToolRunner.BrowserCapture = null;
			}
			try
			{
				string sessionDir = Path.Combine(settings.WorkDir, ".eagentx");
				if (!Directory.Exists(sessionDir))
				{
					Directory.CreateDirectory(sessionDir);
				}
				TranscriptPath = Path.Combine(sessionDir, "session-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".md");
				WriteLine("# EAgentX 会话记录\r\n");
				WriteLine("- 时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
				WriteLine("- 引擎: " + engine.Name);
				WriteLine("- 工作目录: " + settings.WorkDir);
				WriteLine("");
			}
			catch
			{
				// 轨迹写不了不能影响主流程
				TranscriptPath = null;
			}
		}

		public async Task<string> StartAsync(CancellationToken ct)
		{
			if (Started)
			{
				return "";
			}
			Raise(StateChanged, "正在连接引擎…");
			string info = await Engine.StartAsync(ct);
			Started = true;
			Raise(StateChanged, "引擎就绪");
			Log.Dim(info);
			return info;
		}

		public async Task RunTaskAsync(string task, CancellationToken ct)
		{
			if (!Started)
			{
				await StartAsync(ct);
			}
			Raise(UserMessage, task);
			WriteLine("## 用户\r\n\r\n" + task + "\r\n");
			string outbound = (HasRun ? ("【新任务】\r\n" + task + "\r\n\r\n（继续遵守之前的格式：每条回复只给一个工具代码块，通常是 ```powershell；块里可以写多条语句；完成后输出 [TASK_DONE]。）") : PromptBuilder.FirstTask(S, task));
			HasRun = true;
			ContinueNudges = 0;
			PendingAttachments = null;
			int step = 0;
			int incompleteRetries = 0;
			while (step < S.MaxSteps)
			{
				ct.ThrowIfCancellationRequested();
				step++;
				// 网页版限流：上一次「回答结束」到这一次「发送指令」之间至少间隔 interval 秒
				// 测试版：SendIntervalMaxSec 大于下限时，每一步在 [下限, 上限] 里随机
				int intervalMs = NextIntervalMs();
				if (intervalMs > 0 && LastReplyAt != DateTime.MinValue)
				{
					int waitMs = intervalMs - (int)(DateTime.Now - LastReplyAt).TotalMilliseconds;
					if (waitMs > intervalMs)
					{
						// 系统时间被往回调时别无限等
						waitMs = intervalMs;
					}
					if (waitMs > 0)
					{
						Raise(StateChanged, "发送间隔：等待 " + (waitMs + 999) / 1000 + " 秒…");
						await Task.Delay(waitMs, ct);
					}
				}
				Raise(StateChanged, "第 " + step + " 步：等待模型回复…");
				string reply = await Engine.SendAsync(outbound, PendingAttachments, ct);
				PendingAttachments = null;
				LastReplyAt = DateTime.Now;
				if (string.IsNullOrEmpty(reply) || reply.Trim().Length == 0)
				{
					throw new Exception("模型返回了空内容，请重试。");
				}
				Raise(AssistantMessage, reply);
				WriteLine("## 助手（第 " + step + " 步）\r\n\r\n" + reply + "\r\n");
				ParsedCommand parsed = CommandParser.Parse(reply);

				// 测试版：截图 / 上传 / 鼠标 / 键盘 → 宿主执行，把结果（和附件）回喂给模型
				if (parsed.Tool != ToolKind.None && parsed.Tool != ToolKind.Command)
				{
					incompleteRetries = 0;
					ContinueNudges = 0;
					string toolName = PromptBuilder.KindName(parsed.Tool);
					if (!S.EnableTools)
					{
						Raise(StateChanged, "扩展工具已在设置里关闭，已把结果告知模型");
						WriteLine("## 系统\r\n\r\n扩展工具已关闭，拒绝执行：" + toolName + "\r\n");
						outbound = "【工具已关闭】设置里关闭了截图 / 上传 / 鼠标 / 键盘，只能用 PowerShell 完成任务。请改用 ```powershell_tool 代码块，或输出 [TASK_DONE] 结束。";
						continue;
					}
					string payload = (parsed.ToolPayload == null) ? "" : parsed.ToolPayload;
					if ((parsed.Tool == ToolKind.Mouse || parsed.Tool == ToolKind.Keyboard) && ApproveHandler != null &&
						!ApproveHandler("【" + toolName + "】" + payload))
					{
						WriteLine("## 审批\r\n\r\n用户拒绝执行该工具调用。\r\n");
						Raise(StateChanged, "工具调用被拒绝，已把结果告知模型");
						outbound = PromptBuilder.Denied("【" + toolName + "】" + payload);
						continue;
					}
					Raise(CommandStarted, "【" + toolName + "】" + payload);
					WriteLine("## 工具（" + toolName + "）\r\n\r\n```text\r\n" + payload + "\r\n```\r\n");
					Raise(StateChanged, "执行工具：" + toolName + "…");
					long startedAt = Environment.TickCount;
					ToolResult toolResult = await Task.Run(() => ToolRunner.Run(S, parsed.Tool, parsed.ToolPayload));
					RunResult toolRun = new RunResult();
					toolRun.Command = "【" + toolName + "】" + payload;
					toolRun.ExitCode = (toolResult.Ok ? 0 : 1);
					toolRun.Output = toolResult.Text;
					toolRun.ElapsedMs = Environment.TickCount - startedAt;
					Raise(CommandFinished, toolRun);
					WriteLine("## 工具结果\r\n\r\n" + (toolResult.Text == null ? "" : toolResult.Text) + "\r\n");
					if (parsed.Done && toolResult.Ok)
					{
						Raise(StateChanged, "任务完成");
						return;
					}
					if (!toolResult.Ok)
					{
						Log.Error("工具执行失败：" + toolResult.Text);
					}
					outbound = PromptBuilder.ToolFeedback(toolResult.Text, parsed.Tool, toolResult.Ok);
					PendingAttachments = toolResult.Attachments;
					continue;
				}

				// 情况一：有命令，但形状不完整（被截断）→ 不执行，要求重写
				if (parsed.Command != null && !CommandShape.IsComplete(parsed.Command))
				{
					incompleteRetries++;
					string warning = "命令可能被截断，已拒绝执行（" + incompleteRetries + "/3）：\r\n" + parsed.Command;
					Log.Error(warning);
					WriteLine("## 系统\r\n\r\n" + warning + "\r\n");
					Raise(StateChanged, "命令不完整，正在让模型重写（" + incompleteRetries + "/3）…");
					if (incompleteRetries >= 3)
					{
						throw new Exception("模型连续返回不完整命令（引号/括号/here-string 未闭合，或结尾停在操作符）。\r\n请点「新建会话」开一个干净的对话后重试，避免旧上下文继续影响输出。");
					}
					outbound = PromptBuilder.Incomplete(parsed.Command);
					continue;
				}

				// 情况二：没有命令，但回复里的代码块围栏没闭合 → 同样是残片
				if (parsed.Command == null && !parsed.Done && CommandParser.HasUnclosedFence(reply))
				{
					incompleteRetries++;
					string warning = "回复里的代码块没有闭合，已拒绝执行（" + incompleteRetries + "/3）。";
					Log.Error(warning);
					WriteLine("## 系统\r\n\r\n" + warning + "\r\n");
					Raise(StateChanged, "回复不完整，正在让模型重写（" + incompleteRetries + "/3）…");
					if (incompleteRetries >= 3)
					{
						throw new Exception("模型连续返回不完整回复（代码块未闭合）。\r\n请点「新建会话」后重试。");
					}
					outbound = PromptBuilder.Incomplete("（回复中的代码块没有闭合）");
					continue;
				}

				// 情况三：没有命令 → 先确认是不是在等用户补充信息，否则自动催它继续
				if (parsed.Command == null)
				{
					if (parsed.Done)
					{
						Raise(StateChanged, "任务完成");
						return;
					}
					if (reply.IndexOf("[NEED_INPUT]", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						Raise(StateChanged, "等待你的补充信息");
						return;
					}
					if (ContinueNudges < 2)
					{
						ContinueNudges++;
						string nudge = "模型没有给出命令，自动催它继续（" + ContinueNudges + "/2）。";
						Log.Info(nudge);
						WriteLine("## 系统\r\n\r\n" + nudge + "\r\n");
						Raise(StateChanged, "模型只回了文字，正在催它继续（" + ContinueNudges + "/2）…");
						outbound = PromptBuilder.Continue();
						continue;
					}
					Raise(StateChanged, "本轮结束（没有需要执行的命令）");
					return;
				}

				incompleteRetries = 0;
				ContinueNudges = 0;
				bool approved = true;
				if (ApproveHandler != null)
				{
					approved = ApproveHandler(parsed.Command);
				}
				if (!approved)
				{
					WriteLine("## 审批\r\n\r\n用户拒绝执行该命令。\r\n");
					Raise(StateChanged, "命令被拒绝，已把结果告知模型");
					outbound = PromptBuilder.Denied(parsed.Command);
					continue;
				}

				Raise(CommandStarted, parsed.Command);
				WriteLine("## 命令\r\n\r\n```powershell\r\n" + parsed.Command + "\r\n```\r\n");
				Raise(StateChanged, "执行命令中…");
				RunResult result = await Task.Run(() => Runner.Run(parsed.Command));
				Raise(CommandFinished, result);
				WriteLine("## 执行结果\r\n\r\n- 退出码: " + result.ExitCode + "（" + result.ElapsedMs + " ms）\r\n\r\n```text\r\n" + (result.Output ?? "") + "\r\n```\r\n");
				// 模型常把 [TASK_DONE] 写进命令末尾的 Write-Output/注释里；命令失败时不能当成完成
				if (parsed.Done && result.ExitCode == 0 && !result.TimedOut)
				{
					Raise(StateChanged, "任务完成");
					return;
				}
				outbound = PromptBuilder.Feedback(result);
			}
			string tip = PromptBuilder.MaxStepsReached(S.MaxSteps);
			Raise(StateChanged, tip);
			Log.Dim(tip);
			WriteLine("## 系统\r\n\r\n" + tip + "\r\n");
		}

		private void WriteLine(string text)
		{
			if (string.IsNullOrEmpty(TranscriptPath))
			{
				return;
			}
			try
			{
				lock (FileGate)
				{
					File.AppendAllText(TranscriptPath, text + "\r\n", Encoding.UTF8);
				}
			}
			catch
			{
			}
		}

		private int NextIntervalMs()
		{
			int min = S.SendIntervalSec;
			if (min <= 0)
			{
				return 0;
			}
			int max = S.SendIntervalMaxSec;
			if (max < min)
			{
				max = min;
			}
			int seconds = min;
			if (max > min)
			{
				lock (RngGate)
				{
					seconds = Rng.Next(min, max + 1);
				}
			}
			return seconds * 1000;
		}

		private static void Raise(Action<string> handler, string value)
		{
			if (handler == null)
			{
				return;
			}
			try
			{
				handler(value);
			}
			catch
			{
				// 界面回调抛异常不能打断智能体循环
			}
		}

		private static void Raise(Action<RunResult> handler, RunResult value)
		{
			if (handler == null)
			{
				return;
			}
			try
			{
				handler(value);
			}
			catch
			{
			}
		}

		public void Dispose()
		{
			try
			{
				if (Engine != null)
				{
					Engine.Dispose();
				}
			}
			catch
			{
			}
			Engine = null;
		}
	}
}
