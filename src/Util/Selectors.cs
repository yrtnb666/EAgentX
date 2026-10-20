namespace EAgentX.Util
{
	/// <summary>
	/// 网页版页面选择器。DeepSeek 改版时只需要在设置里改这几项，不用重新编译。
	/// </summary>
	public class Selectors
	{
		/// <summary>输入框（textarea 或 contenteditable 的 div）。</summary>
		public string Input { get; set; }

		/// <summary>发送按钮。</summary>
		public string Send { get; set; }

		/// <summary>助手回复节点（markdown 容器）。</summary>
		public string Reply { get; set; }

		/// <summary>生成中的标志（停止按钮等）。</summary>
		public string Generating { get; set; }

		/// <summary>登录页标志，用来判断是否还没登录。</summary>
		public string Login { get; set; }

		public Selectors()
		{
			Input = "textarea#chat-input, textarea[placeholder], div[contenteditable='true']";
			Send = "button[type='submit'], div[role='button'][aria-label*='发送'], button[aria-label*='Send'], div[class*='send']";
			Reply = ".ds-markdown, .markdown, [class*='markdown']";
			Generating = "div[role='button'][aria-label*='停止'], button[aria-label*='Stop'], [class*='stop-btn']";
			Login = "input[type='password'], [class*='login']";
		}

		public Selectors Clone()
		{
			Selectors copy = new Selectors();
			copy.Input = Input;
			copy.Send = Send;
			copy.Reply = Reply;
			copy.Generating = Generating;
			copy.Login = Login;
			return copy;
		}
	}
}
