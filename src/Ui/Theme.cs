using System;
using System.Collections.Generic;
using System.Drawing;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace EAgentX.Ui
{
	/// <summary>
	/// 主题调色板（分区：配色定义）。
	/// 【改颜色只改这里】Light = 日间浅色，Dark = 夜间深色，两套各自独立、互不引用。
	/// 两套都避开纯白 #FFFFFF 与纯黑 #000000：浅色用冷灰底 + 米白卡片，深色用深灰蓝底 + 深灰卡片，
	/// 降低大面积高对比带来的视疲劳，适合长时间挂机查看。
	/// </summary>
	public sealed class ThemePalette
	{
		// ---------- 底层 / 容器 ----------
		public Color Bg;            // 页面底色
		public Color Panel;         // 卡片、工具栏、状态栏
		public Color PanelAlt;      // 次级面板、隔行底色
		public Color Input;         // 输入框/下拉框内部
		public Color Hover;         // 悬浮底色（按钮次要态、列表行）
		public Color Selected;      // 选中行 / 选中项高亮
		public Color Browser;       // 浏览器嵌入区底色

		// ---------- 线条 ----------
		public Color Border;        // 控件边框
		public Color Separator;     // 弱分割线（比边框更淡，尽量少用）

		// ---------- 文字 ----------
		public Color Text;          // 主文字
		public Color Dim;           // 次要文字
		public Color DisabledText;  // 禁用文字
		public Color OnAccent;      // 强调色底上的文字

		// ---------- 强调色 ----------
		public Color Accent;
		public Color AccentHover;
		public Color AccentPressed;
		public Color AccentSoft;    // 强调色浅底（标签、选中块）
		public Color Accent2;       // 顶部渐变副色

		// ---------- 语义色（提示条 / 日志） ----------
		public Color Success;
		public Color Warning;
		public Color Info;
		public Color Error;
		public Color User;
		public Color Agent;
		public Color Command;
		public Color Result;

		// ---------- 阴影 / 遮罩 / 滚动条 / 气泡 ----------
		public Color Overlay;       // 弹窗遮罩
		public Color Shadow;        // 浮层落影
		public Color ShadowSoft;    // 卡片落影（更淡）
		public Color ShadowStrong;  // 悬浮弹窗落影
		public Color ScrollTrack;   // 滚动条轨道
		public Color ScrollThumb;   // 滚动条滑块
		public Color TooltipBg;     // 悬浮提示底色
		public Color TooltipText;   // 悬浮提示文字

		public Color ButtonDisabled;

		/// <summary>日间浅色：冷灰底 + 近白卡片 + 靛蓝强调色。</summary>
		public static ThemePalette Light
		{
			get
			{
				ThemePalette p = new ThemePalette();
				p.Bg = Color.FromArgb(241, 243, 248);
				p.Panel = Color.FromArgb(251, 252, 254);
				p.PanelAlt = Color.FromArgb(238, 241, 247);
				p.Input = Color.FromArgb(246, 248, 252);
				p.Hover = Color.FromArgb(242, 243, 252);
				p.Selected = Color.FromArgb(230, 231, 250);
				p.Browser = Color.FromArgb(20, 22, 28);
				p.Border = Color.FromArgb(224, 228, 237);
				p.Separator = Color.FromArgb(234, 237, 243);
				p.Text = Color.FromArgb(34, 40, 52);
				p.Dim = Color.FromArgb(107, 116, 136);
				p.DisabledText = Color.FromArgb(166, 173, 188);
				p.OnAccent = Color.FromArgb(252, 252, 255);
				p.Accent = Color.FromArgb(91, 91, 214);
				p.AccentHover = Color.FromArgb(74, 74, 200);
				p.AccentPressed = Color.FromArgb(62, 62, 178);
				p.AccentSoft = Color.FromArgb(238, 238, 251);
				p.Accent2 = Color.FromArgb(139, 92, 246);
				p.Success = Color.FromArgb(35, 128, 103);
				p.Warning = Color.FromArgb(160, 111, 28);
				p.Info = Color.FromArgb(54, 103, 180);
				p.Error = Color.FromArgb(192, 57, 43);
				p.User = Color.FromArgb(37, 99, 235);
				p.Agent = Color.FromArgb(34, 40, 52);
				p.Command = Color.FromArgb(150, 88, 0);
				p.Result = Color.FromArgb(15, 118, 92);
				p.Overlay = Color.FromArgb(122, 22, 26, 40);
				p.Shadow = Color.FromArgb(30, 31, 35, 48);
				p.ShadowSoft = Color.FromArgb(20, 31, 35, 48);
				p.ShadowStrong = Color.FromArgb(52, 24, 28, 40);
				p.ScrollTrack = Color.FromArgb(241, 243, 248);
				p.ScrollThumb = Color.FromArgb(201, 207, 219);
				p.TooltipBg = Color.FromArgb(42, 47, 61);
				p.TooltipText = Color.FromArgb(246, 247, 251);
				p.ButtonDisabled = Color.FromArgb(238, 240, 245);
				return p;
			}
		}

		/// <summary>夜间深色：深灰蓝底 + 深灰卡片 + 提亮的靛蓝强调色。</summary>
		public static ThemePalette Dark
		{
			get
			{
				ThemePalette p = new ThemePalette();
				p.Bg = Color.FromArgb(22, 24, 30);
				p.Panel = Color.FromArgb(30, 33, 40);
				p.PanelAlt = Color.FromArgb(38, 42, 51);
				p.Input = Color.FromArgb(27, 30, 37);
				p.Hover = Color.FromArgb(38, 42, 52);
				p.Selected = Color.FromArgb(47, 52, 72);
				p.Browser = Color.FromArgb(14, 16, 22);
				p.Border = Color.FromArgb(51, 56, 70);
				p.Separator = Color.FromArgb(42, 46, 56);
				p.Text = Color.FromArgb(228, 231, 238);
				p.Dim = Color.FromArgb(154, 162, 179);
				p.DisabledText = Color.FromArgb(102, 110, 126);
				p.OnAccent = Color.FromArgb(248, 249, 255);
				p.Accent = Color.FromArgb(124, 140, 248);
				p.AccentHover = Color.FromArgb(150, 163, 255);
				p.AccentPressed = Color.FromArgb(108, 124, 230);
				p.AccentSoft = Color.FromArgb(43, 47, 69);
				p.Accent2 = Color.FromArgb(167, 139, 250);
				p.Success = Color.FromArgb(111, 211, 181);
				p.Warning = Color.FromArgb(224, 179, 106);
				p.Info = Color.FromArgb(143, 180, 255);
				p.Error = Color.FromArgb(240, 138, 130);
				p.User = Color.FromArgb(143, 180, 255);
				p.Agent = Color.FromArgb(228, 231, 238);
				p.Command = Color.FromArgb(224, 179, 106);
				p.Result = Color.FromArgb(111, 211, 181);
				p.Overlay = Color.FromArgb(184, 9, 11, 16);
				p.Shadow = Color.FromArgb(120, 6, 8, 12);
				p.ShadowSoft = Color.FromArgb(72, 6, 8, 12);
				p.ShadowStrong = Color.FromArgb(150, 4, 6, 10);
				p.ScrollTrack = Color.FromArgb(27, 30, 37);
				p.ScrollThumb = Color.FromArgb(69, 76, 92);
				p.TooltipBg = Color.FromArgb(51, 57, 72);
				p.TooltipText = Color.FromArgb(237, 239, 245);
				p.ButtonDisabled = Color.FromArgb(36, 40, 49);
				return p;
			}
		}

		/// <summary>两套调色板的字段集合（反射缓存，新增颜色字段时 Blend 自动覆盖，不会漏）。</summary>
		private static readonly FieldInfo[] ColorFields = typeof(ThemePalette).GetFields(BindingFlags.Public | BindingFlags.Instance);

		public Color Lerp(Color from, Color to, double amount)
		{
			if (amount <= 0.0)
			{
				return from;
			}
			if (amount >= 1.0)
			{
				return to;
			}
			int a = (int)Math.Round(from.A + (to.A - from.A) * amount);
			int r = (int)Math.Round(from.R + (to.R - from.R) * amount);
			int g = (int)Math.Round(from.G + (to.G - from.G) * amount);
			int b = (int)Math.Round(from.B + (to.B - from.B) * amount);
			return Color.FromArgb(Clamp(a), Clamp(r), Clamp(g), Clamp(b));
		}

		private static int Clamp(int value)
		{
			if (value < 0)
			{
				return 0;
			}
			if (value > 255)
			{
				return 255;
			}
			return value;
		}

		/// <summary>按比例混合两套调色板（主题切换动画的每一帧都用它算出中间色）。</summary>
		public ThemePalette Blend(ThemePalette to, double amount)
		{
			ThemePalette p = new ThemePalette();
			if (to == null)
			{
				return p;
			}
			for (int i = 0; i < ColorFields.Length; i++)
			{
				FieldInfo field = ColorFields[i];
				if (field.FieldType != typeof(Color))
				{
					continue;
				}
				Color from = (Color)field.GetValue(this);
				Color target = (Color)field.GetValue(to);
				field.SetValue(p, Lerp(from, target, amount));
			}
			return p;
		}
	}

	public enum ThemeMode
	{
		Light,
		Dark
	}

	/// <summary>控件的颜色角色：界面代码只声明"我是什么"，不写具体颜色。</summary>
	public enum ThemeRole
	{
		None,
		Bg,
		Panel,
		PanelAlt,
		Input,
		Border,
		TextPrimary,
		TextSecondary,
		Accent,
		AccentSoft,
		Error,
		Success,
		Warning,
		Info,
		User,
		Agent,
		Command,
		Result,
		Browser,
		OnAccent
	}

	/// <summary>
	/// 主题入口（分区：主题切换 / 控件换肤）。
	/// 业务代码继续用 Theme.Bg、Theme.Text 这样的名字，切换主题时自动取当前调色板；
	/// 界面代码禁止写死 Color.FromArgb(...)。
	/// </summary>
	public static class Theme
	{
		private sealed class RoleHolder
		{
			public ThemeRole Role;
		}

		private static readonly ConditionalWeakTable<Control, RoleHolder> Roles = new ConditionalWeakTable<Control, RoleHolder>();

		private static readonly ConditionalWeakTable<Control, RoleHolder> TextRoles = new ConditionalWeakTable<Control, RoleHolder>();

		private static ThemePalette current = ThemePalette.Light;

		private static ThemeMode mode = ThemeMode.Light;

		private static ThemePalette animationFrom;

		private static ThemePalette animationTo;

		private static DateTime animationStart;

		private static Timer animationTimer;

		/// <summary>切换过渡时长：280ms（要求 200~350ms），淡入淡出、不闪烁。</summary>
		private const int AnimationMilliseconds = 280;

		public static event EventHandler Changed;

		public static ThemePalette Current
		{
			get
			{
				return current;
			}
		}

		public static ThemeMode Mode
		{
			get
			{
				return mode;
			}
		}

		public static bool IsDark
		{
			get
			{
				return mode == ThemeMode.Dark;
			}
		}

		/// <summary>是否正在跑切换动画。动画期间跳过 DWM / 系统主题这类昂贵调用。</summary>
		public static bool Animating
		{
			get
			{
				return animationTimer != null;
			}
		}

		// ---------- 颜色快捷访问（只读） ----------
		public static Color Bg { get { return current.Bg; } }

		public static Color Panel { get { return current.Panel; } }

		public static Color PanelAlt { get { return current.PanelAlt; } }

		public static Color Input { get { return current.Input; } }

		public static Color Border { get { return current.Border; } }

		public static Color Separator { get { return current.Separator; } }

		public static Color Text { get { return current.Text; } }

		public static Color Dim { get { return current.Dim; } }

		public static Color DisabledText { get { return current.DisabledText; } }

		public static Color Accent { get { return current.Accent; } }

		public static Color AccentHover { get { return current.AccentHover; } }

		public static Color AccentPressed { get { return current.AccentPressed; } }

		public static Color AccentSoft { get { return current.AccentSoft; } }

		public static Color Accent2 { get { return current.Accent2; } }

		public static Color OnAccent { get { return current.OnAccent; } }

		public static Color ButtonDisabled { get { return current.ButtonDisabled; } }

		public static Color Hover { get { return current.Hover; } }

		public static Color Selected { get { return current.Selected; } }

		public static Color User { get { return current.User; } }

		public static Color Agent { get { return current.Agent; } }

		public static Color Command { get { return current.Command; } }

		public static Color Result { get { return current.Result; } }

		public static Color Success { get { return current.Success; } }

		public static Color Warning { get { return current.Warning; } }

		public static Color Info { get { return current.Info; } }

		public static Color Error { get { return current.Error; } }

		public static Color Overlay { get { return current.Overlay; } }

		public static Color Shadow { get { return current.Shadow; } }

		public static Color ShadowSoft { get { return current.ShadowSoft; } }

		public static Color ShadowStrong { get { return current.ShadowStrong; } }

		public static Color ScrollTrack { get { return current.ScrollTrack; } }

		public static Color ScrollThumb { get { return current.ScrollThumb; } }

		public static Color TooltipBg { get { return current.TooltipBg; } }

		public static Color TooltipText { get { return current.TooltipText; } }

		public static Color Browser { get { return current.Browser; } }

		// ---------- 字体层级 ----------
		public static readonly Font UiFont = UiFonts.Body;

		public static readonly Font UiFontSmall = UiFonts.Small;

		public static readonly Font UiFontTiny = UiFonts.Tiny;

		public static readonly Font InputFont = UiFonts.BodyLarge;

		public static readonly Font TitleFont = UiFonts.H1;

		public static readonly Font BannerFont = UiFonts.Banner;

		public static readonly Font H2Font = UiFonts.H2;

		public static readonly Font MonoFont = UiFonts.Mono;

		/// <summary>启动时读取本地主题；文件缺失或损坏一律回退到浅色日间主题。</summary>
		public static void Initialize()
		{
			mode = ThemeStore.Load();
			current = (mode == ThemeMode.Dark) ? ThemePalette.Dark : ThemePalette.Light;
		}

		/// <summary>点主题按钮时调用：切换配色（带动画）并立即写入本地配置。</summary>
		public static void Toggle()
		{
			SetMode((mode == ThemeMode.Light) ? ThemeMode.Dark : ThemeMode.Light, true);
		}

		public static void SetMode(ThemeMode targetMode, bool animate)
		{
			try
			{
				if (targetMode == mode && !IsAnimating())
				{
					return;
				}
				ThemePalette target = (targetMode == ThemeMode.Dark) ? ThemePalette.Dark : ThemePalette.Light;
				ThemePalette from = current;
				mode = targetMode;
				if (!animate || Application.OpenForms.Count == 0)
				{
					StopAnimation();
					current = target;
					ApplyOpenForms();
					RecolorOpenRichTexts(from, target);
					ThemeStore.Save(mode);
					RaiseChanged();
					return;
				}
				// 淡入淡出：280ms 内把两套调色板逐帧线性混合，所有控件同步变色
				StopAnimation();
				animationFrom = from;
				animationTo = target;
				animationStart = DateTime.UtcNow;
				animationTimer = new Timer();
				animationTimer.Interval = 15;
				animationTimer.Tick += delegate
				{
					double elapsed = (DateTime.UtcNow - animationStart).TotalMilliseconds;
					double amount = elapsed / (double)AnimationMilliseconds;
					if (amount >= 1.0)
					{
						StopAnimation();
						current = animationTo;
						ApplyOpenForms();
						RecolorOpenRichTexts(animationFrom, animationTo);
						ThemeStore.Save(mode);
						RaiseChanged();
						return;
					}
					current = animationFrom.Blend(animationTo, amount);
					ApplyOpenForms();
				};
				animationTimer.Start();
			}
			catch (Exception ex)
			{
				// 容错：任何异常都回退到浅色主题，绝不让主程序崩溃；同时记一条日志便于排查
				try
				{
					EAgentX.Util.Log.Error(ex);
				}
				catch
				{
				}
				StopAnimation();
				mode = ThemeMode.Light;
				current = ThemePalette.Light;
				ApplyOpenForms();
				try
				{
					ThemeStore.Save(mode);
				}
				catch
				{
				}
				RaiseChanged();
			}
		}

		private static bool IsAnimating()
		{
			return animationTimer != null;
		}

		private static void StopAnimation()
		{
			if (animationTimer != null)
			{
				animationTimer.Stop();
				animationTimer.Dispose();
				animationTimer = null;
			}
		}

		/// <summary>关闭软件 / 切换结束时保存主题到本地。</summary>
		public static void Save()
		{
			try
			{
				ThemeStore.Save(mode);
			}
			catch
			{
			}
		}

		private static void RaiseChanged()
		{
			EventHandler handler = Changed;
			if (handler != null)
			{
				handler(null, EventArgs.Empty);
			}
		}

		/// <summary>主题切换按钮上的文字（图标另画，见 ModernButton.Icon）。</summary>
		public static string ToggleText
		{
			get
			{
				return (mode == ThemeMode.Dark) ? "切换日间" : "切换夜间";
			}
		}

		public static ModernIcon ToggleIcon
		{
			get
			{
				return (mode == ThemeMode.Dark) ? ModernIcon.Sun : ModernIcon.Moon;
			}
		}

		// ---------- 角色登记 ----------
		public static void SetRole(Control control, ThemeRole role)
		{
			if (control == null)
			{
				return;
			}
			Roles.Remove(control);
			if (role != ThemeRole.None)
			{
				RoleHolder holder = new RoleHolder();
				holder.Role = role;
				Roles.Add(control, holder);
			}
		}

		public static ThemeRole GetRole(Control control)
		{
			RoleHolder holder;
			if (control != null && Roles.TryGetValue(control, out holder))
			{
				return holder.Role;
			}
			return ThemeRole.None;
		}

		public static void SetTextRole(Control control, ThemeRole role)
		{
			if (control == null)
			{
				return;
			}
			TextRoles.Remove(control);
			if (role != ThemeRole.None)
			{
				RoleHolder holder = new RoleHolder();
				holder.Role = role;
				TextRoles.Add(control, holder);
			}
		}

		public static ThemeRole GetTextRole(Control control)
		{
			RoleHolder holder;
			if (control != null && TextRoles.TryGetValue(control, out holder))
			{
				return holder.Role;
			}
			return ThemeRole.None;
		}

		// ---------- 样式入口 ----------
		public static void StyleForm(Form form)
		{
			if (form == null)
			{
				return;
			}
			form.BackColor = current.Bg;
			form.ForeColor = current.Text;
			form.Font = UiFont;
			// 窗口句柄建好后立即整窗换肤一次：保证启动时就是当前主题，
			// 同时把系统标题栏也切到对应明暗（深色主题下不再有亮白边框）
			form.HandleCreated += delegate
			{
				Apply(form);
			};
			// 部分系统要等窗口真正显示后才认 DWM 深色标题栏属性，这里再补一次
			form.Shown += delegate
			{
				WindowChrome.ApplyDarkTitleBar(form, IsDark);
			};
		}

		public static void StyleSurface(Control control, ThemeRole role)
		{
			if (control == null)
			{
				return;
			}
			SetRole(control, role);
			control.BackColor = ColorFor(role);
		}

		public static void SetForeColor(Control control, ThemeRole role)
		{
			if (control == null)
			{
				return;
			}
			SetTextRole(control, role);
			control.ForeColor = ColorFor(role);
		}

		/// <summary>输入类控件：底色走 Input，文字走正文色，下拉框改为扁平自绘。</summary>
		public static void StyleInput(Control control)
		{
			if (control == null)
			{
				return;
			}
			SetRole(control, ThemeRole.Input);
			SetTextRole(control, ThemeRole.TextPrimary);
			control.BackColor = current.Input;
			control.ForeColor = current.Text;
			control.Font = UiFont;
			TextBox textBox = control as TextBox;
			if (textBox != null)
			{
				textBox.BorderStyle = BorderStyle.None;
			}
			ComboBox combo = control as ComboBox;
			if (combo != null)
			{
				combo.FlatStyle = FlatStyle.Flat;
			}
			NativeScrollTheme.Apply(control, IsDark);
		}

		public static void StyleCheck(CheckBox check)
		{
			if (check == null)
			{
				return;
			}
			SetRole(check, ThemeRole.None);
			SetTextRole(check, ThemeRole.TextPrimary);
			check.ForeColor = current.Text;
			check.BackColor = Color.Transparent;
		}

		public static void StyleButton(Button button, bool accent)
		{
			StyledButton(button, accent ? ModernButtonKind.Primary : ModernButtonKind.Secondary);
		}

		public static void StyleDangerButton(Button button)
		{
			StyledButton(button, ModernButtonKind.Danger);
		}

		private static void StyledButton(Button button, ModernButtonKind kind)
		{
			if (button == null)
			{
				return;
			}
			ModernButton modern = button as ModernButton;
			if (modern != null)
			{
				modern.Kind = kind;
			}
			button.FlatStyle = FlatStyle.Flat;
			button.FlatAppearance.BorderSize = 0;
			button.Font = UiFont;
			button.Cursor = Cursors.Hand;
			button.UseVisualStyleBackColor = false;
			ApplyButton(button);
		}

		private static void ApplyButton(Button button)
		{
			ModernButton modern = button as ModernButton;
			if (modern == null)
			{
				button.BackColor = current.Accent;
				button.ForeColor = current.OnAccent;
				return;
			}
			modern.CornerRadius = UiRadius.Control;
			switch (modern.Kind)
			{
			case ModernButtonKind.Primary:
				modern.BackColor = current.Accent;
				modern.ForeColor = current.OnAccent;
				modern.Outline = false;
				modern.HoverColor = current.AccentHover;
				modern.PressColor = current.AccentPressed;
				modern.FlatAppearance.BorderColor = current.Accent;
				break;
			case ModernButtonKind.Danger:
				modern.BackColor = current.Panel;
				modern.ForeColor = current.Error;
				modern.Outline = true;
				modern.HoverColor = BlendTo(current.Panel, current.Error, 0.10);
				modern.PressColor = BlendTo(current.Panel, current.Error, 0.18);
				modern.FlatAppearance.BorderColor = BlendTo(current.Border, current.Error, 0.45);
				break;
			case ModernButtonKind.Ghost:
				modern.BackColor = Color.Transparent;
				modern.ForeColor = current.Dim;
				modern.Outline = false;
				modern.HoverColor = current.Hover;
				modern.PressColor = current.PanelAlt;
				modern.FlatAppearance.BorderColor = current.Border;
				break;
			default:
				modern.BackColor = current.Panel;
				modern.ForeColor = current.Text;
				modern.Outline = true;
				modern.HoverColor = current.Hover;
				modern.PressColor = current.PanelAlt;
				modern.FlatAppearance.BorderColor = current.Border;
				break;
			}
			modern.ApplyTheme();
		}

		/// <summary>按比例把两个颜色混合（用于 hover 淡淡的染色）。</summary>
		public static Color BlendTo(Color from, Color to, double amount)
		{
			return current.Lerp(from, to, amount);
		}

		/// <summary>创建跟随主题的标签（自动登记文字角色，切换主题时一起变色）。</summary>
		public static Label Label(string text, Font font, Color color)
		{
			Label label = new Label();
			label.Text = text;
			label.Font = font;
			label.ForeColor = color;
			label.BackColor = Color.Transparent;
			label.AutoSize = true;
			ThemeRole role = RoleForColor(color);
			if (role != ThemeRole.None)
			{
				SetTextRole(label, role);
			}
			return label;
		}

		private static ThemeRole RoleForColor(Color color)
		{
			if (color == current.Text || color == current.Agent)
			{
				return (color == current.Agent) ? ThemeRole.Agent : ThemeRole.TextPrimary;
			}
			if (color == current.Dim)
			{
				return ThemeRole.TextSecondary;
			}
			if (color == current.Accent)
			{
				return ThemeRole.Accent;
			}
			if (color == current.AccentSoft)
			{
				return ThemeRole.AccentSoft;
			}
			if (color == current.User)
			{
				return ThemeRole.User;
			}
			if (color == current.Command)
			{
				return ThemeRole.Command;
			}
			if (color == current.Result || color == current.Success)
			{
				return ThemeRole.Success;
			}
			if (color == current.Warning)
			{
				return ThemeRole.Warning;
			}
			if (color == current.Info)
			{
				return ThemeRole.Info;
			}
			if (color == current.Error)
			{
				return ThemeRole.Error;
			}
			if (color == current.OnAccent)
			{
				return ThemeRole.OnAccent;
			}
			return ThemeRole.None;
		}

		public static Color ColorFor(ThemeRole role)
		{
			switch (role)
			{
			case ThemeRole.Bg: return current.Bg;
			case ThemeRole.Panel: return current.Panel;
			case ThemeRole.PanelAlt: return current.PanelAlt;
			case ThemeRole.Input: return current.Input;
			case ThemeRole.Border: return current.Border;
			case ThemeRole.TextPrimary: return current.Text;
			case ThemeRole.TextSecondary: return current.Dim;
			case ThemeRole.Accent: return current.Accent;
			case ThemeRole.AccentSoft: return current.AccentSoft;
			case ThemeRole.Error: return current.Error;
			case ThemeRole.Success: return current.Success;
			case ThemeRole.Warning: return current.Warning;
			case ThemeRole.Info: return current.Info;
			case ThemeRole.User: return current.User;
			case ThemeRole.Agent: return current.Agent;
			case ThemeRole.Command: return current.Command;
			case ThemeRole.Result: return current.Result;
			case ThemeRole.Browser: return current.Browser;
			case ThemeRole.OnAccent: return current.OnAccent;
			default: return current.Text;
			}
		}

		/// <summary>递归刷新所有已创建控件：只改颜色、边框、滚动条主题和重绘，不触碰任何业务事件。</summary>
		public static void Apply(Control root)
		{
			if (root == null)
			{
				return;
			}
			ApplyOne(root);
			for (int i = 0; i < root.Controls.Count; i++)
			{
				Apply(root.Controls[i]);
			}
		}

		private static void ApplyOpenForms()
		{
			try
			{
				FormCollection forms = Application.OpenForms;
				for (int i = 0; i < forms.Count; i++)
				{
					Apply(forms[i]);
				}
			}
			catch
			{
			}
		}

		private static void ApplyOne(Control control)
		{
			Form form = control as Form;
			if (form != null)
			{
				form.BackColor = current.Bg;
				form.ForeColor = current.Text;
				// 标题栏也跟着一起变暗 / 变亮。
				// 注意：DWM 调用开销大，动画中间帧不调用，只在一头一尾设置，否则切换会掉帧
				if (!Animating)
				{
					WindowChrome.ApplyDarkTitleBar(form, mode == ThemeMode.Dark);
				}
			}
			ModernCard card = control as ModernCard;
			if (card != null)
			{
				ThemeRole cardRole = GetRole(card);
				card.CardColor = (cardRole == ThemeRole.None) ? current.Panel : ColorFor(cardRole);
				card.BackColor = ColorFor(GetRole(card) == ThemeRole.Bg ? ThemeRole.Bg : ThemeRole.Bg);
			}
			ModernButton modern = control as ModernButton;
			if (modern != null)
			{
				if (modern.IsThemeToggle)
				{
					modern.Text = ToggleText;
					modern.Icon = ToggleIcon;
				}
				ApplyButton(modern);
			}
			Label label = control as Label;
			if (label != null)
			{
				ThemeRole labelRole = GetTextRole(label);
				if (labelRole != ThemeRole.None)
				{
					label.ForeColor = ColorFor(labelRole);
				}
				label.BackColor = Color.Transparent;
			}
			TextBox textBox = control as TextBox;
			if (textBox != null)
			{
				ThemeRole inputRole = GetRole(textBox);
				ThemeRole textRole = GetTextRole(textBox);
				textBox.BackColor = (inputRole == ThemeRole.Panel) ? current.Panel : current.Input;
				textBox.ForeColor = (textRole == ThemeRole.None) ? current.Text : ColorFor(textRole);
				if (!Animating)
				{
					NativeScrollTheme.Apply(textBox, IsDark);
				}
			}
			RichTextBox rich = control as RichTextBox;
			if (rich != null)
			{
				ThemeRole richRole = GetRole(rich);
				ThemeRole richTextRole = GetTextRole(rich);
				rich.BackColor = (richRole == ThemeRole.Input) ? current.Input : current.Panel;
				rich.ForeColor = (richTextRole == ThemeRole.None) ? current.Text : ColorFor(richTextRole);
				if (!Animating)
				{
					NativeScrollTheme.Apply(rich, IsDark);
				}
			}
			ComboBox combo = control as ComboBox;
			if (combo != null)
			{
				ThemeRole comboTextRole = GetTextRole(combo);
				combo.BackColor = current.Input;
				combo.ForeColor = (comboTextRole == ThemeRole.None) ? current.Text : ColorFor(comboTextRole);
				combo.FlatStyle = FlatStyle.Flat;
				if (!Animating)
				{
					NativeScrollTheme.Apply(combo, IsDark);
				}
			}
			NumericUpDown numeric = control as NumericUpDown;
			if (numeric != null)
			{
				ThemeRole numericTextRole = GetTextRole(numeric);
				numeric.BackColor = current.Input;
				numeric.ForeColor = (numericTextRole == ThemeRole.None) ? current.Text : ColorFor(numericTextRole);
			}
			CheckBox check = control as CheckBox;
			if (check != null)
			{
				ThemeRole checkRole = GetTextRole(check);
				check.ForeColor = (checkRole == ThemeRole.None) ? current.Text : ColorFor(checkRole);
				check.BackColor = Color.Transparent;
			}
			TabControl tabs = control as TabControl;
			if (tabs != null)
			{
				tabs.BackColor = current.Panel;
				if (!Animating)
				{
					NativeScrollTheme.Apply(tabs, IsDark);
				}
			}
			TabPage page = control as TabPage;
			if (page != null)
			{
				page.BackColor = current.Bg;
				if (!Animating)
				{
					NativeScrollTheme.Apply(page, IsDark);
				}
			}
			Panel panel = control as Panel;
			if (panel != null && !(panel is ModernCard))
			{
				ThemeRole panelRole = GetRole(panel);
				if (panelRole != ThemeRole.None)
				{
					panel.BackColor = ColorFor(panelRole);
				}
			}
			// 自绘控件各自处理自己的配色（按钮/输入框容器/下拉框/复选框/提示气泡等）
			IThemedControl themed = control as IThemedControl;
			if (themed != null)
			{
				themed.ApplyTheme();
			}
			control.Invalidate();
		}

		/// <summary>RichTextBox 已有文字使用 RTF 颜色表；这里把旧表映射到新表，避免切换后旧文字仍发白/发黑。</summary>
		public static void RecolorRichText(RichTextBox box, ThemePalette from, ThemePalette to)
		{
			if (box == null || from == null || to == null || box.TextLength == 0)
			{
				return;
			}
			try
			{
				Dictionary<int, Color> map = BuildColorMap(from, to);
				string rtf = box.Rtf;
				string replaced = Regex.Replace(rtf, @"\\red(\d+)\\green(\d+)\\blue(\d+)", delegate(Match match)
				{
					int r = int.Parse(match.Groups[1].Value);
					int g = int.Parse(match.Groups[2].Value);
					int b = int.Parse(match.Groups[3].Value);
					Color color;
					if (map.TryGetValue((r << 16) | (g << 8) | b, out color))
					{
						return "\\red" + color.R + "\\green" + color.G + "\\blue" + color.B;
					}
					return match.Value;
				});
				if (!string.Equals(replaced, rtf, StringComparison.Ordinal))
				{
					int start = box.SelectionStart;
					int length = box.SelectionLength;
					box.Rtf = replaced;
					box.SelectionStart = Math.Min(start, box.TextLength);
					box.SelectionLength = Math.Min(length, box.TextLength - box.SelectionStart);
					box.ScrollToCaret();
				}
			}
			catch
			{
			}
		}

		private static void RecolorOpenRichTexts(ThemePalette from, ThemePalette to)
		{
			try
			{
				FormCollection forms = Application.OpenForms;
				for (int i = 0; i < forms.Count; i++)
				{
					RecolorRichTexts(forms[i], from, to);
				}
			}
			catch
			{
			}
		}

		private static void RecolorRichTexts(Control root, ThemePalette from, ThemePalette to)
		{
			RichTextBox box = root as RichTextBox;
			if (box != null)
			{
				RecolorRichText(box, from, to);
			}
			for (int i = 0; i < root.Controls.Count; i++)
			{
				RecolorRichTexts(root.Controls[i], from, to);
			}
		}

		private static Dictionary<int, Color> BuildColorMap(ThemePalette from, ThemePalette to)
		{
			Dictionary<int, Color> map = new Dictionary<int, Color>();
			AddColor(map, from.Text, to.Text);
			AddColor(map, from.Agent, to.Agent);
			AddColor(map, from.Dim, to.Dim);
			AddColor(map, from.Accent, to.Accent);
			AddColor(map, from.AccentSoft, to.AccentSoft);
			AddColor(map, from.User, to.User);
			AddColor(map, from.Command, to.Command);
			AddColor(map, from.Result, to.Result);
			AddColor(map, from.Success, to.Success);
			AddColor(map, from.Warning, to.Warning);
			AddColor(map, from.Info, to.Info);
			AddColor(map, from.Error, to.Error);
			AddColor(map, from.Panel, to.Panel);
			AddColor(map, from.Bg, to.Bg);
			AddColor(map, from.PanelAlt, to.PanelAlt);
			AddColor(map, from.Input, to.Input);
			AddColor(map, from.Selected, to.Selected);
			return map;
		}

		private static void AddColor(Dictionary<int, Color> map, Color from, Color to)
		{
			int key = (from.R << 16) | (from.G << 8) | from.B;
			if (!map.ContainsKey(key))
			{
				map.Add(key, to);
			}
		}
	}
}
