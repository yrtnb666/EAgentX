using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using EAgentX.Core;
using EAgentX.Engines;
using EAgentX.Net;
using EAgentX.Util;

namespace EAgentX.Ui
{
	/// <summary>
	/// 主窗口：日志渲染 + 输入区 + 顶部工具栏 + 底部状态栏。
	/// 界面全部手写布局（没有 designer 文件），控件位置在 BuildUi / LayoutHeader / LayoutInputArea 里算。
	/// 本文件所有像素值都走 Ui.P(...) 换算，保证 100% / 125% / 150% 缩放下不错位。
	/// </summary>
	public class MainForm : Form
	{
		private Settings S;

		private AgentSession Session;

		private bool SessionStarted;

		private bool Busy;

		private bool Connecting;

		private CancellationTokenSource Cts;

		private RichTextBox LogBox;

		private Panel InputPanel;

		private TextBox InputBox;

		private Label InputPlaceholder;

		private Button BtnSend;

		private Button BtnAttach;

		private Button BtnStart;

		private Button BtnStop;

		private Button BtnDiag;

		private Button BtnDelay;

		private Button BtnEmbed;

		private Panel BrowserHost;

		private Panel BrowserPanel;

		private Splitter BrowserSplitter;

		/// <summary>浏览器窗口被重新 parent 后 Chromium 会异步重排，这里延时再校准一次尺寸。</summary>
		private System.Windows.Forms.Timer EmbedRelayoutTimer;

		/// <summary>WS_MAXIMIZE：最大化窗口 SetParent 后无法被 MoveWindow 缩小，会导致只显示一部分。</summary>
		private const int WS_MAXIMIZE = 0x01000000;

		private const int BrowserMinWidth = 320;

		private const int BrowserDefaultWidth = 560;

		private bool BrowserEmbedded;

		private IntPtr EmbeddedWindow = IntPtr.Zero;

		private int EmbeddedStyle;

		private Win32.RECT EmbeddedRect;

		private Button BtnSettings;

		private Button BtnWorkDir;

		private ModernButton BtnTheme;

		private CheckBox ChkAuto;

		private Label LblHint;

		private Label LblStatus;

		private Label LblEngine;

		public MainForm()
		{
			S = Settings.Load();
			S.Normalize();
			Text = "EAgentX · 桌面智能体";
			// 尺寸由 Ui 统一换算，关掉 WinForms 自带的字体缩放，避免两套缩放叠加
			base.AutoScaleMode = AutoScaleMode.None;
			BackColor = Theme.Bg;
			ForeColor = Theme.Text;
			Font = Theme.UiFont;
			base.DoubleBuffered = true;
			base.StartPosition = FormStartPosition.CenterScreen;
			// 设计尺寸 1180×780，屏幕放不下时按工作区等比收缩（150% 缩放的小屏也能完整显示）
			base.ClientSize = Ui.FitToScreen(1180, 780, 0.92);
			Size minSize = Ui.FitToScreen(980, 660, 0.80);
			MinimumSize = new Size(Math.Min(minSize.Width, base.ClientSize.Width), Math.Min(minSize.Height, base.ClientSize.Height));
			BuildUi();
			ToolRunner.HideSelf = delegate
			{
				SetSelfOpacity(0.02);
			};
			ToolRunner.ShowSelf = delegate
			{
				SetSelfOpacity(1.0);
			};
			Log.Line += OnLogLine;
			ChkAuto.Checked = S.AutoApprove;
			UpdateDelayButton();
			AppendBanner();
			UpdateUi();
			// 启动时整窗换肤一次（含标题栏明暗），之后切换主题走 Theme.Apply
			base.HandleCreated += delegate
			{
				Theme.Apply(this);
			};
			base.Shown += delegate
			{
				base.ActiveControl = null;
				UpdateInputPlaceholder();
			};
			base.Shown += delegate
			{
				// 部分系统要等窗口显示后才认 DWM 深色标题栏，这里补一次
				WindowChrome.ApplyDarkTitleBar(this, Theme.IsDark);
			};
			base.FormClosing += OnClosing;
		}

		private void BuildUi()
		{
			int pagePad = UiSpace.Xl;
			int cardPadX = Ui.P(20);
			int cardPadY = UiSpace.Lg;

			// ---------- 顶部标题栏 ----------
			ModernSurface headerBar = new ModernSurface();
			headerBar.Dock = DockStyle.Top;
			headerBar.Height = Ui.P(112);
			Theme.StyleSurface(headerBar, ThemeRole.Panel);
			headerBar.Paint += delegate(object sender, PaintEventArgs e)
			{
				// 弱分割线：只有 1px，且用比边框更淡的颜色
				using (Pen pen = new Pen(Theme.Separator, 1f))
				{
					e.Graphics.DrawLine(pen, 0, headerBar.Height - 1, headerBar.Width, headerBar.Height - 1);
				}
				// 品牌识别：左侧 5px 竖向渐变条
				using (LinearGradientBrush brush = new LinearGradientBrush(new Rectangle(0, 0, Ui.P(5), headerBar.Height), Theme.Accent, Theme.Accent2, 90f))
				{
					e.Graphics.FillRectangle(brush, 0, 0, Ui.P(5), headerBar.Height);
				}
			};

			Label titleLabel = Theme.Label("EAgentX", Theme.TitleFont, Theme.Text);
			titleLabel.Location = Ui.Pt(28, 14);
			headerBar.Controls.Add(titleLabel);

			Label subtitleLabel = Theme.Label("DeepSeek 桌面智能体", Theme.UiFontSmall, Theme.Dim);
			subtitleLabel.AutoSize = false;
			subtitleLabel.Size = Ui.Sz(460, 22);
			subtitleLabel.TextAlign = ContentAlignment.MiddleLeft;
			subtitleLabel.Location = Ui.Pt(30, 62);
			headerBar.Controls.Add(subtitleLabel);

			BtnSettings = new ModernButton();
			BtnSettings.Text = "设置";
			((ModernButton)BtnSettings).Icon = ModernIcon.Gear;
			BtnSettings.Size = Ui.Sz(94, 36);
			Theme.StyleButton(BtnSettings, accent: false);
			BtnSettings.Click += delegate
			{
				OpenSettings();
			};
			headerBar.Controls.Add(BtnSettings);

			BtnWorkDir = new ModernButton();
			BtnWorkDir.Text = "工作目录";
			((ModernButton)BtnWorkDir).Icon = ModernIcon.Folder;
			BtnWorkDir.Size = Ui.Sz(118, 36);
			Theme.StyleButton(BtnWorkDir, accent: false);
			BtnWorkDir.Click += delegate
			{
				PickWorkDir();
			};
			headerBar.Controls.Add(BtnWorkDir);

			// 主题切换：图标 + 文字，浅色显示「切换夜间」，深色显示「切换日间」
			BtnTheme = new ModernButton();
			BtnTheme.Text = Theme.ToggleText;
			BtnTheme.Icon = Theme.ToggleIcon;
			BtnTheme.Size = Ui.Sz(126, 36);
			BtnTheme.IsThemeToggle = true;
			Theme.StyleButton(BtnTheme, accent: false);
			BtnTheme.Click += delegate
			{
				// 只切换界面配色，并自动写入 theme.json；不触发任何业务动作
				Theme.Toggle();
			};
			headerBar.Controls.Add(BtnTheme);

			Tip.Attach(BtnTheme, "切换日间 / 夜间配色（自动记住上次选择）");
			Tip.Attach(BtnSettings, "打开设置");
			Tip.Attach(BtnWorkDir, "切换智能体的工作目录");

			// 标题栏右侧按钮用手写布局，避免缩放后 Anchor 把按钮推到窗口外面
			headerBar.Resize += delegate
			{
				LayoutHeader(headerBar);
			};
			LayoutHeader(headerBar);

			// ---------- 工具栏 ----------
			ModernSurface toolBar = new ModernSurface();
			toolBar.Dock = DockStyle.Top;
			toolBar.Height = Ui.P(64);
			Theme.StyleSurface(toolBar, ThemeRole.Bg);

			BtnStart = new ModernButton();
			BtnStart.Text = "新建会话";
			((ModernButton)BtnStart).Icon = ModernIcon.Plus;
			BtnStart.Size = Ui.Sz(132, 38);
			BtnStart.Location = Ui.Pt(24, 13);
			Theme.StyleButton(BtnStart, accent: true);
			BtnStart.Click += async delegate
			{
				await NewSessionAsync(true);
			};
			toolBar.Controls.Add(BtnStart);

			BtnDiag = new ModernButton();
			BtnDiag.Text = "页面状态";
			BtnDiag.Size = Ui.Sz(96, 38);
			BtnDiag.Location = Ui.Pt(168, 13);
			Theme.StyleButton(BtnDiag, accent: false);
			BtnDiag.Click += async delegate
			{
				await DiagnoseAsync();
			};
			toolBar.Controls.Add(BtnDiag);

			BtnStop = new ModernButton();
			BtnStop.Text = "停止";
			((ModernButton)BtnStop).Icon = ModernIcon.Stop;
			BtnStop.Size = Ui.Sz(92, 38);
			BtnStop.Location = Ui.Pt(276, 13);
			BtnStop.Enabled = false;
			Theme.StyleButton(BtnStop, accent: false);
			BtnStop.Click += delegate
			{
				StopWork();
			};
			toolBar.Controls.Add(BtnStop);

			BtnDelay = new ModernButton();
			BtnDelay.Size = Ui.Sz(112, 38);
			BtnDelay.Location = Ui.Pt(380, 13);
			Theme.StyleButton(BtnDelay, accent: false);
			BtnDelay.Click += delegate
			{
				EditSendDelay();
			};
			toolBar.Controls.Add(BtnDelay);

			BtnEmbed = new ModernButton();
			BtnEmbed.Text = "嵌入浏览器";
			BtnEmbed.Size = Ui.Sz(126, 38);
			BtnEmbed.Location = Ui.Pt(504, 13);
			Theme.StyleButton(BtnEmbed, accent: false);
			BtnEmbed.Click += delegate
			{
				ToggleEmbed();
			};
			toolBar.Controls.Add(BtnEmbed);

			Tip.Attach(BtnStart, "开启一个新对话");
			Tip.Attach(BtnDiag, "读取当前页面状态，排查「找不到输入框」");
			Tip.Attach(BtnStop, "停止当前任务");
			Tip.Attach(BtnDelay, "设置「回答结束 → 下一次发送」之间的等待时间");
			Tip.Attach(BtnEmbed, "把浏览器窗口嵌入主窗口左侧");

			LblStatus = Theme.Label("● 就绪", Theme.UiFontSmall, Theme.Dim);
			LblStatus.Location = Ui.Pt(650, 23);
			LblStatus.AutoSize = false;
			LblStatus.TextAlign = ContentAlignment.MiddleLeft;
			LblStatus.AutoEllipsis = true;
			LblStatus.Size = new Size(Math.Max(Ui.P(120), base.ClientSize.Width - Ui.P(674)), Ui.P(20));
			LblStatus.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
			toolBar.Controls.Add(LblStatus);
			Tip.Attach(LblStatus);

			// ---------- 底部输入区 ----------
			ModernSurface inputHost = new ModernSurface();
			inputHost.Dock = DockStyle.Bottom;
			inputHost.Height = Ui.P(218);
			Theme.StyleSurface(inputHost, ThemeRole.Bg);
			inputHost.Padding = Ui.Pad(pagePad, 0, pagePad, pagePad);
			inputHost.ShadowSpread = 10;

			InputPanel = new ModernCard();
			InputPanel.Dock = DockStyle.Fill;
			Theme.StyleSurface(InputPanel, ThemeRole.Panel);
			InputPanel.Padding = Ui.Pad(1);
			inputHost.ShadowTarget = InputPanel;

			InputBox = new TextBox();
			InputBox.Multiline = true;
			InputBox.ScrollBars = ScrollBars.None;
			InputBox.BorderStyle = BorderStyle.None;
			InputBox.Font = Theme.InputFont;
			Theme.StyleSurface(InputBox, ThemeRole.Panel);
			Theme.SetTextRole(InputBox, ThemeRole.TextPrimary);
			InputBox.ForeColor = Theme.Text;
			InputBox.KeyDown += OnInputKeyDown;
			InputPanel.Controls.Add(InputBox);

			InputPlaceholder = new Label();
			InputPlaceholder.Text = "在这里描述任务…";
			InputPlaceholder.Font = Theme.InputFont;
			InputPlaceholder.ForeColor = Theme.Dim;
			InputPlaceholder.BackColor = Color.Transparent;
			Theme.SetTextRole(InputPlaceholder, ThemeRole.TextSecondary);
			InputPlaceholder.AutoSize = false;
			InputPlaceholder.TextAlign = ContentAlignment.TopLeft;
			InputPlaceholder.Cursor = Cursors.IBeam;
			InputPlaceholder.MouseDown += delegate
			{
				InputBox.Focus();
			};
			InputBox.TextChanged += delegate
			{
				UpdateInputPlaceholder();
			};
			InputBox.Enter += delegate
			{
				UpdateInputPlaceholder();
			};
			InputBox.Leave += delegate
			{
				UpdateInputPlaceholder();
			};
			InputPanel.Controls.Add(InputPlaceholder);

			BtnSend = new ModernButton();
			BtnSend.Text = "发送";
			BtnSend.Size = Ui.Sz(112, 42);
			Theme.StyleButton(BtnSend, accent: true);
			BtnSend.Click += async delegate
			{
				await SendCurrentAsync();
			};
			InputPanel.Controls.Add(BtnSend);

			BtnAttach = new ModernButton();
			BtnAttach.Text = "添加附件";
			BtnAttach.Size = Ui.Sz(112, 36);
			Theme.StyleButton(BtnAttach, accent: false);
			BtnAttach.Click += async delegate
			{
				await AttachFilesAsync();
			};
			InputPanel.Controls.Add(BtnAttach);
			Tip.Attach(BtnSend, "发送任务（Enter）");
			Tip.Attach(BtnAttach, "把文件附给模型");

			LblHint = Theme.Label("Enter 发送  ·  Shift + Enter 换行  ·  默认需要确认命令", Theme.UiFontSmall, Theme.Dim);
			InputPanel.Controls.Add(LblHint);

			ChkAuto = new ModernCheckBox();
			ChkAuto.Text = "自动允许执行";
			Theme.StyleCheck(ChkAuto);
			Theme.SetForeColor(ChkAuto, ThemeRole.TextSecondary);
			// 关闭 AutoSize：自绘复选框自己量尺寸，避免宽度被算成 0 导致文字重叠
			ChkAuto.AutoSize = false;
			ChkAuto.Size = Ui.Sz(148, 26);
			ChkAuto.CheckedChanged += delegate
			{
				S.AutoApprove = ChkAuto.Checked;
				S.Save();
				if (ChkAuto.Checked)
				{
					AppendBlock(Theme.Error, Theme.UiFontSmall, "已开启自动允许：助手生成的命令会直接执行，请自行判断风险。");
				}
			};
			Tip.Attach(ChkAuto, "开启后模型生成的命令不再逐条确认（危险）");
			InputPanel.Controls.Add(ChkAuto);
			inputHost.Controls.Add(InputPanel);

			// ---------- 状态栏 ----------
			ModernSurface statusBar = new ModernSurface();
			statusBar.Dock = DockStyle.Bottom;
			statusBar.Height = Ui.P(32);
			Theme.StyleSurface(statusBar, ThemeRole.Panel);
			statusBar.Padding = Ui.Pad(26, 0, 26, 0);
			statusBar.Paint += delegate(object sender, PaintEventArgs e)
			{
				using (Pen pen = new Pen(Theme.Separator, 1f))
				{
					e.Graphics.DrawLine(pen, 0, 0, statusBar.Width, 0);
				}
			};

			LblEngine = Theme.Label("", Theme.UiFontTiny, Theme.Dim);
			LblEngine.Dock = DockStyle.Fill;
			LblEngine.AutoSize = false;
			LblEngine.TextAlign = ContentAlignment.MiddleLeft;
			LblEngine.AutoEllipsis = true;
			statusBar.Controls.Add(LblEngine);
			Tip.Attach(LblEngine);

			// ---------- 日志区 ----------
			ModernSurface logHost = new ModernSurface();
			logHost.Dock = DockStyle.Fill;
			logHost.Padding = Ui.Pad(pagePad, UiSpace.Lg, pagePad, UiSpace.Lg);
			Theme.StyleSurface(logHost, ThemeRole.Bg);

			ModernCard logCard = new ModernCard();
			logCard.Dock = DockStyle.Fill;
			logCard.BackColor = Theme.Bg;
			logCard.CardColor = Theme.Panel;
			logHost.ShadowTarget = logCard;
			logHost.ShadowSpread = 10;

			Panel logInner = new Panel();
			logInner.Dock = DockStyle.Fill;
			logInner.Padding = Ui.Pad(cardPadX, cardPadY, cardPadX, Ui.P(14));
			Theme.StyleSurface(logInner, ThemeRole.Panel);

			LogBox = new RichTextBox();
			LogBox.Dock = DockStyle.Fill;
			LogBox.BorderStyle = BorderStyle.None;
			Theme.StyleSurface(LogBox, ThemeRole.Panel);
			LogBox.ForeColor = Theme.Text;
			LogBox.Font = Theme.UiFont;
			LogBox.ReadOnly = true;
			LogBox.DetectUrls = false;
			LogBox.HideSelection = false;
			LogBox.ShortcutsEnabled = true;
			logInner.Controls.Add(LogBox);

			// 模块小标题：建立信息层级（对话记录是主区域，标题轻量、不抢注意力）
			Panel logHeader = new Panel();
			logHeader.Dock = DockStyle.Top;
			logHeader.Height = Ui.P(28);
			Theme.StyleSurface(logHeader, ThemeRole.Panel);
			Label logCaption = Theme.Label("对话记录", Theme.UiFontSmall, Theme.Dim);
			logCaption.Dock = DockStyle.Left;
			logCaption.AutoSize = false;
			logCaption.Width = Ui.P(160);
			logCaption.TextAlign = ContentAlignment.MiddleLeft;
			logHeader.Controls.Add(logCaption);
			logInner.Controls.Add(logHeader);

			logCard.Controls.Add(logInner);
			logHost.Controls.Add(logCard);

			base.Controls.Add(logHost);

			// ---------- 浏览器置入区（默认隐藏，点「嵌入浏览器」后显示在左侧） ----------
			// 外层容器负责宽度，右侧留一条可拖动的分隔条：嵌入后可以自己拉宽，避免页面被挤得显示不全
			BrowserHost = new ModernSurface();
			BrowserHost.Dock = DockStyle.Left;
			BrowserHost.Width = Ui.P(BrowserDefaultWidth);
			Theme.StyleSurface(BrowserHost, ThemeRole.Bg);
			BrowserHost.Visible = false;

			BrowserPanel = new ModernSurface();
			BrowserPanel.Dock = DockStyle.Fill;
			Theme.StyleSurface(BrowserPanel, ThemeRole.Browser);
			BrowserPanel.Resize += delegate
			{
				MoveEmbeddedWindow();
			};

			BrowserSplitter = new Splitter();
			BrowserSplitter.Dock = DockStyle.Right;
			BrowserSplitter.Width = Ui.P(6);
			BrowserSplitter.MinSize = Ui.P(BrowserMinWidth);
			BrowserSplitter.MinExtra = Ui.P(420);
			BrowserSplitter.BackColor = Theme.Bg;
			BrowserSplitter.Cursor = Cursors.SizeWE;
			BrowserSplitter.Visible = false;

			BrowserHost.Controls.Add(BrowserPanel);
			BrowserHost.Controls.Add(BrowserSplitter);
			base.Controls.Add(BrowserHost);

			base.Controls.Add(inputHost);
			base.Controls.Add(statusBar);
			base.Controls.Add(toolBar);
			base.Controls.Add(headerBar);

			InputPanel.Resize += delegate
			{
				LayoutInputArea();
			};
			base.Resize += delegate
			{
				LayoutInputArea();
			};
			LayoutInputArea();
			InputPlaceholder.BringToFront();
			UpdateInputPlaceholder();

			// 长日志行悬浮时显示完整内容（只读操作，不影响任何日志逻辑）
			Tip.AttachLineHover(LogBox, 40);
		}

		/// <summary>标题栏右侧按钮靠右依次排列（三个按钮：设置 / 工作目录 / 主题）。</summary>
		private void LayoutHeader(Panel headerBar)
		{
			if (headerBar == null || BtnSettings == null || BtnWorkDir == null || BtnTheme == null)
			{
				return;
			}
			int y = Ui.P(38);
			int gap = UiSpace.Sm;
			int right = headerBar.ClientSize.Width - UiSpace.Xl;
			BtnSettings.Location = new Point(right - BtnSettings.Width, y);
			right = BtnSettings.Left - gap;
			BtnWorkDir.Location = new Point(right - BtnWorkDir.Width, y);
			right = BtnWorkDir.Left - gap;
			BtnTheme.Location = new Point(right - BtnTheme.Width, y);
		}

		private void UpdateInputPlaceholder()
		{
			if (InputPlaceholder == null || InputBox == null)
			{
				return;
			}
			InputPlaceholder.Visible = (InputBox.TextLength == 0 && !InputBox.Focused);
		}

		/// <summary>输入区是手写布局：左边多行文本框，右侧上方附件、下方发送，底部一行提示。</summary>
		private void LayoutInputArea()
		{
			if (InputPanel != null)
			{
				int margin = Ui.P(18);
				int hintHeight = Ui.P(26);
				int buttonWidth = Ui.P(112);
				// 底部一行（提示 + 复选框）与卡片下边缘之间同样留 margin 的空白
				int rowTop = InputPanel.Height - margin - hintHeight;
				int boxHeight = rowTop - margin - Ui.P(6);
				if (boxHeight < Ui.P(40))
				{
					boxHeight = Ui.P(40);
				}
				if (InputBox != null)
				{
					InputBox.SetBounds(margin, margin, Math.Max(Ui.P(80), InputPanel.Width - margin * 2 - buttonWidth - Ui.P(14)), boxHeight);
				}
				if (InputPlaceholder != null && InputBox != null)
				{
					InputPlaceholder.SetBounds(InputBox.Left + Ui.P(2), InputBox.Top + Ui.P(2), Math.Max(Ui.P(10), InputBox.Width - Ui.P(6)), Ui.P(26));
				}
				if (BtnSend != null)
				{
					BtnSend.SetBounds(InputPanel.Width - margin - buttonWidth, InputPanel.Height - margin - BtnSend.Height, buttonWidth, BtnSend.Height);
				}
				if (BtnAttach != null)
				{
					BtnAttach.SetBounds(InputPanel.Width - margin - buttonWidth, margin, buttonWidth, BtnAttach.Height);
				}
				if (LblHint != null)
				{
					LblHint.Location = new Point(margin + Ui.P(2), rowTop + Ui.P(5));
				}
				if (ChkAuto != null)
				{
					ChkAuto.Location = new Point(InputPanel.Width - margin - buttonWidth - Ui.P(14) - ChkAuto.Width, rowTop + Ui.P(1));
				}
			}
		}

		/// <summary>截图时把主窗口临时调透明（ToolRunner 在后台线程调用，这里切回 UI 线程）。</summary>
		private void SetSelfOpacity(double value)
		{
			if (base.IsDisposed)
			{
				return;
			}
			if (base.InvokeRequired)
			{
				try
				{
					Invoke((Action<double>)SetSelfOpacity, value);
				}
				catch
				{
				}
				return;
			}
			try
			{
				Opacity = value;
				Application.DoEvents();
			}
			catch
			{
			}
		}

		/// <summary>把 Edge 窗口 SetParent 到左侧面板；再点一次恢复成独立窗口。</summary>
		private void ToggleEmbed()
		{
			if (BrowserEmbedded)
			{
				DetachBrowser();
				return;
			}
			WebEngine web = ((Session == null) ? null : (Session.Engine as WebEngine));
			if (web == null)
			{
				Log.Write(LogKind.Info, "只有网页版（Edge/Chrome）能嵌入浏览器；API 模式没有浏览器窗口。");
				return;
			}
			if (Busy || Connecting)
			{
				Log.Write(LogKind.Info, "正在执行任务，等这一步结束再嵌入浏览器。");
				return;
			}
			IntPtr hwnd = Win32.FindBrowserWindow(web.BrowserProcessId, "DeepSeek");
			if (hwnd == IntPtr.Zero)
			{
				Log.Write(LogKind.Error, "没找到浏览器窗口。请先点「新建会话」，等 DeepSeek 页面打开后再试。");
				return;
			}
			try
			{
				EmbeddedWindow = hwnd;
				EmbeddedStyle = Win32.GetWindowLong(hwnd, Win32.GWL_STYLE);
				Win32.GetWindowRect(hwnd, out EmbeddedRect);
				// 最大化 / 最小化的窗口 SetParent 之后无法被 MoveWindow 缩小，会只显示一部分，先还原
				if ((EmbeddedStyle & WS_MAXIMIZE) != 0 || Win32.IsIconic(hwnd))
				{
					Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
					Application.DoEvents();
				}
				BrowserHost.Visible = true;
				BrowserSplitter.Visible = true;
				Win32.SetParent(hwnd, BrowserPanel.Handle);
				int style = (EmbeddedStyle | Win32.WS_CHILD) & ~Win32.WS_POPUP & ~Win32.WS_CAPTION & ~Win32.WS_THICKFRAME & ~WS_MAXIMIZE;
				Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, style);
				MoveEmbeddedWindow();
				Win32.ShowWindow(hwnd, Win32.SW_SHOW);
				// 浏览器内核在换父窗口后会异步重排一次，稍后再校准尺寸，避免只显示左上角
				ScheduleEmbedRelayout();
				BrowserEmbedded = true;
				BtnEmbed.Text = "恢复窗口";
				Log.Write(LogKind.Info, "浏览器已嵌入主窗口。如果页面点击没反应，点「恢复窗口」改用独立窗口。");
			}
			catch (Exception ex)
			{
				Log.Error(ex);
				Log.Write(LogKind.Error, "嵌入浏览器失败: " + ex.Message);
				try
				{
					DetachBrowser();
				}
				catch
				{
				}
			}
		}

		private void DetachBrowser()
		{
			IntPtr hwnd = EmbeddedWindow;
			BrowserEmbedded = false;
			EmbeddedWindow = IntPtr.Zero;
			if (hwnd != IntPtr.Zero)
			{
				try
				{
					Win32.SetParent(hwnd, IntPtr.Zero);
					Win32.SetWindowLong(hwnd, Win32.GWL_STYLE, EmbeddedStyle);
					Win32.MoveWindow(hwnd, EmbeddedRect.Left, EmbeddedRect.Top, Math.Max(320, EmbeddedRect.Width), Math.Max(240, EmbeddedRect.Height), true);
					Win32.ShowWindow(hwnd, Win32.SW_SHOW);
				}
				catch
				{
				}
			}
			CancelEmbedRelayout();
			if (BrowserSplitter != null)
			{
				BrowserSplitter.Visible = false;
			}
			if (BrowserHost != null)
			{
				BrowserHost.Visible = false;
			}
			if (BtnEmbed != null)
			{
				BtnEmbed.Text = "嵌入浏览器";
			}
		}

		private void MoveEmbeddedWindow()
		{
			if (BrowserEmbedded && EmbeddedWindow != IntPtr.Zero && BrowserPanel != null)
			{
				try
				{
					int width = Math.Max(Ui.P(200), BrowserPanel.ClientSize.Width);
					int height = Math.Max(Ui.P(200), BrowserPanel.ClientSize.Height);
					Win32.MoveWindow(EmbeddedWindow, 0, 0, width, height, true);
				}
				catch
				{
				}
			}
		}

		/// <summary>延时再校准一次嵌入窗口尺寸（Chromium 换父窗口后会自己重排一次）。</summary>
		private void ScheduleEmbedRelayout()
		{
			try
			{
				CancelEmbedRelayout();
				EmbedRelayoutTimer = new System.Windows.Forms.Timer();
				EmbedRelayoutTimer.Interval = 260;
				EmbedRelayoutTimer.Tick += delegate
				{
					CancelEmbedRelayout();
					MoveEmbeddedWindow();
				};
				EmbedRelayoutTimer.Start();
			}
			catch
			{
				EmbedRelayoutTimer = null;
			}
		}

		private void CancelEmbedRelayout()
		{
			try
			{
				if (EmbedRelayoutTimer != null)
				{
					EmbedRelayoutTimer.Stop();
					EmbedRelayoutTimer.Dispose();
				}
			}
			catch
			{
			}
			EmbedRelayoutTimer = null;
		}

		private void StartEmbedTimer()
		{
			System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
			timer.Interval = 1500;
			timer.Tick += delegate
			{
				timer.Stop();
				timer.Dispose();
				if (base.IsDisposed)
				{
					return;
				}
				if (!Busy && !Connecting && !BrowserEmbedded)
				{
					ToggleEmbed();
				}
			};
			timer.Start();
		}

		/// <summary>把 Log 事件渲染成带颜色的一行（Result 由 AppendResult 单独着色）。</summary>
		private void OnLogLine(LogKind kind, string text)
		{
			if (!base.IsDisposed && kind != LogKind.Result)
			{
				Color color = Theme.Text;
				Font font = Theme.UiFont;
				string prefix = "";
				switch (kind)
				{
				case LogKind.User:
					color = Theme.User;
					prefix = "你\r\n";
					break;
				case LogKind.Agent:
					color = Theme.Agent;
					prefix = "助手\r\n";
					break;
				case LogKind.Command:
					color = Theme.Command;
					font = Theme.MonoFont;
					prefix = "执行命令\r\n";
					break;
				case LogKind.Info:
					color = Theme.Accent;
					font = Theme.UiFontSmall;
					prefix = "· ";
					break;
				case LogKind.Dim:
					color = Theme.Dim;
					font = Theme.UiFontSmall;
					break;
				case LogKind.Error:
					color = Theme.Error;
					prefix = "× ";
					break;
				}
				string body = ((kind == LogKind.Command) ? Indent(text) : text);
				AppendBlock(color, font, prefix + body);
			}
		}

		private void AppendBlock(Color color, Font font, string text)
		{
			if (base.IsDisposed)
			{
				return;
			}
			if (base.InvokeRequired)
			{
				try
				{
					BeginInvoke(new Action<Color, Font, string>(AppendBlock), color, font, text);
					return;
				}
				catch
				{
					return;
				}
			}
			if (text == null)
			{
				text = "";
			}
			LogBox.SelectionStart = LogBox.TextLength;
			LogBox.SelectionLength = 0;
			LogBox.SelectionColor = color;
			LogBox.SelectionFont = font;
			LogBox.AppendText(text + "\r\n\r\n");
			LogBox.SelectionStart = LogBox.TextLength;
			LogBox.ScrollToCaret();
			TrimLog();
		}

		/// <summary>命令执行结果：一行摘要 + 缩进的原始输出。</summary>
		private void AppendResult(RunResult r)
		{
			if (r == null || base.IsDisposed)
			{
				return;
			}
			if (base.InvokeRequired)
			{
				try
				{
					BeginInvoke(new Action<RunResult>(AppendResult), r);
					return;
				}
				catch
				{
					return;
				}
			}
			bool success = (r.ExitCode == 0 && !r.TimedOut);
			Color summaryColor = (success ? Theme.Result : Theme.Error);
			string summary = (success ? "完成" : "失败") + " · " + ((double)r.ElapsedMs / 1000.0).ToString("0.0") + " 秒" +
				(success ? "" : (" · 退出码 " + r.ExitCode)) + (r.TimedOut ? " · 已超时终止" : "");
			LogBox.SelectionStart = LogBox.TextLength;
			LogBox.SelectionColor = summaryColor;
			LogBox.SelectionFont = Theme.UiFontSmall;
			LogBox.AppendText(summary + "\r\n");
			LogBox.SelectionColor = Theme.Dim;
			LogBox.SelectionFont = Theme.MonoFont;
			LogBox.AppendText(Indent(r.Output) + "\r\n\r\n");
			LogBox.SelectionStart = LogBox.TextLength;
			LogBox.ScrollToCaret();
			TrimLog();
		}

		private void AppendBanner()
		{
			AppendBlock(Theme.Text, Theme.BannerFont, "开始一个新任务");
			AppendBlock(Theme.Dim, Theme.UiFontSmall, "描述你想让电脑完成的事，助手会先规划，再一步步执行。\r\n默认每条命令都会先请你确认；确认无误后再允许执行。");
		}

		/// <summary>日志太长时丢掉最前面的 15 万字符，避免 RichTextBox 越来越卡。</summary>
		private void TrimLog()
		{
			try
			{
				if (LogBox.TextLength > 400000)
				{
					LogBox.Select(0, 150000);
					LogBox.SelectedText = "";
					LogBox.SelectionStart = 0;
				}
			}
			catch
			{
			}
		}

		/// <summary>每行前加两个空格，命令和输出统一缩进。</summary>
		private static string Indent(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return "  ";
			}
			string[] lines = text.Replace("\r\n", "\n").Split('\n');
			StringBuilder sb = new StringBuilder();
			for (int i = 0; i < lines.Length; i++)
			{
				sb.Append("  ").Append(lines[i]);
				if (i < lines.Length - 1)
				{
					sb.Append("\r\n");
				}
			}
			return sb.ToString();
		}

		/// <summary>把会话事件接到界面：状态、用户消息、模型回复、命令、执行结果、审批。</summary>
		private void WireSession(AgentSession session)
		{
			session.StateChanged += OnStateChanged;
			session.UserMessage += delegate(string text)
			{
				Log.Write(LogKind.User, text);
			};
			session.AssistantMessage += delegate(string text)
			{
				Log.Write(LogKind.Agent, text);
			};
			session.CommandStarted += delegate(string command)
			{
				Log.Write(LogKind.Command, command);
			};
			session.CommandFinished += AppendResult;
			session.ApproveHandler = ApproveCommand;
		}

		private void OnStateChanged(string state)
		{
			if (base.IsDisposed)
			{
				return;
			}
			if (base.InvokeRequired)
			{
				try
				{
					BeginInvoke(new Action<string>(OnStateChanged), state);
					return;
				}
				catch
				{
					return;
				}
			}
			LblStatus.Text = "● " + state;
			if (state.IndexOf("错误", StringComparison.Ordinal) >= 0 || state.IndexOf("失败", StringComparison.Ordinal) >= 0)
			{
				Theme.SetForeColor(LblStatus, ThemeRole.Error);
			}
			else if (state.IndexOf("完成", StringComparison.Ordinal) >= 0)
			{
				Theme.SetForeColor(LblStatus, ThemeRole.Success);
			}
			else if (StateIsActive(state))
			{
				Theme.SetForeColor(LblStatus, ThemeRole.Accent);
			}
			else
			{
				Theme.SetForeColor(LblStatus, ThemeRole.TextSecondary);
			}
		}

		private static bool StateIsActive(string state)
		{
			if (string.IsNullOrEmpty(state))
			{
				return false;
			}
			return state.IndexOf("等待", StringComparison.Ordinal) >= 0 ||
				state.IndexOf("执行", StringComparison.Ordinal) >= 0 ||
				state.IndexOf("连接", StringComparison.Ordinal) >= 0 ||
				state.IndexOf("发送", StringComparison.Ordinal) >= 0;
		}

		/// <summary>按设置创建引擎并连接；已经连上就直接返回。</summary>
		private async Task<bool> EnsureSessionAsync()
		{
			if (Session != null && SessionStarted)
			{
				return true;
			}
			if (Connecting)
			{
				return false;
			}
			Connecting = true;
			UpdateUi();
			AgentSession session = null;
			try
			{
				try
				{
					S.Normalize();
					IChatEngine engine = ((S.Engine == "api") ? ((IChatEngine)new ApiEngine(S)) : ((IChatEngine)new WebEngine(S)));
					session = new AgentSession(S, engine);
					WireSession(session);
					Log.Dim("连接引擎: " + engine.Name);
					if (Cts == null || Cts.IsCancellationRequested)
					{
						Cts = new CancellationTokenSource();
					}
					string info = await Task.Run(() => session.StartAsync(Cts.Token));
					Session = session;
					SessionStarted = true;
					Log.Write(LogKind.Info, "已连接\n" + info);
					if (S.EmbedBrowser)
					{
						StartEmbedTimer();
					}
					return true;
				}
				catch (Exception ex)
				{
					Log.Error(ex);
					Log.Write(LogKind.Error, ex.Message);
					if (session != null)
					{
						try
						{
							session.Dispose();
						}
						catch
						{
						}
					}
					return false;
				}
			}
			finally
			{
				Connecting = false;
				UpdateUi();
			}
		}

		/// <summary>「新建会话」按钮：网页版会让浏览器回到起始页重新注入脚本。</summary>
		private async Task NewSessionAsync(bool clearLog)
		{
			if (Busy || Connecting)
			{
				Log.Write(LogKind.Info, "正在执行任务，先点「停止」再开新对话。");
				return;
			}
			if (clearLog)
			{
				LogBox.Clear();
			}
			AppendBanner();
			if (Session != null && SessionStarted)
			{
				WebEngine web = Session.Engine as WebEngine;
				if (web != null)
				{
					Connecting = true;
					UpdateUi();
					try
					{
						try
						{
							if (Cts == null || Cts.IsCancellationRequested)
							{
								Cts = new CancellationTokenSource();
							}
							await Task.Run(() => web.ResetAsync(Cts.Token));
							Log.Write(LogKind.Info, "已在浏览器里开启新对话。\n" + web.Diagnostics);
							return;
						}
						catch (Exception ex)
						{
							Log.Error(ex);
							Log.Write(LogKind.Error, "开启新对话失败: " + ex.Message);
							DisposeSession();
							return;
						}
					}
					finally
					{
						Connecting = false;
						UpdateUi();
					}
				}
				Log.Write(LogKind.Info, "API 模式已就绪，直接输入任务即可。");
			}
			else
			{
				DisposeSession();
				SessionStarted = false;
				if (await EnsureSessionAsync())
				{
					Log.Write(LogKind.Info, "把任务写在下面，Enter 发送。");
				}
			}
		}

		private async Task SendCurrentAsync()
		{
			if (Busy || Connecting)
			{
				return;
			}
			string task = InputBox.Text.Trim();
			if (task.Length == 0)
			{
				return;
			}
			InputBox.Clear();
			if (!(await EnsureSessionAsync()))
			{
				InputBox.Text = task;
				return;
			}
			Cts = new CancellationTokenSource();
			Busy = true;
			UpdateUi();
			try
			{
				try
				{
					await Task.Run(() => Session.RunTaskAsync(task, Cts.Token));
				}
				catch (OperationCanceledException)
				{
					Log.Write(LogKind.Dim, "已停止。");
				}
				catch (Exception ex)
				{
					Log.Error(ex);
					Log.Write(LogKind.Error, ex.Message);
				}
			}
			finally
			{
				Busy = false;
				UpdateUi();
			}
		}

		/// <summary>命令审批：在 UI 线程上弹 ApproveForm（Enter 允许 / Esc 拒绝）。</summary>
		private bool ApproveCommand(string command)
		{
			if (S.AutoApprove)
			{
				return true;
			}
			if (base.IsDisposed || !base.IsHandleCreated)
			{
				return false;
			}
			try
			{
				object result = Invoke((Func<bool>)delegate
				{
					DimLayer.ShowFor(this);
					try
					{
						using (ApproveForm dialog = new ApproveForm(command))
						{
							return dialog.ShowDialog(this) == DialogResult.OK;
						}
					}
					finally
					{
						DimLayer.HideLayer();
					}
				});
				return result is bool && (bool)result;
			}
			catch
			{
				DimLayer.HideLayer();
				return false;
			}
		}

		private void StopWork()
		{
			try
			{
				if (Cts != null)
				{
					Cts.Cancel();
				}
			}
			catch
			{
			}
			Log.Write(LogKind.Dim, "正在停止…");
		}

		private async Task DiagnoseAsync()
		{
			if (Busy || Connecting || ((Session == null || !SessionStarted) && !(await EnsureSessionAsync())))
			{
				return;
			}
			WebEngine web = Session.Engine as WebEngine;
			if (web == null)
			{
				Log.Write(LogKind.Info, "API 模式无需页面检测。当前引擎: " + Session.Engine.Name);
				return;
			}
			try
			{
				Log.Write(LogKind.Info, "页面状态\n" + await web.ReadDiagnosticsAsync());
			}
			catch (Exception ex)
			{
				Log.Error(ex);
				Log.Write(LogKind.Error, "检测失败: " + ex.Message);
			}
		}

		private void PickWorkDir()
		{
			using (FolderBrowserDialog dialog = new FolderBrowserDialog())
			{
				dialog.Description = "选择智能体的工作目录（命令将在这里执行）";
				if (Directory.Exists(S.WorkDir))
				{
					dialog.SelectedPath = S.WorkDir;
				}
				if (dialog.ShowDialog(this) == DialogResult.OK)
				{
					ApplyWorkDir(dialog.SelectedPath);
				}
			}
		}

		private void EditSendDelay()
		{
			DimLayer.ShowFor(this);
			try
			{
				using (DelayForm dialog = new DelayForm(S.SendIntervalSec, S.SendIntervalMaxSec))
				{
					if (dialog.ShowDialog(this) == DialogResult.OK)
					{
						S.SendIntervalSec = dialog.Seconds;
						S.SendIntervalMaxSec = dialog.MaxSeconds;
						S.Normalize();
						S.Save();
						UpdateDelayButton();
						UpdateUi();
						Log.Write(LogKind.Info, "发送间隔已设为 " + ((S.SendIntervalMaxSec > S.SendIntervalSec) ? (S.SendIntervalSec + "–" + S.SendIntervalMaxSec + " 秒（随机）") : (S.SendIntervalSec + " 秒")) + "。");
					}
				}
			}
			finally
			{
				DimLayer.HideLayer();
			}
		}

		private void UpdateDelayButton()
		{
			if (BtnDelay != null)
			{
				int max = S.SendIntervalMaxSec;
				BtnDelay.Text = ((max > S.SendIntervalSec) ? ("间隔 " + S.SendIntervalSec + "–" + max + "s") : ("间隔 " + S.SendIntervalSec + "s"));
			}
		}

		/// <summary>上传文件：网页版走原生附件，失败或 API 模式则复制到工作目录。</summary>
		private async Task AttachFilesAsync()
		{
			if (Busy || Connecting)
			{
				Log.Write(LogKind.Info, "正在执行任务，暂时不能上传文件。");
				return;
			}
			string[] paths;
			using (OpenFileDialog dialog = new OpenFileDialog())
			{
				dialog.Title = "选择要上传给智能体的文件";
				dialog.Multiselect = true;
				dialog.Filter = "常用文件|*.txt;*.md;*.pdf;*.doc;*.docx;*.ppt;*.pptx;*.xls;*.xlsx;*.csv;*.json;*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|代码文件|*.cs;*.js;*.ts;*.py;*.ps1;*.html;*.css;*.java;*.cpp;*.h;*.xml;*.yaml;*.yml|所有文件|*.*";
				if (dialog.ShowDialog(this) != DialogResult.OK)
				{
					return;
				}
				paths = dialog.FileNames;
			}
			if (paths == null || paths.Length == 0 || !(await EnsureSessionAsync()))
			{
				return;
			}
			List<string> attached = new List<string>();
			bool uploadedToPage = false;
			WebEngine web = Session.Engine as WebEngine;
			try
			{
				if (web != null)
				{
					Log.Write(LogKind.Info, await web.AttachFilesAsync(paths, CtsToken()));
					uploadedToPage = true;
					for (int i = 0; i < paths.Length; i++)
					{
						attached.Add(Path.GetFileName(paths[i]));
					}
				}
				else
				{
					attached = await Task.Run(() => CopyFilesToWorkDir(paths));
					Log.Write(LogKind.Info, "API 模式不支持网页附件，已复制 " + attached.Count + " 个文件到工作目录。");
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex);
				try
				{
					attached = CopyFilesToWorkDir(paths);
					Log.Write(LogKind.Info, "网页附件上传失败，已改为复制到工作目录：" + ex.Message);
				}
				catch (Exception copyError)
				{
					Log.Error(copyError);
					Log.Write(LogKind.Error, "上传失败：" + ex.Message);
					return;
				}
			}
			AppendAttachmentNote(attached, uploadedToPage);
		}

		/// <summary>把文件复制到 &lt;工作目录&gt;\.eagentx\uploads\时间戳\，重名自动加 -1/-2。</summary>
		private List<string> CopyFilesToWorkDir(string[] paths)
		{
			string uploadDir = Path.Combine(S.WorkDir, ".eagentx", "uploads", DateTime.Now.ToString("yyyyMMdd-HHmmss-fff"));
			Directory.CreateDirectory(uploadDir);
			List<string> copied = new List<string>();
			foreach (string source in paths)
			{
				if (File.Exists(source))
				{
					string fileName = Path.GetFileName(source);
					string target = Path.Combine(uploadDir, fileName);
					int suffix = 1;
					while (File.Exists(target))
					{
						target = Path.Combine(uploadDir, Path.GetFileNameWithoutExtension(fileName) + "-" + suffix + Path.GetExtension(fileName));
						suffix++;
					}
					File.Copy(source, target, true);
					copied.Add(target);
				}
			}
			return copied;
		}

		/// <summary>把附件名写进输入框，让模型知道要读哪些文件。</summary>
		private void AppendAttachmentNote(List<string> attached, bool uploadedToPage)
		{
			if (attached == null || attached.Count == 0)
			{
				return;
			}
			StringBuilder names = new StringBuilder();
			for (int i = 0; i < attached.Count; i++)
			{
				if (i > 0)
				{
					names.Append("、");
				}
				names.Append(attached[i]);
			}
			string lead = (uploadedToPage ? "已上传附件：" : "本地附件（已复制到工作目录）：");
			string tail = (uploadedToPage ? "。请阅读附件后继续。" : "。请用 PowerShell 读取这些文件后继续。");
			string note = string.Concat(lead, names, tail);
			if (InputBox.Text.Trim().Length > 0)
			{
				InputBox.Text = InputBox.Text.TrimEnd() + "\r\n\r\n" + note;
			}
			else
			{
				InputBox.Text = string.Concat("请阅读我上传的附件（", names, "），", uploadedToPage ? "然后按我的要求处理。" : "它们在工作目录里，然后按我的要求处理。");
			}
			InputBox.SelectionStart = InputBox.TextLength;
			InputBox.Focus();
		}

		private void ApplyWorkDir(string path)
		{
			if (Directory.Exists(path))
			{
				S.WorkDir = path;
				S.Save();
				DisposeSession();
				SessionStarted = false;
				UpdateUi();
				Log.Write(LogKind.Info, "工作目录已切换到 " + path + "（下次任务生效）");
			}
		}

		private void OpenSettings()
		{
			DimLayer.ShowFor(this);
			try
			{
				using (SettingsForm dialog = new SettingsForm(S))
				{
					if (dialog.ShowDialog(this) == DialogResult.OK)
					{
						S = dialog.Result;
						S.Save();
						DisposeSession();
						SessionStarted = false;
						ChkAuto.Checked = S.AutoApprove;
						UpdateDelayButton();
						UpdateUi();
						Log.Write(LogKind.Info, "设置已保存。");
					}
				}
			}
			finally
			{
				DimLayer.HideLayer();
			}
		}

		private void DisposeSession()
		{
			try
			{
				if (Cts != null)
				{
					Cts.Cancel();
				}
			}
			catch
			{
			}
			try
			{
				if (Session != null)
				{
					Session.Dispose();
				}
			}
			catch
			{
			}
			Session = null;
			SessionStarted = false;
		}

		private CancellationToken CtsToken()
		{
			// 点过「停止」之后再上传文件/重连，不能继续用已经取消的令牌
			if (Cts == null || Cts.IsCancellationRequested)
			{
				Cts = new CancellationTokenSource();
			}
			return Cts.Token;
		}

		private void UpdateUi()
		{
			if (base.IsDisposed)
			{
				return;
			}
			if (base.InvokeRequired)
			{
				try
				{
					BeginInvoke(new Action(UpdateUi));
					return;
				}
				catch
				{
					return;
				}
			}
			bool idle = !Busy && !Connecting;
			BtnStart.Enabled = idle;
			BtnSend.Enabled = idle;
			BtnAttach.Enabled = idle;
			BtnDiag.Enabled = idle;
			BtnDelay.Enabled = idle;
			BtnEmbed.Enabled = idle;
			BtnStop.Enabled = Busy;
			BtnSettings.Enabled = idle;
			BtnWorkDir.Enabled = idle;
			InputBox.Enabled = !Busy;
			string intervalText = ((S.SendIntervalMaxSec > S.SendIntervalSec) ? (S.SendIntervalSec + "–" + S.SendIntervalMaxSec + " 秒（随机）") : (S.SendIntervalSec + " 秒"));
			LblEngine.Text = ((S.Engine == "api") ? ("API · " + S.ApiModel) : "DeepSeek 网页版") + "    ·    间隔 " + intervalText + "    ·    " + S.WorkDir;
			if (Connecting)
			{
				LblStatus.Text = "● 正在连接（首次会启动浏览器，请稍候）…";
				Theme.SetForeColor(LblStatus, ThemeRole.Accent);
			}
			else if (!Busy && (LblStatus.Text == "" || LblStatus.Text.StartsWith("● 正在连接")))
			{
				LblStatus.Text = "● 就绪";
				Theme.SetForeColor(LblStatus, ThemeRole.TextSecondary);
			}
		}

		private async void OnInputKeyDown(object sender, KeyEventArgs e)
		{
			if (e.KeyCode == Keys.Return && !e.Shift && !e.Control)
			{
				e.SuppressKeyPress = true;
				e.Handled = true;
				await SendCurrentAsync();
			}
		}

		private void OnClosing(object sender, FormClosingEventArgs e)
		{
			if (Busy)
			{
				// 与原生 MessageBox 等价的确认，只是换成跟随主题的自绘弹窗
				DialogResult confirm = MessageDialog.Show(this, "任务还在执行中，确定退出吗？", "EAgentX", MessageBoxIcon.Question, MessageBoxButtons.YesNo);
				if (confirm != DialogResult.Yes)
				{
					e.Cancel = true;
					return;
				}
			}
			try
			{
				if (Cts != null)
				{
					Cts.Cancel();
				}
			}
			catch
			{
			}
			DetachBrowser();
			// 关闭时再次保存主题；即使切换动画还没结束也按目标主题保存
			Theme.Save();
			try
			{
				if (Session != null)
				{
					Session.Dispose();
				}
			}
			catch
			{
			}
			Log.Line -= OnLogLine;
		}
	}
}
