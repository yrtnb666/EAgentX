namespace EAgentX.Core
{
	/// <summary>模型这一轮想调用的工具。</summary>
	public enum ToolKind
	{
		None,
		Command,
		Screenshot,
		Upload,
		Mouse,
		Keyboard
	}

	/// <summary>一轮模型回复的解析结果。</summary>
	public class ParsedCommand
	{
		/// <summary>提取出来的 PowerShell 命令；没有命令时为 null。</summary>
		public string Command;

		/// <summary>测试版：这一轮调用的工具类型；Command 表示 PowerShell。</summary>
		public ToolKind Tool = ToolKind.None;

		/// <summary>测试版：截图 / 上传 / 鼠标 / 键盘的工具参数原文。</summary>
		public string ToolPayload;

		/// <summary>回复里是否出现 [TASK_DONE]。</summary>
		public bool Done;

		/// <summary>去掉首尾空白后的原始回复。</summary>
		public string Reply;
	}
}
