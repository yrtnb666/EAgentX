using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using EAgentX.Core;
using EAgentX.Util;

namespace EAgentX.Ui
{
	/// <summary>
	/// 设置窗口。编辑器里改的是一份副本（S = settings.Clone()），点「取消」不会落盘。
	/// 两个页签：常规、高级（网页选择器）。
	/// 业务逻辑与原来完全一致，只重做了排版、控件外观、分区标题和间距。
	/// </summary>
	public class SettingsForm : Form
	{
		private readonly Settings S;

		private ModernSelect EngineBox;

		private TextBox WorkDirBox;

		private TextBox BrowserBox;

		private TextBox StartUrlBox;

		private TextBox ApiBaseBox;

		private TextBox ApiKeyBox;

		private TextBox ApiModelBox;

		private TextBox SysPromptBox;

		/// <summary>打开窗口时内置提示词的快照：用来判断用户到底有没有改过。</summary>
		private string BuiltInPrompt;

		private NumericUpDown PortBox;

		private NumericUpDown ReplyTimeoutBox;

		private NumericUpDown CommandTimeoutBox;

		private NumericUpDown MaxStepsBox;

		private NumericUpDown SendIntervalBox;

		private NumericUpDown MaxIntervalBox;

		private NumericUpDown RateLimitRetriesBox;

		private NumericUpDown RateLimitWaitBox;

		private CheckBox AutoApproveBox;

		private CheckBox EnableToolsBox;

		private CheckBox EmbedBrowserBox;

		private CheckBox StealthBox;

		private TextBox SelInputBox;

		private TextBox SelSendBox;

		private TextBox SelReplyBox;

		private TextBox SelGeneratingBox;

		private TextBox SelLoginBox;

		/// <summary>工作目录输入框的外框：校验失败时只把边框标红（纯视觉）。</summary>
		private ModernField WorkDirField;

		public Settings Result { get; private set; }

		public SettingsForm(Settings settings)
		{
			S = settings.Clone();
			Text = "EAgentX 设置";
			base.AutoScaleMode = AutoScaleMode.None;
			base.StartPosition = FormStartPosition.CenterParent;
			base.ClientSize = Ui.FitToScreen(780, 720, 0.86);
			Size minSize = Ui.FitToScreen(700, 600, 0.72);
			MinimumSize = new Size(Math.Min(minSize.Width, base.ClientSize.Width), Math.Min(minSize.Height, base.ClientSize.Height));
			Theme.StyleForm(this);
			base.ShowInTaskbar = false;
			base.ShowIcon = false;
			base.DoubleBuffered = true;
			BuildUi();
			LoadValues();
		}

		private void BuildUi()
		{
			// 自绘页签条 + 两个自绘滚动页（原生页签/滚动条在深色主题下会出现白底白条）
			ModernTabStrip tabStrip = new ModernTabStrip();
			tabStrip.Dock = DockStyle.Top;
			tabStrip.Titles.Add("常规");
			tabStrip.Titles.Add("高级");
			Theme.StyleSurface(tabStrip, ThemeRole.Panel);

			ModernScrollHost basicHost = new ModernScrollHost();
			basicHost.Dock = DockStyle.Fill;
			Theme.StyleSurface(basicHost, ThemeRole.Bg);

			ModernScrollHost selectorHost = new ModernScrollHost();
			selectorHost.Dock = DockStyle.Fill;
			Theme.StyleSurface(selectorHost, ThemeRole.Bg);
			selectorHost.Visible = false;

			tabStrip.SelectedIndexChanged += delegate
			{
				basicHost.Visible = (tabStrip.SelectedIndex == 0);
				selectorHost.Visible = (tabStrip.SelectedIndex == 1);
			};

			// ---------- 常规 ----------
			TableLayoutPanel basicTable = BuildTable();

			AddSectionRow(basicTable, "运行方式");
			EngineBox = new ModernSelect();
			EngineBox.Items.Add("DeepSeek 网页版（免费，需登录）");
			EngineBox.Items.Add("API 模式（OpenAI 兼容，需 Key）");
			Theme.StyleInput(EngineBox);
			AddRow(basicTable, "引擎", EngineBox, 36);
			Tip.Attach(EngineBox, "网页版复用浏览器登录态；API 模式需要填 Base 与 Key");

			WorkDirBox = new TextBox();
			Theme.StyleInput(WorkDirBox);
			AddRow(basicTable, "工作目录", WithBrowse(WorkDirBox, BrowseWorkDir, true), 36);
			Tip.Attach(WorkDirBox, "模型执行的命令都以这个目录为当前目录");

			BrowserBox = new TextBox();
			BrowserBox.Font = Theme.UiFontSmall;
			Theme.StyleInput(BrowserBox);
			AddRow(basicTable, "浏览器路径", WithBrowse(BrowserBox, BrowseBrowser, false), 36);
			Tip.Attach(BrowserBox, "留空自动探测 Edge / Chrome");

			PortBox = Spinner(1024, 65535, 1);
			AddRow(basicTable, "CDP 端口", PortBox, 36);

			StartUrlBox = new TextBox();
			Theme.StyleInput(StartUrlBox);
			AddRow(basicTable, "起始网址", StartUrlBox, 36);

			AddSectionRow(basicTable, "超时与节奏");
			ReplyTimeoutBox = Spinner(30, 3600, 10);
			AddRow(basicTable, "回复超时(秒)", ReplyTimeoutBox, 36);

			CommandTimeoutBox = Spinner(5, 3600, 5);
			AddRow(basicTable, "命令超时(秒)", CommandTimeoutBox, 36);

			MaxStepsBox = Spinner(1, 200, 1);
			AddRow(basicTable, "最大步数", MaxStepsBox, 36);

			SendIntervalBox = Spinner(0, 300, 1);
			AddRow(basicTable, "发送间隔(秒)", SendIntervalBox, 36);

			MaxIntervalBox = Spinner(0, 300, 1);
			AddRow(basicTable, "间隔上限(秒)", MaxIntervalBox, 36);

			Label intervalHint = Theme.Label("每一步发送前随机等待：上限大于下限时在两者之间取值；0 或小于下限表示固定间隔。", Theme.UiFontSmall, Theme.Dim);
			intervalHint.MaximumSize = Ui.Sz(520, 0);
			intervalHint.AutoSize = true;
			AddFullWidthRow(basicTable, intervalHint);

			RateLimitRetriesBox = Spinner(0, 10, 1);
			AddRow(basicTable, "限流重试次数", RateLimitRetriesBox, 36);

			RateLimitWaitBox = Spinner(10, 1800, 10);
			AddRow(basicTable, "限流等待(秒)", RateLimitWaitBox, 36);

			Label rateLimitHint = Theme.Label("被 DeepSeek 提示「发送过于频繁」时，程序会等这么多秒后自动重发；重试次数为 0 表示直接报错。等待时间每次翻倍。", Theme.UiFontSmall, Theme.Dim);
			rateLimitHint.MaximumSize = Ui.Sz(520, 0);
			rateLimitHint.AutoSize = true;
			AddFullWidthRow(basicTable, rateLimitHint);

			AddSectionRow(basicTable, "权限与浏览器");
			AutoApproveBox = new ModernCheckBox();
			AutoApproveBox.Text = "自动允许所有命令（危险：模型生成的命令会直接执行）";
			Theme.StyleCheck(AutoApproveBox);
			AutoApproveBox.AutoSize = true;
			AddRow(basicTable, "", AutoApproveBox, 32);

			EnableToolsBox = new ModernCheckBox();
			EnableToolsBox.Text = "允许助手使用截图、文件、鼠标和键盘";
			Theme.StyleCheck(EnableToolsBox);
			EnableToolsBox.AutoSize = true;
			AddRow(basicTable, "", EnableToolsBox, 32);

			EmbedBrowserBox = new ModernCheckBox();
			EmbedBrowserBox.Text = "连接后把浏览器嵌入主窗口";
			Theme.StyleCheck(EmbedBrowserBox);
			EmbedBrowserBox.AutoSize = true;
			AddRow(basicTable, "", EmbedBrowserBox, 32);

			StealthBox = new ModernCheckBox();
			StealthBox.Text = "使用兼容模式启动浏览器（隐藏自动化特征）";
			Theme.StyleCheck(StealthBox);
			StealthBox.AutoSize = true;
			AddRow(basicTable, "", StealthBox, 32);

			AddSectionRow(basicTable, "API 模式");
			ApiBaseBox = new TextBox();
			Theme.StyleInput(ApiBaseBox);
			AddRow(basicTable, "API Base", ApiBaseBox, 36);

			ApiKeyBox = new TextBox();
			ApiKeyBox.UseSystemPasswordChar = true;
			Theme.StyleInput(ApiKeyBox);
			AddRow(basicTable, "API Key", ApiKeyBox, 36);

			ApiModelBox = new TextBox();
			Theme.StyleInput(ApiModelBox);
			AddRow(basicTable, "模型名", ApiModelBox, 36);

			AddSectionRow(basicTable, "提示词");
			SysPromptBox = new TextBox();
			SysPromptBox.Multiline = true;
			SysPromptBox.ScrollBars = ScrollBars.Vertical;
			SysPromptBox.Font = Theme.UiFontSmall;
			Theme.StyleInput(SysPromptBox);
			AddRow(basicTable, "自定义提示词", SysPromptBox, 96);

			Label promptHint = Theme.Label("框里显示的是当前内置的 EAgent 提示词。不改就继续跟随内置版本（以后升级自动用新版）；改过则整段替换内置提示词。其中「工作目录 / 临时目录 / 技能目录」由程序按设置自动刷新。网页版不需要 API 配置。", Theme.UiFontSmall, Theme.Dim);
			promptHint.AutoSize = true;
			promptHint.MaximumSize = Ui.Sz(430, 0);
			promptHint.Margin = Ui.Pad(3, 2, 8, 0);

			Button restorePromptButton = new ModernButton();
			restorePromptButton.Text = "恢复内置提示词";
			restorePromptButton.Size = Ui.Sz(128, 30);
			restorePromptButton.Margin = Ui.Pad(0);
			Theme.StyleButton(restorePromptButton, accent: false);
			restorePromptButton.Click += delegate
			{
				SysPromptBox.Text = BuiltInPrompt;
			};

			FlowLayoutPanel promptActions = new FlowLayoutPanel();
			promptActions.AutoSize = true;
			promptActions.WrapContents = false;
			promptActions.Margin = Ui.Pad(3, 0, 3, 6);
			promptActions.Controls.Add(promptHint);
			promptActions.Controls.Add(restorePromptButton);
			AddFullWidthRow(basicTable, promptActions);

			basicHost.Content = basicTable;

			// ---------- 高级（网页选择器） ----------
			TableLayoutPanel selectorTable = BuildTable();

			Label selectorHint = Theme.Label("DeepSeek 页面改版导致「找不到输入框」时，在这里调整 CSS 选择器。多个选择器用英文逗号分隔。", Theme.UiFontSmall, Theme.Dim);
			selectorHint.MaximumSize = Ui.Sz(520, 0);
			selectorHint.AutoSize = true;
			AddFullWidthRow(selectorTable, selectorHint);

			SelInputBox = SelectorBox();
			AddRow(selectorTable, "输入框", SelInputBox, 36);
			SelSendBox = SelectorBox();
			AddRow(selectorTable, "发送按钮", SelSendBox, 36);
			SelReplyBox = SelectorBox();
			AddRow(selectorTable, "回复区域", SelReplyBox, 36);
			SelGeneratingBox = SelectorBox();
			AddRow(selectorTable, "生成中标志", SelGeneratingBox, 36);
			SelLoginBox = SelectorBox();
			AddRow(selectorTable, "登录标志", SelLoginBox, 36);

			selectorHost.Content = selectorTable;

			// ---------- 底部按钮栏 ----------
			Panel footer = new Panel();
			footer.Dock = DockStyle.Bottom;
			footer.Height = Ui.P(66);
			Theme.StyleSurface(footer, ThemeRole.Panel);
			footer.Paint += delegate(object sender, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Theme.Separator, 1f))
				{
					e.Graphics.DrawLine(pen, 0, 0, footer.Width, 0);
				}
			};

			Button cancelButton = new ModernButton();
			cancelButton.Text = "取消";
			cancelButton.Size = Ui.Sz(110, 36);
			cancelButton.Margin = Ui.Pad(8, 0, 0, 0);
			Theme.StyleButton(cancelButton, accent: false);
			cancelButton.DialogResult = DialogResult.Cancel;

			Button saveButton = new ModernButton();
			saveButton.Text = "保存";
			saveButton.Size = Ui.Sz(110, 36);
			saveButton.Margin = Ui.Pad(8, 0, 0, 0);
			Theme.StyleButton(saveButton, accent: true);
			saveButton.Click += delegate
			{
				Save();
			};

			FlowLayoutPanel actions = new FlowLayoutPanel();
			actions.Dock = DockStyle.Fill;
			actions.FlowDirection = FlowDirection.RightToLeft;
			actions.WrapContents = false;
			actions.AutoSize = true;
			actions.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			actions.Padding = Ui.Pad(0, 15, 16, 0);
			actions.Margin = Ui.Pad(0);
			Theme.StyleSurface(actions, ThemeRole.Panel);
			actions.Controls.Add(saveButton);
			actions.Controls.Add(cancelButton);

			Label configHint = Theme.Label("配置与主题目录：%APPDATA%\\EAgentX-UI", Theme.UiFontTiny, Theme.Dim);
			configHint.AutoSize = false;
			configHint.Dock = DockStyle.Fill;
			configHint.Margin = Ui.Pad(0);
			configHint.Padding = Ui.Pad(16, 0, 0, 0);
			configHint.TextAlign = ContentAlignment.MiddleLeft;
			configHint.AutoEllipsis = true;
			Tip.Attach(configHint, "配置：%APPDATA%\\EAgentX-UI\\config.json\r\n主题：%APPDATA%\\EAgentX-UI\\theme.json");

			TableLayoutPanel footerLayout = new TableLayoutPanel();
			footerLayout.Dock = DockStyle.Fill;
			footerLayout.ColumnCount = 2;
			footerLayout.RowCount = 1;
			footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			footerLayout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
			footerLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
			footerLayout.Controls.Add(configHint, 0, 0);
			footerLayout.Controls.Add(actions, 1, 0);
			footer.Controls.Add(footerLayout);

			base.Controls.Add(selectorHost);
			base.Controls.Add(basicHost);
			base.Controls.Add(tabStrip);
			base.Controls.Add(footer);
			base.CancelButton = cancelButton;
		}

		/// <summary>两个页签共用同一套表格骨架：左列标签固定宽，右列控件自适应。</summary>
		private static TableLayoutPanel BuildTable()
		{
			TableLayoutPanel table = new TableLayoutPanel();
			table.Dock = DockStyle.Top;
			table.AutoSize = true;
			table.AutoSizeMode = AutoSizeMode.GrowAndShrink;
			table.Padding = Ui.Pad(UiSpace.Lg, UiSpace.Lg, UiSpace.Lg, UiSpace.Lg);
			table.ColumnCount = 2;
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, Ui.P(150)));
			table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
			Theme.StyleSurface(table, ThemeRole.Bg);
			return table;
		}

		private static TextBox SelectorBox()
		{
			TextBox box = new TextBox();
			box.Font = Theme.MonoFont;
			Theme.StyleInput(box);
			return box;
		}

		private static NumericUpDown Spinner(int min, int max, int step)
		{
			NumericUpDown spinner = new NumericUpDown();
			spinner.Minimum = min;
			spinner.Maximum = max;
			spinner.Increment = step;
			Theme.StyleInput(spinner);
			spinner.BorderStyle = BorderStyle.FixedSingle;
			spinner.Font = Theme.UiFont;
			spinner.TextAlign = HorizontalAlignment.Left;
			return spinner;
		}

		/// <summary>文本框 + 右侧「浏览」按钮；文本框外面套一层圆角描边。</summary>
		private Panel WithBrowse(TextBox box, EventHandler onBrowse, bool captureField)
		{
			Panel wrapper = new Panel();
			wrapper.Height = Ui.P(36);
			wrapper.Dock = DockStyle.Fill;
			Theme.StyleSurface(wrapper, ThemeRole.Bg);
			ModernField field = new ModernField(box);
			field.Dock = DockStyle.Fill;
			field.Margin = Ui.Pad(0);
			Button browseButton = new ModernButton();
			browseButton.Text = "浏览";
			browseButton.Dock = DockStyle.Right;
			browseButton.Width = Ui.P(64);
			browseButton.Margin = Ui.Pad(0);
			Theme.StyleButton(browseButton, accent: false);
			browseButton.Click += onBrowse;
			wrapper.Controls.Add(field);
			wrapper.Controls.Add(browseButton);
			if (captureField)
			{
				// 工作目录的外框交给 Save() 做错误态标记（只改边框颜色）
				WorkDirField = field;
			}
			return wrapper;
		}

		/// <summary>加一行「标签 | 控件」；输入类控件自动套上圆角描边外框。</summary>
		private static Control AddRow(TableLayoutPanel table, string label, Control control, int height)
		{
			table.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.P(height + 10)));
			int row = table.RowCount++;
			if (label.Length > 0)
			{
				Label caption = Theme.Label(label, Theme.UiFont, Theme.Dim);
				caption.Margin = Ui.Pad(3, 9, 6, 0);
				table.Controls.Add(caption, 0, row);
			}
			Control placed = WrapField(control);
			if (control is NumericUpDown)
			{
				// 数字类输入框固定宽度，避免被拉成整行导致数字贴边
				placed.Dock = DockStyle.None;
				placed.Size = Ui.Sz(150, height);
				placed.Anchor = AnchorStyles.Top | AnchorStyles.Left;
				placed.Margin = Ui.Pad(3, 6, 3, 0);
			}
			else
			{
				placed.Dock = DockStyle.Fill;
				placed.Margin = Ui.Pad(3, 6, 3, 0);
			}
			table.Controls.Add(placed, 1, row);
			return placed;
		}

		/// <summary>输入类控件套 ModernField；下拉框自带边框，不重复包装。</summary>
		private static Control WrapField(Control control)
		{
			if (control == null)
			{
				return null;
			}
			if (control is ComboBox || control is Panel)
			{
				return control;
			}
			if (control is TextBox || control is NumericUpDown)
			{
				return new ModernField(control);
			}
			return control;
		}

		/// <summary>加一行分区标题（模块小标题，建立信息层级）。</summary>
		private static void AddSectionRow(TableLayoutPanel table, string title)
		{
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			int row = table.RowCount++;
			Label caption = Theme.Label(title, Theme.H2Font, Theme.Text);
			caption.Margin = Ui.Pad(3, 14, 6, 6);
			table.Controls.Add(caption, 0, row);
			table.Controls.Add(new Label(), 1, row);
		}

		/// <summary>加一行整行宽度的说明文字。</summary>
		private static void AddFullWidthRow(TableLayoutPanel table, Control control)
		{
			table.RowStyles.Add(new RowStyle(SizeType.AutoSize));
			table.RowCount++;
			table.Controls.Add(new Label(), 0, table.RowCount - 1);
			table.Controls.Add(control, 1, table.RowCount - 1);
		}

		private void LoadValues()
		{
			EngineBox.SelectedIndex = ((S.Engine == "api") ? 1 : 0);
			WorkDirBox.Text = S.WorkDir;
			BrowserBox.Text = S.EdgePath;
			PortBox.Value = S.CdpPort;
			StartUrlBox.Text = S.StartUrl;
			ReplyTimeoutBox.Value = S.ReplyTimeoutSec;
			CommandTimeoutBox.Value = S.CommandTimeoutSec;
			MaxStepsBox.Value = S.MaxSteps;
			SendIntervalBox.Value = S.SendIntervalSec;
			MaxIntervalBox.Value = S.SendIntervalMaxSec;
			RateLimitRetriesBox.Value = S.RateLimitRetries;
			RateLimitWaitBox.Value = S.RateLimitWaitSec;
			AutoApproveBox.Checked = S.AutoApprove;
			EnableToolsBox.Checked = S.EnableTools;
			EmbedBrowserBox.Checked = S.EmbedBrowser;
			StealthBox.Checked = S.StealthFlags;
			ApiBaseBox.Text = S.ApiBase;
			ApiKeyBox.Text = S.ApiKey;
			ApiModelBox.Text = S.ApiModel;
			BuiltInPrompt = PromptBuilder.BuiltIn(S);
			SysPromptBox.Text = (string.IsNullOrEmpty(S.SystemPrompt) ? BuiltInPrompt : PromptBuilder.RefreshRuntimeLines(S.SystemPrompt, S));
			SelInputBox.Text = S.Sel.Input;
			SelSendBox.Text = S.Sel.Send;
			SelReplyBox.Text = S.Sel.Reply;
			SelGeneratingBox.Text = S.Sel.Generating;
			SelLoginBox.Text = S.Sel.Login;
		}

		private void BrowseWorkDir(object sender, EventArgs e)
		{
			using (FolderBrowserDialog dialog = new FolderBrowserDialog())
			{
				dialog.Description = "选择智能体的工作目录";
				if (Directory.Exists(WorkDirBox.Text))
				{
					dialog.SelectedPath = WorkDirBox.Text;
				}
				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					WorkDirBox.Text = dialog.SelectedPath;
					if (WorkDirField != null)
					{
						WorkDirField.ShowError = false;
					}
				}
			}
		}

		private void BrowseBrowser(object sender, EventArgs e)
		{
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "选择 msedge.exe 或 chrome.exe";
				dialog.Filter = "浏览器|msedge.exe;chrome.exe|所有程序|*.exe";
				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					BrowserBox.Text = dialog.FileName;
				}
			}
		}

		private void Save()
		{
			string workDir = WorkDirBox.Text.Trim();
			if (workDir.Length == 0 || !Directory.Exists(workDir))
			{
				// 校验逻辑不变，只是把输入框边框标红，提示更直观
				if (WorkDirField != null)
				{
					WorkDirField.ShowError = true;
				}
				MessageDialog.Show(this, "工作目录不存在，请重新选择。", "EAgentX", MessageBoxIcon.Exclamation, MessageBoxButtons.OK);
				return;
			}
			if (WorkDirField != null)
			{
				WorkDirField.ShowError = false;
			}
			S.Engine = ((EngineBox.SelectedIndex == 1) ? "api" : "web");
			S.WorkDir = workDir;
			S.EdgePath = BrowserBox.Text.Trim();
			S.CdpPort = (int)PortBox.Value;
			S.StartUrl = StartUrlBox.Text.Trim();
			S.ReplyTimeoutSec = (int)ReplyTimeoutBox.Value;
			S.CommandTimeoutSec = (int)CommandTimeoutBox.Value;
			S.MaxSteps = (int)MaxStepsBox.Value;
			S.SendIntervalSec = (int)SendIntervalBox.Value;
			S.SendIntervalMaxSec = (int)MaxIntervalBox.Value;
			S.RateLimitRetries = (int)RateLimitRetriesBox.Value;
			S.RateLimitWaitSec = (int)RateLimitWaitBox.Value;
			S.AutoApprove = AutoApproveBox.Checked;
			S.EnableTools = EnableToolsBox.Checked;
			S.EmbedBrowser = EmbedBrowserBox.Checked;
			S.StealthFlags = StealthBox.Checked;
			S.ApiBase = ApiBaseBox.Text.Trim();
			S.ApiKey = ApiKeyBox.Text.Trim();
			S.ApiModel = ApiModelBox.Text.Trim();
			string promptText = SysPromptBox.Text.Trim();
			if (promptText.Length == 0 || promptText == BuiltInPrompt.Trim())
			{
				// 没改过内置提示词（或清空）→ 存空串，继续跟随内置版本，以后升级自动用新版
				promptText = "";
			}
			else
			{
				// 用户是在内置提示词基础上改的：目录与本机信息按当前机器刷新
				promptText = PromptBuilder.RefreshRuntimeLines(promptText, S);
			}
			S.SystemPrompt = promptText;
			S.Sel.Input = SelInputBox.Text.Trim();
			S.Sel.Send = SelSendBox.Text.Trim();
			S.Sel.Reply = SelReplyBox.Text.Trim();
			S.Sel.Generating = SelGeneratingBox.Text.Trim();
			S.Sel.Login = SelLoginBox.Text.Trim();
			S.Normalize();
			Result = S;
			base.DialogResult = DialogResult.OK;
		}
	}
}
