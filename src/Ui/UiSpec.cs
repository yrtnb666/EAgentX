using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EAgentX.Ui
{
	/// <summary>
	/// 全局 UI 规范（分区：设计规范 / 缩放）。
	/// 【唯一来源】界面上所有像素尺寸、间距、圆角、字号都必须经过这里换算，
	/// 这样 100% / 125% / 150% 缩放下布局等比放大，不会错位、不会文字溢出。
	/// 只有本文件允许出现"裸数字"，其余界面代码一律写 Ui.P(逻辑像素)。
	/// </summary>
	public static class Ui
	{
		private static readonly float uiScale;

		private static readonly Dictionary<string, Font> FontCache = new Dictionary<string, Font>();

		static Ui()
		{
			float value = 0f;
			// 开发期校验用：设置环境变量 EAGENTX_UI_SCALE=1.25 可强制按 125% 渲染（正常使用无需设置）
			try
			{
				string env = Environment.GetEnvironmentVariable("EAGENTX_UI_SCALE");
				float parsed = 0f;
				if (!string.IsNullOrEmpty(env) && float.TryParse(env, out parsed) && parsed >= 0.75f && parsed <= 4f)
				{
					value = parsed;
				}
			}
			catch
			{
			}
			if (value <= 0f)
			{
				try
				{
					using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
					{
						value = g.DpiX / 96f;
					}
				}
				catch
				{
					value = 1f;
				}
			}
			if (value < 0.75f || float.IsNaN(value))
			{
				value = 1f;
			}
			if (value > 6f)
			{
				value = 6f;
			}
			uiScale = value;
		}

		/// <summary>当前缩放比例（96 DPI = 1.0）。</summary>
		public static float Scale
		{
			get
			{
				return uiScale;
			}
		}

		/// <summary>逻辑像素 → 实际像素。</summary>
		public static int P(double logical)
		{
			return (int)Math.Round(logical * uiScale);
		}

		public static Point Pt(double x, double y)
		{
			return new Point(P(x), P(y));
		}

		public static Size Sz(double width, double height)
		{
			return new Size(Math.Max(1, P(width)), Math.Max(1, P(height)));
		}

		/// <summary>
		/// 设计尺寸 → 实际尺寸，并按屏幕可用工作区等比收缩。
		/// 150% 缩放 + 小屏笔记本时，设计尺寸会超出屏幕，这里保证窗口一定放得下。
		/// </summary>
		public static Size FitToScreen(double width, double height, double ratio)
		{
			Size design = Sz(width, height);
			try
			{
				Rectangle work = Screen.PrimaryScreen.WorkingArea;
				int maxWidth = (int)Math.Round(work.Width * ratio);
				int maxHeight = (int)Math.Round(work.Height * ratio);
				return new Size(Math.Min(design.Width, maxWidth), Math.Min(design.Height, maxHeight));
			}
			catch
			{
				return design;
			}
		}

		public static Padding Pad(double all)
		{
			return Pad(all, all, all, all);
		}

		public static Padding Pad(double horizontal, double vertical)
		{
			return Pad(horizontal, vertical, horizontal, vertical);
		}

		public static Padding Pad(double left, double top, double right, double bottom)
		{
			return new Padding(P(left), P(top), P(right), P(bottom));
		}

		/// <summary>按像素字号取字体（带缓存）。Windows 原生字体，避免装不上的第三方字体。</summary>
		public static Font Font(double pixelSize, FontStyle style)
		{
			return GetFont("Microsoft YaHei UI", pixelSize, style);
		}

		public static Font MonoFont(double pixelSize, FontStyle style)
		{
			return GetFont("Consolas", pixelSize, style);
		}

		private static Font GetFont(string family, double pixelSize, FontStyle style)
		{
			float size = (float)Math.Round(pixelSize * uiScale, 1);
			string key = family + "|" + size.ToString("0.0") + "|" + (int)style;
			Font font;
			if (FontCache.TryGetValue(key, out font))
			{
				return font;
			}
			try
			{
				font = new Font(family, size, style, GraphicsUnit.Pixel);
			}
			catch
			{
				font = new Font(FontFamily.GenericSansSerif, size, style, GraphicsUnit.Pixel);
			}
			FontCache[key] = font;
			return font;
		}

	/// <summary>
	/// 取控件所在"表面"的真实颜色。
	/// 自绘控件（按钮、复选框）设成 Transparent 后必须自己铺一层父容器底色，
	/// 否则 UserPaint + AllPaintingInWmPaint 会留下黑底。
	/// </summary>
	public static Color SurfaceOf(Control control)
	{
		Control parent = (control == null) ? null : control.Parent;
		while (parent != null)
		{
			ModernCard card = parent as ModernCard;
			if (card != null)
			{
				return card.CardColor;
			}
			if (parent.BackColor.A == 255)
			{
				return parent.BackColor;
			}
			parent = parent.Parent;
		}
		return Theme.Bg;
	}

	/// <summary>把父容器底色铺满控件客户区。</summary>
	public static void FillSurface(Control control, Graphics g)
	{
		if (control == null || g == null)
		{
			return;
		}
		using (SolidBrush brush = new SolidBrush(SurfaceOf(control)))
		{
			g.FillRectangle(brush, control.ClientRectangle);
		}
	}

	}

	/// <summary>间距系统：4 的倍数阶，所有外边距 / 内边距 / 控件间距都从这里取。</summary>
	public static class UiSpace
	{
		/// <summary>4 — 图标与文字之间。</summary>
		public static readonly int Xs = Ui.P(4);

		/// <summary>8 — 同类控件之间、按钮组之间。</summary>
		public static readonly int Sm = Ui.P(8);

		/// <summary>12 — 输入框内边距、行间距。</summary>
		public static readonly int Md = Ui.P(12);

		/// <summary>16 — 卡片内边距、区块内部留白。</summary>
		public static readonly int Lg = Ui.P(16);

		/// <summary>24 — 页面外边距、区块间隔。</summary>
		public static readonly int Xl = Ui.P(24);

		/// <summary>32 — 大区块之间的间隔。</summary>
		public static readonly int Xxl = Ui.P(32);

		/// <summary>40 — 页面顶部/底部大留白。</summary>
		public static readonly int Xxxl = Ui.P(40);
	}

	/// <summary>圆角规范：主容器、控件、小控件三档，全局不允许出现第四种圆角。</summary>
	public static class UiRadius
	{
		/// <summary>16 — 卡片、弹窗内容容器。</summary>
		public static readonly int Card = Ui.P(16);

		/// <summary>10 — 按钮、输入框、下拉框。</summary>
		public static readonly int Control = Ui.P(10);

		/// <summary>6 — 复选框、小标签、滚动条滑块。</summary>
		public static readonly int Small = Ui.P(6);
	}

	/// <summary>字体层级：大标题 / 模块小标题 / 正文 / 辅助说明 / 备注，字号字重固定。</summary>
	public static class UiFonts
	{
		/// <summary>页面大标题 27px 加粗。</summary>
		public static readonly Font H1 = Ui.Font(27.0, FontStyle.Bold);

		/// <summary>日志区开场大标题 21px 加粗。</summary>
		public static readonly Font Banner = Ui.Font(21.0, FontStyle.Bold);

		/// <summary>弹窗标题 19px 加粗。</summary>
		public static readonly Font DialogTitle = Ui.Font(19.0, FontStyle.Bold);

		/// <summary>模块小标题 15px 加粗。</summary>
		public static readonly Font H2 = Ui.Font(15.0, FontStyle.Bold);

		/// <summary>正文 13px 常规。</summary>
		public static readonly Font Body = Ui.Font(13.0, FontStyle.Regular);

		/// <summary>输入区正文 14px（比正文略大，长句更好读）。</summary>
		public static readonly Font BodyLarge = Ui.Font(14.0, FontStyle.Regular);

		/// <summary>辅助说明 12px 常规。</summary>
		public static readonly Font Small = Ui.Font(12.0, FontStyle.Regular);

		/// <summary>备注提示 11px 常规。</summary>
		public static readonly Font Tiny = Ui.Font(11.0, FontStyle.Regular);

		/// <summary>等宽字体 13px（命令与输出）。</summary>
		public static readonly Font Mono = Ui.MonoFont(13.0, FontStyle.Regular);
	}

	/// <summary>
	/// 阴影规范：低透明度、大扩散、柔和边缘。
	/// 分三层：底层背景（无影）/ 卡片（轻影）/ 悬浮弹窗（重影），深色主题下整体调低亮度。
	/// </summary>
	public static class UiShadow
	{
		/// <summary>卡片落影扩散半径。</summary>
		public static readonly int CardSpread = Ui.P(10);

		/// <summary>把柔和落影画在 bounds 外面（由父容器在卡片之前绘制，不会被卡片裁掉）。</summary>
		public static void Drop(Graphics g, Rectangle bounds, int radius, int spread, Color color)
		{
			if (g == null || spread <= 0 || bounds.Width <= Ui.P(8) || bounds.Height <= Ui.P(8) || color.A == 0)
			{
				return;
			}
			SmoothingMode oldMode = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			try
			{
				int offsetY = Math.Max(1, spread / 3);
				Rectangle baseRect = bounds;
				baseRect.Offset(0, offsetY);
				// 6 层足够柔和，层数越少每帧绘制越便宜（主题切换动画期间尤其明显）
				const int Steps = 6;
				for (int step = Steps; step >= 1; step--)
				{
					int i = Math.Max(1, spread * step / Steps);
					double falloff = 1.0 - (double)(i - 1) / (double)spread;
					int alpha = (int)Math.Round(color.A * falloff * falloff * 0.55);
					if (alpha <= 0)
					{
						continue;
					}
					Rectangle layer = baseRect;
					layer.Inflate(i, i);
					using (GraphicsPath path = ModernButton.RoundedPath(layer, radius + i / 2))
					{
						using (SolidBrush brush = new SolidBrush(Color.FromArgb(Math.Min(255, alpha), color)))
						{
							g.FillPath(brush, path);
						}
					}
				}
			}
			catch
			{
			}
			finally
			{
				g.SmoothingMode = oldMode;
			}
		}
	}

	/// <summary>
	/// 极简补间：把 0→1 的状态变化拉成 ~120ms 的平滑过渡，
	/// 用于 hover / press / 选中状态，避免颜色硬切造成的闪烁感。
	/// </summary>
	internal sealed class Tween : IDisposable
	{
		private Timer timer;

		private float current;

		private float target;

		/// <summary>值变化时回调（用来 Invalidate 重绘）。</summary>
		public Action Changed;

		/// <summary>当前插值（0~1）。</summary>
		public float Value
		{
			get
			{
				return current;
			}
		}

		public void To(float value)
		{
			if (value < 0f)
			{
				value = 0f;
			}
			if (value > 1f)
			{
				value = 1f;
			}
			if (Math.Abs(value - target) < 0.001f && timer != null)
			{
				return;
			}
			target = value;
			if (timer == null)
			{
				timer = new Timer();
				timer.Interval = 15;
				timer.Tick += OnTick;
				timer.Start();
			}
		}

		/// <summary>不做动画，直接落到目标值（主题切换、控件重建时用）。值没变就不重绘。</summary>
		public void Jump(float value)
		{
			float clamped = Math.Max(0f, Math.Min(1f, value));
			bool changed = Math.Abs(clamped - current) > 0.002f;
			Stop();
			current = clamped;
			target = clamped;
			if (changed)
			{
				Raise();
			}
		}

		private void OnTick(object sender, EventArgs e)
		{
			float diff = target - current;
			if (Math.Abs(diff) <= 0.02f)
			{
				current = target;
				Stop();
				Raise();
				return;
			}
			current += diff * 0.34f;
			Raise();
		}

		private void Raise()
		{
			Action handler = Changed;
			if (handler != null)
			{
				handler();
			}
		}

		private void Stop()
		{
			if (timer != null)
			{
				timer.Stop();
				timer.Tick -= OnTick;
				timer.Dispose();
				timer = null;
			}
		}

		public void Dispose()
		{
			Stop();
			Changed = null;
		}
	}

	/// <summary>线性简约图标集合（只画线条，颜色跟随主题文字色，缩放跟随 DPI）。</summary>
	public enum ModernIcon
	{
		None,
		Sun,
		Moon,
		Gear,
		Folder,
		Plus,
		Stop,
		ChevronDown,
		ArrowUp,
		ArrowDown,
		Check,
		Info,
		Close
	}

	/// <summary>图标绘制：统一线宽、统一端点，禁止使用彩色图片图标。</summary>
	public static class IconPainter
	{
		/// <summary>把图标画在 rect 内（正方形区域），stroke 为逻辑线宽。</summary>
		public static void Draw(Graphics g, Rectangle rect, ModernIcon icon, Color color, double stroke)
		{
			if (g == null || icon == ModernIcon.None || rect.Width <= 0 || rect.Height <= 0)
			{
				return;
			}
			SmoothingMode oldMode = g.SmoothingMode;
			g.SmoothingMode = SmoothingMode.AntiAlias;
			float width = (float)Math.Max(1.0, stroke * Ui.Scale);
			using (Pen pen = new Pen(color, width))
			{
				pen.StartCap = LineCap.Round;
				pen.EndCap = LineCap.Round;
				pen.LineJoin = LineJoin.Round;
				float cx = rect.Left + rect.Width / 2f;
				float cy = rect.Top + rect.Height / 2f;
				float r = Math.Min(rect.Width, rect.Height) / 2f - width / 2f - Ui.Scale;
				try
				{
					switch (icon)
					{
					case ModernIcon.Sun:
						g.DrawEllipse(pen, cx - r * 0.42f, cy - r * 0.42f, r * 0.84f, r * 0.84f);
						for (int i = 0; i < 8; i++)
						{
							double angle = Math.PI / 4.0 * i;
							float x1 = cx + (float)Math.Cos(angle) * r * 0.62f;
							float y1 = cy + (float)Math.Sin(angle) * r * 0.62f;
							float x2 = cx + (float)Math.Cos(angle) * r;
							float y2 = cy + (float)Math.Sin(angle) * r;
							g.DrawLine(pen, x1, y1, x2, y2);
						}
						break;
					case ModernIcon.Moon:
						// 两段圆弧拼出月牙：外弧 + 内弧，线宽一致
						using (GraphicsPath moon = new GraphicsPath())
						{
							RectangleF outer = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
							moon.AddArc(outer, 55f, 250f);
							RectangleF inner = new RectangleF(cx - r * 0.42f, cy - r * 1.05f, r * 1.75f, r * 1.75f);
							moon.AddArc(inner, 235f, -160f);
							g.DrawPath(pen, moon);
						}
						break;
					case ModernIcon.Gear:
						g.DrawEllipse(pen, cx - r * 0.34f, cy - r * 0.34f, r * 0.68f, r * 0.68f);
						for (int i = 0; i < 8; i++)
						{
							double angle = Math.PI / 4.0 * i + Math.PI / 8.0;
							float x1 = cx + (float)Math.Cos(angle) * r * 0.62f;
							float y1 = cy + (float)Math.Sin(angle) * r * 0.62f;
							float x2 = cx + (float)Math.Cos(angle) * r;
							float y2 = cy + (float)Math.Sin(angle) * r;
							g.DrawLine(pen, x1, y1, x2, y2);
						}
						break;
					case ModernIcon.Folder:
						using (GraphicsPath folder = new GraphicsPath())
						{
							float left = cx - r;
							float top = cy - r * 0.72f;
							float right = cx + r;
							float bottom = cy + r * 0.72f;
							float tab = r * 0.62f;
							folder.AddLines(new PointF[]
							{
								new PointF(left, bottom),
								new PointF(left, top),
								new PointF(cx - tab, top),
								new PointF(cx - tab + r * 0.22f, top + r * 0.26f),
								new PointF(right, top + r * 0.26f),
								new PointF(right, bottom)
							});
							folder.CloseFigure();
							g.DrawPath(pen, folder);
						}
						break;
					case ModernIcon.Plus:
						g.DrawLine(pen, cx - r * 0.7f, cy, cx + r * 0.7f, cy);
						g.DrawLine(pen, cx, cy - r * 0.7f, cx, cy + r * 0.7f);
						break;
					case ModernIcon.Stop:
						using (GraphicsPath stop = ModernButton.RoundedPath(new Rectangle((int)(cx - r * 0.6f), (int)(cy - r * 0.6f), (int)(r * 1.2f), (int)(r * 1.2f)), Ui.P(2)))
						{
							g.DrawPath(pen, stop);
						}
						break;
					case ModernIcon.ChevronDown:
						g.DrawLines(pen, new PointF[]
						{
							new PointF(cx - r * 0.48f, cy - r * 0.24f),
							new PointF(cx, cy + r * 0.28f),
							new PointF(cx + r * 0.48f, cy - r * 0.24f)
						});
						break;
					case ModernIcon.ArrowUp:
						g.DrawLines(pen, new PointF[]
						{
							new PointF(cx - r * 0.42f, cy + r * 0.22f),
							new PointF(cx, cy - r * 0.26f),
							new PointF(cx + r * 0.42f, cy + r * 0.22f)
						});
						break;
					case ModernIcon.ArrowDown:
						g.DrawLines(pen, new PointF[]
						{
							new PointF(cx - r * 0.42f, cy - r * 0.22f),
							new PointF(cx, cy + r * 0.26f),
							new PointF(cx + r * 0.42f, cy - r * 0.22f)
						});
						break;
					case ModernIcon.Check:
						g.DrawLines(pen, new PointF[]
						{
							new PointF(cx - r * 0.55f, cy + r * 0.02f),
							new PointF(cx - r * 0.12f, cy + r * 0.45f),
							new PointF(cx + r * 0.58f, cy - r * 0.42f)
						});
						break;
					case ModernIcon.Info:
						g.DrawEllipse(pen, cx - r * 0.92f, cy - r * 0.92f, r * 1.84f, r * 1.84f);
						g.DrawLine(pen, cx, cy - r * 0.34f, cx, cy + r * 0.44f);
						g.DrawLine(pen, cx, cy - r * 0.64f, cx, cy - r * 0.6f);
						break;
					case ModernIcon.Close:
						g.DrawLine(pen, cx - r * 0.55f, cy - r * 0.55f, cx + r * 0.55f, cy + r * 0.55f);
						g.DrawLine(pen, cx + r * 0.55f, cy - r * 0.55f, cx - r * 0.55f, cy + r * 0.55f);
						break;
					}
				}
				catch
				{
				}
			}
			g.SmoothingMode = oldMode;
		}
	}

	/// <summary>
	/// 窗口标题栏主题化：深色主题下把系统标题栏切成暗色，
	/// 否则窗口边框仍是亮白一条，和暗色界面不搭。老系统不支持时静默忽略。
	/// </summary>
	internal static class WindowChrome
	{
		[DllImport("dwmapi.dll")]
		private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

		/// <summary>DWMWA_USE_IMMERSIVE_DARK_MODE = 20（旧版本 SDK 用 19）。</summary>
		public static void ApplyDarkTitleBar(Form form, bool dark)
		{
			if (form == null || !form.IsHandleCreated)
			{
				return;
			}
			try
			{
				int value = dark ? 1 : 0;
				int result = DwmSetWindowAttribute(form.Handle, 20, ref value, 4);
				if (result != 0)
				{
					DwmSetWindowAttribute(form.Handle, 19, ref value, 4);
				}
			}
			catch
			{
			}
		}
	}

	/// <summary>
	/// 原生滚动条主题化：文本框 / 富文本框里的滚动条是系统绘制的，
	/// 这里用 SetWindowTheme 把深色主题下的滚动条切换成暗色版本（Win10 1809+ 有效）。
	/// 失败也不影响功能，只是滚动条保持系统默认外观。
	/// </summary>
	internal static class NativeScrollTheme
	{
		[DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
		private static extern int SetWindowTheme(IntPtr hWnd, string subAppName, string subIdList);

		/// <summary>取消该控件上的系统视觉样式：TabControl 用它去掉主题绘制的白色底带和边框。</summary>
		public static void UseClassicStyle(Control control)
		{
			if (control == null)
			{
				return;
			}
			try
			{
				if (!control.IsHandleCreated)
				{
					return;
				}
				SetWindowTheme(control.Handle, "", "");
			}
			catch
			{
			}
		}

		private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Control, object> Hooked = new System.Runtime.CompilerServices.ConditionalWeakTable<Control, object>();

		public static void Apply(Control control, bool dark)
		{
			if (control == null)
			{
				return;
			}
			try
			{
				if (control.IsHandleCreated)
				{
					SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
					return;
				}
				// 句柄还没创建（例如 TabPage）：等创建后再应用，只挂一次
				object existing;
				if (Hooked.TryGetValue(control, out existing))
				{
					return;
				}
				Hooked.Add(control, new object());
				control.HandleCreated += delegate
				{
					try
					{
						SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
					}
					catch
					{
					}
				};
			}
			catch
			{
			}
		}
	}
}
