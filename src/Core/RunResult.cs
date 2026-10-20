namespace EAgentX.Core
{
	/// <summary>一条命令的执行结果，用来回喂给模型。</summary>
	public class RunResult
	{
		public string Command;

		public int ExitCode;

		/// <summary>stdout + stderr 合并后的文本（超长会被截断）。</summary>
		public string Output;

		public bool TimedOut;

		/// <summary>用户在审批弹窗里点了拒绝。</summary>
		public bool Denied;

		public long ElapsedMs;
	}
}
