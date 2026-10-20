using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace EAgentX.Ui
{
	/// <summary>自绘控件统一接口：主题切换时由 Theme.Apply 逐个回调，控件自己重算配色。</summary>
	public interface IThemedControl
	{
		void ApplyTheme();
	}

	/// <summary>按钮类型：主操作 / 次要操作 / 危险操作 / 幽灵按钮（无边框，用于工具栏弱操作）。</summary>
	public enum ModernButtonKind
	{
		Primary,
		Secondary,
		Danger,
		Ghost
	}

	/// <summary>
	/// 圆角扁平按钮：四种状态（常态 / 悬浮 / 按下 / 禁用）都带 ~120ms 补间，
	/// 颜色全部来自 Theme 的角色色，切换主题时自动跟随。
	/// </summary>
	public class ModernButton : Button, IThemedControl
	{
		private readonly Tween hoverTween = new Tween();

		private readonly Tween pressTween = new Tween();

		private bool hovering;

		public int CornerRadius { get; set; }

		public bool Outline { get; set; }

		public Color HoverColor { get; set; }

		public Color PressColor { get; set; }

		/// <summary>主题切换按钮专用：切换时由 Theme 自动更新图标和文字。</summary>
		public bool IsThemeToggle { get; set; }

		/// <summary>按钮类型，决定主色 / 次要 / 危险 / 幽灵四种配色。</summary>
		public ModernButtonKind Kind { get; set; }

		/// <summary>左侧线性图标（与文字同色，随主题变色）。</summary>
		public ModernIcon Icon { get; set; }

		/// <summary>图标边长（逻辑像素）。</summary>
		public int IconSize { get; set; }

		public ModernButton()
		{
			Kind = ModernButtonKind.Secondary;
			CornerRadius = UiRadius.Control;
			IconSize = 15;
			Outline = true;
			HoverColor = Theme.Hover;
			PressColor = Theme.PanelAlt;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
				ControlStyles.SupportsTransparentBackColor, true);
			FlatStyle = FlatStyle.Flat;
			FlatAppearance.BorderSize = 0;
			UseVisualStyleBackColor = false;
			hoverTween.Changed = delegate { Invalidate(); };
			pressTween.Changed = delegate { Invalidate(); };
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = ClientRectangle;
			if (bounds.Width < 2 || bounds.Height < 2)
			{
				return;
			}
			// 先铺父容器底色：圆角之外就是父容器的颜色。
			// 这样控件是"矩形窗口 + 双缓冲"，不需要 Region 裁剪，鼠标悬停重绘时不会闪。
			Ui.FillSurface(this, e.Graphics);
			bounds.Width -= 1;
			bounds.Height -= 1;
			Color fill = Enabled ? BackColor : Theme.ButtonDisabled;
			if (Enabled)
			{
				if (hoverTween.Value > 0f)
				{
					fill = Theme.BlendTo(fill, HoverColor, hoverTween.Value * 0.92);
				}
				if (pressTween.Value > 0f)
				{
					fill = Theme.BlendTo(fill, PressColor, pressTween.Value);
				}
			}
			using (GraphicsPath path = RoundedPath(bounds, CornerRadius))
			{
				if (fill.A > 0)
				{
					using (SolidBrush brush = new SolidBrush(fill))
					{
						e.Graphics.FillPath(brush, path);
					}
				}
				if (Outline)
				{
					using (Pen pen = new Pen(Enabled ? FlatAppearance.BorderColor : Theme.Border, 1f))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
			}
			Color textColor = (Enabled ? ForeColor : Theme.DisabledText);
			if (Focused && ShowFocusCues)
			{
				Rectangle focus = bounds;
				focus.Inflate(-3, -3);
				using (GraphicsPath focusPath = RoundedPath(focus, Math.Max(2, CornerRadius - 3)))
				{
					using (Pen pen = new Pen(Color.FromArgb(110, Theme.Accent), 1.4f))
					{
						e.Graphics.DrawPath(pen, focusPath);
					}
				}
			}
			DrawContent(e.Graphics, bounds, textColor);
		}

		/// <summary>图标 + 文字整体居中排布，图标为矢量线条，跟随文字颜色。</summary>
		private void DrawContent(Graphics g, Rectangle bounds, Color textColor)
		{
			string caption = Text;
			int iconPx = (Icon == ModernIcon.None) ? 0 : Ui.P(IconSize);
			int gap = (iconPx > 0 && !string.IsNullOrEmpty(caption)) ? UiSpace.Xs + Ui.P(2) : 0;
			Size textSize = string.IsNullOrEmpty(caption) ? Size.Empty : TextRenderer.MeasureText(caption, Font);
			int total = iconPx + gap + textSize.Width;
			int left = bounds.Left + Math.Max(Ui.P(4), (bounds.Width - total) / 2);
			if (iconPx > 0)
			{
				Rectangle iconRect = new Rectangle(left, bounds.Top + (bounds.Height - iconPx) / 2, iconPx, iconPx);
				IconPainter.Draw(g, iconRect, Icon, textColor, 1.4);
				left += iconPx + gap;
			}
			Rectangle textRect = new Rectangle(left, bounds.Top, Math.Max(0, bounds.Right - left - Ui.P(4)), bounds.Height);
			TextRenderer.DrawText(g, caption, Font, textRect, textColor,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter |
				TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
		}

		public void ApplyTheme()
		{
			if (IsThemeToggle)
			{
				Text = Theme.ToggleText;
				Icon = Theme.ToggleIcon;
			}
			hoverTween.Jump(hovering ? 1f : 0f);
			pressTween.Jump(0f);
			// 不重建 Region：圆角只靠绘制，尺寸变化时不需要重设窗口形状
			Invalidate();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			hovering = true;
			if (Enabled)
			{
				hoverTween.To(1f);
			}
			Cursor = Cursors.Hand;
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			hovering = false;
			hoverTween.To(0f);
			pressTween.To(0f);
			base.OnMouseLeave(e);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			if (e.Button == MouseButtons.Left)
			{
				pressTween.To(1f);
				Invalidate();
			}
			base.OnMouseDown(e);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			pressTween.To(0f);
			base.OnMouseUp(e);
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			base.OnEnabledChanged(e);
			Cursor = Enabled ? Cursors.Hand : Cursors.Default;
			if (!Enabled)
			{
				hoverTween.Jump(0f);
				pressTween.Jump(0f);
			}
			Invalidate();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				hoverTween.Dispose();
				pressTween.Dispose();
			}
			base.Dispose(disposing);
		}

		internal static GraphicsPath RoundedPath(Rectangle bounds, int radius)
		{
			int value = Math.Max(0, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
			GraphicsPath path = new GraphicsPath();
			if (value == 0 || bounds.Width <= 0 || bounds.Height <= 0)
			{
				path.AddRectangle(bounds);
				return path;
			}
			int diameter = value * 2;
			path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180f, 90f);
			path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270f, 90f);
			path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0f, 90f);
			path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90f, 90f);
			path.CloseFigure();
			return path;
		}
	}

	/// <summary>
	/// 自绘页签条：替代原生 TabControl 的头部（原生页签会带系统白底和白色边框）。
	/// 只负责"画标签 + 报告选中项"，页面切换由调用方按 SelectedIndex 显隐。
	/// </summary>
	public class ModernTabStrip : Control, IThemedControl
	{
		private readonly List<string> titles = new List<string>();

		private int selectedIndex;

		private int hoverIndex = -1;

		public ModernTabStrip()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Font = Theme.UiFont;
			Cursor = Cursors.Hand;
			Height = Ui.P(46);
		}

		public List<string> Titles
		{
			get
			{
				return titles;
			}
		}

		public int SelectedIndex
		{
			get
			{
				return selectedIndex;
			}
			set
			{
				int clamped = (value < 0 || value >= titles.Count) ? 0 : value;
				if (clamped == selectedIndex)
				{
					return;
				}
				selectedIndex = clamped;
				Invalidate();
				EventHandler handler = SelectedIndexChanged;
				if (handler != null)
				{
					handler(this, EventArgs.Empty);
				}
			}
		}

		public event EventHandler SelectedIndexChanged;

		private int SegmentWidth
		{
			get
			{
				return Ui.P(124);
			}
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush brush = new SolidBrush(Theme.Panel))
			{
				e.Graphics.FillRectangle(brush, ClientRectangle);
			}
			// 与内容区之间的 1px 弱分割线（比边框更淡）
			using (Pen pen = new Pen(Theme.Separator, 1f))
			{
				e.Graphics.DrawLine(pen, 0, Height - 1, Width, Height - 1);
			}
			int x = Ui.P(16);
			for (int i = 0; i < titles.Count; i++)
			{
				Rectangle segment = new Rectangle(x, Ui.P(6), SegmentWidth, Height - Ui.P(10));
				bool selected = (i == selectedIndex);
				if (selected)
				{
					using (GraphicsPath path = ModernButton.RoundedPath(segment, UiRadius.Control))
					{
						using (SolidBrush brush = new SolidBrush(Theme.AccentSoft))
						{
							e.Graphics.FillPath(brush, path);
						}
					}
				}
				else if (i == hoverIndex)
				{
					using (GraphicsPath path = ModernButton.RoundedPath(segment, UiRadius.Control))
					{
						using (SolidBrush brush = new SolidBrush(Theme.Hover))
						{
							e.Graphics.FillPath(brush, path);
						}
					}
				}
				TextRenderer.DrawText(e.Graphics, titles[i], Font, segment,
					selected ? Theme.Accent : Theme.Dim,
					TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
				if (selected)
				{
					Rectangle underline = new Rectangle(segment.Left + Ui.P(24), segment.Bottom - Ui.P(3), segment.Width - Ui.P(48), Ui.P(2));
					using (SolidBrush brush = new SolidBrush(Theme.Accent))
					{
						e.Graphics.FillRectangle(brush, underline);
					}
				}
				x += SegmentWidth + Ui.P(4);
			}
		}

		private int IndexAt(int x)
		{
			int start = Ui.P(16);
			int step = SegmentWidth + Ui.P(4);
			if (x < start)
			{
				return -1;
			}
			int index = (x - start) / step;
			return (index >= 0 && index < titles.Count) ? index : -1;
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			int index = IndexAt(e.X);
			if (index != hoverIndex)
			{
				hoverIndex = index;
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			hoverIndex = -1;
			Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			int index = IndexAt(e.X);
			if (index >= 0)
			{
				SelectedIndex = index;
			}
		}

		public void ApplyTheme()
		{
			Invalidate();
		}
	}

	/// <summary>
	/// 自绘滚动容器：内容超高时右侧出现一条跟随主题的细滚动条（替代原生灰白滚动条）。
	/// 性能要点：滚动只移动内部视口的 Top，不重新设置整块尺寸——
	/// 这样 TableLayoutPanel 不会在每次滚轮时重排几十个子控件，滚动才不卡。
	/// </summary>
	public class ModernScrollHost : Panel, IThemedControl
	{
		private readonly Panel viewport;

		private Control content;

		private int scrollValue;

		private int maximum;

		private bool dragging;

		private int dragOffset;

		/// <summary>更新过程中的重入保护：避免 SetBounds → SizeChanged → 再 SetBounds 的循环。</summary>
		private bool adjusting;

		public ModernScrollHost()
		{
			DoubleBuffered = true;
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
			viewport = new ScrollViewport();
			viewport.BackColor = Theme.Bg;
			// 位置由滚动逻辑控制；锚定只能给 Top|Left（Anchor=None 会被布局引擎居中）
			viewport.Dock = DockStyle.None;
			viewport.Anchor = AnchorStyles.Top | AnchorStyles.Left;
			Controls.Add(viewport);
		}

		/// <summary>被滚动的宿主控件（通常 Dock=Top、AutoSize 的 TableLayoutPanel）。</summary>
		public Control Content
		{
			get
			{
				return content;
			}
			set
			{
				if (content != null)
				{
					content.SizeChanged -= OnContentSizeChanged;
				}
				content = value;
				viewport.SuspendLayout();
				try
				{
					viewport.Controls.Clear();
					if (content != null)
					{
						// 内容固定在视口左上角：滚动时移动的是"视口"这个窗口，
						// 内容的 Bounds 完全不变，所以 AutoSize 的 TableLayoutPanel 不会重新测量
						content.Dock = DockStyle.None;
						content.Anchor = AnchorStyles.Top | AnchorStyles.Left;
						// 关键：关掉 AutoSize。AutoSize 容器在任何 bounds 通知（包括窗口移动）时
						// 都会重新测量自己，几十个控件的表格一次要 20ms 以上，滚动必然卡。
						// 高度改由 MeasureContent 在宽度变化时主动算一次。
						content.AutoSize = false;
						content.Location = new Point(0, 0);
						viewport.Controls.Add(content);
						content.SizeChanged += OnContentSizeChanged;
					}
				}
				finally
				{
					viewport.ResumeLayout(true);
				}
				scrollValue = 0;
				SizeChanged -= OnHostSizeChanged;
				SizeChanged += OnHostSizeChanged;
				UpdateScroll();
			}
		}

		private void OnContentSizeChanged(object sender, EventArgs e)
		{
			// 内容高度变化（例如切换页签、控件换行）才重算滚动范围
			if (adjusting)
			{
				return;
			}
			AdjustRange();
			Invalidate();
		}

		private void OnHostSizeChanged(object sender, EventArgs e)
		{
			UpdateScroll();
		}

		private int GutterWidth
		{
			get
			{
				return Ui.P(12);
			}
		}

		/// <summary>内容实际高度：AutoSize 的表格自己算好，直接用 Height 最省。</summary>
		private int ContentHeight
		{
			get
			{
				return (content == null) ? 0 : content.Height;
			}
		}

		/// <summary>
		/// 主动测量内容高度：只在宽度变化时调用一次。
		/// 做法是"测量时临时打开 AutoSize，量完立刻关掉"——
		/// AutoSize 打开时 TableLayoutPanel 才会算出真实高度，关掉后尺寸就固定住，
		/// 之后窗口移动不会再触发重新测量（这正是之前卡顿的根源）。
		/// </summary>
		private void MeasureContent(int width)
		{
			if (content == null)
			{
				return;
			}
			content.SuspendLayout();
			try
			{
				content.AutoSize = true;
				if (content.Width != width)
				{
					content.Width = width;
				}
				content.PerformLayout();
				int height = content.Height;
				content.AutoSize = false;
				if (height < 1)
				{
					height = Math.Max(Ui.P(40), content.PreferredSize.Height);
				}
				if (content.Height != height)
				{
					content.Height = height;
				}
			}
			finally
			{
				content.ResumeLayout(true);
			}
		}

		/// <summary>只重算滚动范围与滑块比例，不动布局。</summary>
		private void AdjustRange()
		{
			int viewHeight = Math.Max(1, ClientSize.Height);
			maximum = Math.Max(0, ContentHeight - viewHeight);
			if (scrollValue > maximum)
			{
				scrollValue = maximum;
			}
			if (scrollValue < 0)
			{
				scrollValue = 0;
			}
		}

		public void UpdateScroll()
		{
			if (content == null || adjusting)
			{
				return;
			}
			adjusting = true;
			try
			{
				int viewHeight = Math.Max(1, ClientSize.Height);
				// 槽位恒定预留 12px：宽度稳定，内容不会因为滚动条出现/消失反复重排
				int width = Math.Max(1, ClientSize.Width - GutterWidth);
				if (content == null)
				{
					return;
				}
				// 只有宽度变化才重新测量内容（这一步会触发表格重排，所以不在滚动时做）
				if (content.Width != width || content.Height <= 0)
				{
					MeasureContent(width);
					AdjustRange();
				}
				// 视口高度撑满内容：滚动时整体上移，视口底部不会露出空白
				int viewportHeight = Math.Max(viewHeight, ContentHeight);
				if (viewport.Width != width || viewport.Height != viewportHeight)
				{
					viewport.SetBounds(0, -scrollValue, width, viewportHeight);
				}
				else if (viewport.Top != -scrollValue)
				{
					viewport.Top = -scrollValue;
				}
			}
			finally
			{
				adjusting = false;
			}
			Invalidate();
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			base.OnLayout(levent);
			UpdateScroll();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			WheelGate.Register(this);
		}

		/// <summary>滚轮：按屏幕坐标判断鼠标是否在本容器内（供全局消息过滤器调用）。</summary>
		internal bool ContainsScreen(Point screenPoint)
		{
			if (!IsHandleCreated || IsDisposed || !Visible)
			{
				return false;
			}
			if (maximum <= 0)
			{
				return false;
			}
			return RectangleToScreen(ClientRectangle).Contains(screenPoint);
		}

		/// <summary>滚一格（120）滚动 3 行左右，步长与系统一致。</summary>
		internal void ScrollByWheel(int delta)
		{
			ScrollBy(-delta * Ui.P(18) * 3 / 120);
		}

		internal void ScrollBy(int delta)
		{
			if (delta == 0 || maximum <= 0)
			{
				return;
			}
			int value = scrollValue + delta;
			if (value < 0)
			{
				value = 0;
			}
			if (value > maximum)
			{
				value = maximum;
			}
			if (value == scrollValue)
			{
				return;
			}
			scrollValue = value;
			// 关键：移动"视口"这一个窗口，内容的 Bounds 不变，
			// 所以不会触发表格重新测量（实测 23.6ms/步 → 5ms/步）
			viewport.Top = -scrollValue;
			// 只重画右侧滚动条槽位：其余被揭开的部分由 Windows 自己裁好区域重画
			Invalidate(new Rectangle(ClientSize.Width - GutterWidth - Ui.P(2), 0, GutterWidth + Ui.P(2), ClientSize.Height));
		}

		private Rectangle ThumbRect()
		{
			int trackTop = Ui.P(4);
			int trackHeight = Math.Max(Ui.P(20), ClientSize.Height - Ui.P(8));
			long total = Math.Max(1, ContentHeight);
			int thumbHeight = (int)Math.Max(Ui.P(28), (long)trackHeight * ClientSize.Height / total);
			if (thumbHeight > trackHeight)
			{
				thumbHeight = trackHeight;
			}
			int travel = Math.Max(1, trackHeight - thumbHeight);
			int top = trackTop + ((maximum <= 0) ? 0 : travel * scrollValue / maximum);
			return new Rectangle(ClientSize.Width - GutterWidth + Ui.P(3), top, Ui.P(6), thumbHeight);
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			if (maximum <= 0)
			{
				return;
			}
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle thumb = ThumbRect();
			Color color = dragging ? Theme.Accent : Theme.ScrollThumb;
			using (GraphicsPath path = ModernButton.RoundedPath(thumb, Ui.P(3)))
			{
				using (SolidBrush brush = new SolidBrush(color))
				{
					e.Graphics.FillPath(brush, path);
				}
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (maximum <= 0 || e.X < ClientSize.Width - GutterWidth)
			{
				return;
			}
			Rectangle thumb = ThumbRect();
			if (thumb.Contains(e.Location))
			{
				dragging = true;
				dragOffset = e.Y - thumb.Top;
			}
			else
			{
				ScrollBy((e.Y < thumb.Top) ? -(int)(ClientSize.Height * 0.9) : (int)(ClientSize.Height * 0.9));
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			if (!dragging)
			{
				return;
			}
			int trackTop = Ui.P(4);
			int trackHeight = Math.Max(Ui.P(20), ClientSize.Height - Ui.P(8));
			Rectangle thumb = ThumbRect();
			int travel = Math.Max(1, trackHeight - thumb.Height);
			int target = e.Y - dragOffset - trackTop;
			if (target < 0)
			{
				target = 0;
			}
			if (target > travel)
			{
				target = travel;
			}
			int desired = (maximum <= 0) ? 0 : target * maximum / travel;
			ScrollBy(desired - scrollValue);
		}

		protected override void OnMouseUp(MouseEventArgs e)
		{
			base.OnMouseUp(e);
			dragging = false;
			Invalidate();
		}

		public void ApplyTheme()
		{
			viewport.BackColor = Theme.Bg;
			Invalidate();
		}

		/// <summary>视口：只负责承载内容并随滚动整体上下移动，自身双缓冲避免滚动抖动。</summary>
		private sealed class ScrollViewport : Panel
		{
			public ScrollViewport()
			{
				DoubleBuffered = true;
				SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
			}
		}
	}

	/// <summary>
	/// 滚轮消息路由：全局只注册一个消息过滤器，用弱引用记录所有滚动容器。
	/// 以前每个容器各注册一个过滤器且永不移除，滚轮时会被重复调用，越用越卡。
	/// </summary>
	internal static class WheelGate
	{
		private static readonly List<WeakReference> Hosts = new List<WeakReference>();

		private static bool installed;

		public static void Register(ModernScrollHost host)
		{
			if (host == null)
			{
				return;
			}
			lock (Hosts)
			{
				Hosts.Add(new WeakReference(host));
				if (!installed)
				{
					Application.AddMessageFilter(new Router());
					installed = true;
				}
			}
		}

		private sealed class Router : IMessageFilter
		{
			public bool PreFilterMessage(ref Message m)
			{
				if (m.Msg != 0x020A)
				{
					return false;
				}
				int delta;
				Point screen;
				try
				{
					delta = (int)(m.WParam.ToInt64() >> 16);
					screen = Cursor.Position;
				}
				catch
				{
					return false;
				}
				lock (Hosts)
				{
					for (int i = Hosts.Count - 1; i >= 0; i--)
					{
						ModernScrollHost host = Hosts[i].Target as ModernScrollHost;
						if (host == null || host.IsDisposed)
						{
							Hosts.RemoveAt(i);
							continue;
						}
						if (host.ContainsScreen(screen))
						{
							host.ScrollByWheel(delta);
							return true;
						}
					}
				}
				return false;
			}
		}
	}

	/// <summary>
	/// 自绘下拉选择框：收起态是圆角描边 + 文字 + 线性箭头，展开态是跟随主题的列表浮层。
	/// 对外只暴露 Items / SelectedIndex / SelectedIndexChanged，调用方写法与原 ComboBox 一致。
	/// </summary>
	public class ModernSelect : Control, IThemedControl
	{
		private readonly List<string> items = new List<string>();

		private int selectedIndex = -1;

		private readonly Tween hoverTween = new Tween();

		private ModernSelectPopup popup;

		public ModernSelect()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
				ControlStyles.SupportsTransparentBackColor, true);
			Font = Theme.UiFont;
			BackColor = Theme.Input;
			ForeColor = Theme.Text;
			Cursor = Cursors.Hand;
			hoverTween.Changed = delegate { Invalidate(); };
		}

		/// <summary>选项列表（用法与 ComboBox.Items.Add 一致）。</summary>
		public List<string> Items
		{
			get
			{
				return items;
			}
		}

		public int SelectedIndex
		{
			get
			{
				return selectedIndex;
			}
			set
			{
				int clamped = (value < -1 || value >= items.Count) ? -1 : value;
				if (clamped == selectedIndex)
				{
					return;
				}
				selectedIndex = clamped;
				Invalidate();
				EventHandler handler = SelectedIndexChanged;
				if (handler != null)
				{
					handler(this, EventArgs.Empty);
				}
			}
		}

		public string SelectedItem
		{
			get
			{
				if (selectedIndex >= 0 && selectedIndex < items.Count)
				{
					return items[selectedIndex];
				}
				return null;
			}
		}

		public event EventHandler SelectedIndexChanged;

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = ClientRectangle;
			if (bounds.Width < 2 || bounds.Height < 2)
			{
				return;
			}
			bounds.Width -= 1;
			bounds.Height -= 1;
			bool open = (popup != null && !popup.IsDisposed && popup.Visible);
			Color border = open ? Theme.Accent : (hoverTween.Value > 0f ? Theme.BlendTo(Theme.Border, Theme.Accent, hoverTween.Value * 0.6) : Theme.Border);
			using (GraphicsPath path = ModernButton.RoundedPath(bounds, UiRadius.Control))
			{
				using (SolidBrush brush = new SolidBrush(Enabled ? Theme.Input : Theme.ButtonDisabled))
				{
					e.Graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(border, 1.2f))
				{
					e.Graphics.DrawPath(pen, path);
				}
			}
			string caption = SelectedItem;
			Color textColor = Enabled ? (caption == null ? Theme.DisabledText : Theme.Text) : Theme.DisabledText;
			Rectangle textRect = new Rectangle(UiSpace.Md, 0, Math.Max(0, Width - UiSpace.Md - Ui.P(34)), Height);
			TextRenderer.DrawText(e.Graphics, caption ?? "", Font, textRect, textColor,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			int arrowPx = Ui.P(12);
			Rectangle arrowRect = new Rectangle(Width - UiSpace.Md - arrowPx, (Height - arrowPx) / 2, arrowPx, arrowPx);
			IconPainter.Draw(e.Graphics, arrowRect, ModernIcon.ChevronDown, Enabled ? Theme.Dim : Theme.DisabledText, 1.4);
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			base.OnMouseEnter(e);
			if (Enabled)
			{
				hoverTween.To(1f);
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			hoverTween.To(0f);
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (!Enabled)
			{
				return;
			}
			TogglePopup();
		}

		private void TogglePopup()
		{
			try
			{
				if (popup != null && !popup.IsDisposed && popup.Visible)
				{
					popup.Close();
					popup = null;
					Invalidate();
					return;
				}
				if (items.Count == 0)
				{
					return;
				}
				Form host = FindForm();
				popup = new ModernSelectPopup(items, selectedIndex, this);
				popup.Show(host);
				popup.FormClosed += delegate
				{
					int picked = popup.Picked;
					popup = null;
					if (picked >= 0)
					{
						SelectedIndex = picked;
					}
					Invalidate();
				};
				Invalidate();
			}
			catch
			{
				popup = null;
			}
		}

		public void ApplyTheme()
		{
			hoverTween.Jump(0f);
			Invalidate();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				try
				{
					if (popup != null && !popup.IsDisposed)
					{
						popup.Close();
						popup.Dispose();
					}
				}
				catch
				{
				}
				popup = null;
				hoverTween.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	/// <summary>下拉浮层：圆角、无边框、跟随主题，支持鼠标与方向键选择。</summary>
	internal sealed class ModernSelectPopup : Form
	{
		private readonly List<string> items;

		private int hoverIndex = -1;

		private int startIndex;

		private readonly int rowHeight;

		/// <summary>被选中的下标，-1 表示取消。</summary>
		public int Picked { get; private set; }

		public ModernSelectPopup(List<string> source, int selected, Control owner)
		{
			items = source;
			startIndex = Math.Max(0, selected);
			Picked = -1;
			rowHeight = Ui.P(30);
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			DoubleBuffered = true;
			BackColor = Theme.Panel;
			ForeColor = Theme.Text;
			Font = (owner != null) ? owner.Font : Theme.UiFont;
			ShowWithoutActivationCore();
			int padding = Ui.P(6);
			int height = Math.Min(items.Count, 12) * rowHeight + padding * 2;
			int width = ((owner != null) ? owner.Width : Ui.P(240));
			Rectangle screen = Screen.FromControl(owner).WorkingArea;
			int x = (owner != null) ? owner.PointToScreen(Point.Empty).X : screen.Left + Ui.P(100);
			int y = (owner != null) ? owner.PointToScreen(new Point(0, owner.Height)).Y + Ui.P(4) : screen.Top + Ui.P(100);
			if (x + width > screen.Right)
			{
				x = Math.Max(screen.Left, screen.Right - width);
			}
			if (y + height > screen.Bottom)
			{
				y = Math.Max(screen.Top, y - height - (owner == null ? 0 : owner.Height) - Ui.P(8));
			}
			Bounds = new Rectangle(x, y, width, height);
			UpdateRegion();
			Deactivate += delegate
			{
				Close();
			};
		}

		private void ShowWithoutActivationCore()
		{
			SetStyle(ControlStyles.Selectable, true);
			TabStop = true;
		}

		private void UpdateRegion()
		{
			try
			{
				using (GraphicsPath path = ModernButton.RoundedPath(new Rectangle(0, 0, Width, Height), UiRadius.Control))
				{
					Region old = Region;
					Region = new Region(path);
					if (old != null)
					{
						old.Dispose();
					}
				}
			}
			catch
			{
			}
		}

		private int IndexAt(int y)
		{
			int padding = Ui.P(6);
			int index = (y - padding) / rowHeight;
			if (index < 0 || index >= items.Count)
			{
				return -1;
			}
			return index;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			using (SolidBrush brush = new SolidBrush(Theme.Panel))
			{
				e.Graphics.FillRectangle(brush, ClientRectangle);
			}
			int padding = Ui.P(6);
			for (int i = 0; i < items.Count; i++)
			{
				int top = padding + i * rowHeight;
				if (top > ClientSize.Height)
				{
					break;
				}
				Rectangle row = new Rectangle(padding, top, ClientSize.Width - padding * 2, rowHeight);
				if (i == startIndex)
				{
					using (SolidBrush brush = new SolidBrush(Theme.Selected))
					{
						e.Graphics.FillRectangle(brush, row);
					}
				}
				else if (i == hoverIndex)
				{
					using (SolidBrush brush = new SolidBrush(Theme.Hover))
					{
						e.Graphics.FillRectangle(brush, row);
					}
				}
				Rectangle text = new Rectangle(row.Left + Ui.P(8), row.Top, row.Width - Ui.P(16), row.Height);
				TextRenderer.DrawText(e.Graphics, items[i], Font, text,
					(i == startIndex) ? Theme.Accent : Theme.Text,
					TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			}
			using (Pen pen = new Pen(Theme.Border, 1f))
			{
				using (GraphicsPath path = ModernButton.RoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), UiRadius.Control))
				{
					e.Graphics.DrawPath(pen, path);
				}
			}
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			int index = IndexAt(e.Y);
			if (index != hoverIndex)
			{
				hoverIndex = index;
				Invalidate();
			}
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			int index = IndexAt(e.Y);
			if (index >= 0)
			{
				Picked = index;
				Close();
			}
		}

		protected override void OnKeyDown(KeyEventArgs e)
		{
			base.OnKeyDown(e);
			if (e.KeyCode == Keys.Escape)
			{
				Close();
				return;
			}
			if (e.KeyCode == Keys.Enter && hoverIndex >= 0)
			{
				Picked = hoverIndex;
				Close();
				return;
			}
			if (e.KeyCode == Keys.Down)
			{
				hoverIndex = Math.Min(items.Count - 1, hoverIndex + 1);
				Invalidate();
				e.Handled = true;
			}
			else if (e.KeyCode == Keys.Up)
			{
				hoverIndex = Math.Max(0, hoverIndex - 1);
				Invalidate();
				e.Handled = true;
			}
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			UpdateRegion();
		}
	}

	/// <summary>自绘复选框：圆角方框、柔和选中色、悬浮/聚焦反馈，完全跟随主题。</summary>
	public class ModernCheckBox : CheckBox, IThemedControl
	{
		private readonly Tween hoverTween = new Tween();

		public ModernCheckBox()
		{
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw |
				ControlStyles.SupportsTransparentBackColor, true);
			BackColor = Color.Transparent;
			ForeColor = Theme.Text;
			Font = Theme.UiFont;
			Cursor = Cursors.Hand;
			hoverTween.Changed = delegate { Invalidate(); };
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Ui.FillSurface(this, e.Graphics);
			int boxSize = Ui.P(18);
			int top = Math.Max(0, (ClientSize.Height - boxSize) / 2);
			Rectangle box = new Rectangle(Ui.P(1), top, boxSize, boxSize);
			float hover = hoverTween.Value;
			// 未选中时边框比普通描边略深一档，浅色主题下也能一眼看出这里能勾
			Color border = Checked ? Theme.Accent : Theme.BlendTo(Theme.Border, Theme.Dim, 0.45);
			if (!Checked && hover > 0f)
			{
				border = Theme.BlendTo(border, Theme.Accent, hover * 0.7);
			}
			if (!Enabled)
			{
				border = Theme.Border;
			}
			Color fill = Checked ? Theme.Accent : Theme.Input;
			if (!Checked && hover > 0f)
			{
				fill = Theme.BlendTo(fill, Theme.AccentSoft, hover * 0.8);
			}
			if (!Enabled)
			{
				fill = Theme.ButtonDisabled;
			}
			using (GraphicsPath path = ModernButton.RoundedPath(box, UiRadius.Small))
			{
				using (SolidBrush brush = new SolidBrush(fill))
				{
					e.Graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(border, 1.4f))
				{
					e.Graphics.DrawPath(pen, path);
				}
			}
			if (Checked)
			{
				Color markColor = Enabled ? Theme.OnAccent : Theme.DisabledText;
				IconPainter.Draw(e.Graphics, box, ModernIcon.Check, markColor, 1.8);
			}
			Rectangle textBounds = new Rectangle(box.Right + UiSpace.Sm, 0, Math.Max(0, ClientSize.Width - box.Right - UiSpace.Sm), ClientSize.Height);
			TextRenderer.DrawText(e.Graphics, Text, Font, textBounds,
				Enabled ? ForeColor : Theme.DisabledText,
				TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPadding);
			if (Focused && ShowFocusCues)
			{
				Rectangle focus = new Rectangle(box.Right + UiSpace.Sm - 2, Ui.P(2), Math.Max(0, ClientSize.Width - box.Right - UiSpace.Sm), Math.Max(0, ClientSize.Height - Ui.P(4)));
				ControlPaint.DrawFocusRectangle(e.Graphics, focus, ForeColor, BackColor);
			}
		}

		public void ApplyTheme()
		{
			ForeColor = Theme.Text;
			Invalidate();
		}

		protected override void OnMouseEnter(EventArgs e)
		{
			if (Enabled)
			{
				hoverTween.To(1f);
			}
			base.OnMouseEnter(e);
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			hoverTween.To(0f);
			base.OnMouseLeave(e);
		}

		protected override void OnCheckedChanged(EventArgs e)
		{
			base.OnCheckedChanged(e);
			Invalidate();
		}

		protected override void OnEnabledChanged(EventArgs e)
		{
			base.OnEnabledChanged(e);
			if (!Enabled)
			{
				hoverTween.Jump(0f);
			}
			Cursor = Enabled ? Cursors.Hand : Cursors.Default;
			Invalidate();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				hoverTween.Dispose();
			}
			base.Dispose(disposing);
		}
	}

	/// <summary>圆角卡片容器：填充 + 1px 描边 + 圆角裁剪，落影由宿主 ModernSurface 负责。</summary>
	public class ModernCard : Panel, IThemedControl
	{
		public int CornerRadius { get; set; }

		public bool Outline { get; set; }

		public Color CardColor { get; set; }

		public ModernCard()
		{
			CornerRadius = UiRadius.Card;
			Outline = true;
			CardColor = Theme.Panel;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			BackColor = Theme.Bg;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = ClientRectangle;
			if (bounds.Width < 2 || bounds.Height < 2)
			{
				return;
			}
			bounds.Width -= 1;
			bounds.Height -= 1;
			using (GraphicsPath path = ModernButton.RoundedPath(bounds, CornerRadius))
			{
				using (SolidBrush brush = new SolidBrush(CardColor))
				{
					e.Graphics.FillPath(brush, path);
				}
				if (Outline)
				{
					using (Pen pen = new Pen(Theme.Border, 1f))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
			}
		}

		public void ApplyTheme()
		{
			BackColor = Theme.Bg;
			Invalidate();
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			UpdateRegion();
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);
			UpdateRegion();
		}

		private void UpdateRegion()
		{
			if (ClientRectangle.Width < 2 || ClientRectangle.Height < 2)
			{
				return;
			}
			Rectangle bounds = ClientRectangle;
			bounds.Width -= 1;
			bounds.Height -= 1;
			try
			{
				using (GraphicsPath path = ModernButton.RoundedPath(bounds, CornerRadius))
				{
					Region old = Region;
					Region = new Region(path);
					if (old != null)
					{
						old.Dispose();
					}
				}
			}
			catch
			{
			}
		}
	}

	/// <summary>
	/// 双缓冲容器：可选在背景给子卡片画一层柔和落影（卡片自己画不到自己的外面）。
	/// 这样就有了"底层背景 / 卡片 / 悬浮弹窗"三层视觉层次。
	/// </summary>
	public class ModernSurface : Panel, IThemedControl
	{
		private Control shadowTarget;

		private int shadowSpread;

		public ModernSurface()
		{
			DoubleBuffered = true;
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
		}

		/// <summary>要投影的子控件（通常是 Dock=Fill 的 ModernCard）。</summary>
		public Control ShadowTarget
		{
			get
			{
				return shadowTarget;
			}
			set
			{
				if (shadowTarget != null)
				{
					shadowTarget.Resize -= OnTargetResize;
					shadowTarget.VisibleChanged -= OnTargetResize;
				}
				shadowTarget = value;
				if (shadowTarget != null)
				{
					shadowTarget.Resize += OnTargetResize;
					shadowTarget.VisibleChanged += OnTargetResize;
				}
				Invalidate();
			}
		}

		/// <summary>投影扩散半径（逻辑像素，0 表示不投影）。</summary>
		public int ShadowSpread
		{
			get
			{
				return shadowSpread;
			}
			set
			{
				shadowSpread = value;
				Invalidate();
			}
		}

		private void OnTargetResize(object sender, EventArgs e)
		{
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			base.OnPaint(e);
			if (shadowTarget != null && shadowTarget.Visible && shadowSpread > 0)
			{
				UiShadow.Drop(e.Graphics, shadowTarget.Bounds, UiRadius.Card, Ui.P(shadowSpread), Theme.ShadowSoft);
			}
		}

		protected override void OnResize(EventArgs e)
		{
			base.OnResize(e);
			Invalidate();
		}

		protected override void OnScroll(ScrollEventArgs se)
		{
			base.OnScroll(se);
			Invalidate();
		}

		public void ApplyTheme()
		{
			Invalidate();
		}
	}

	/// <summary>
	/// 输入框容器：把原生输入框包进圆角描边里，实现「常态 / 聚焦 / 错误」三种边框样式。
	/// 内部控件仍是原来的 TextBox / NumericUpDown，输入逻辑、事件、取值方式完全不变。
	/// </summary>
	public class ModernField : Panel, IThemedControl
	{
		private readonly Control inner;

		private Control spinButtons;

		private bool focused;

		private bool showError;

		public ModernField(Control control)
		{
			inner = control;
			DoubleBuffered = true;
			SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
			BackColor = Theme.Input;
			Padding = Ui.Pad(1);
			if (inner != null)
			{
				Controls.Add(inner);
				inner.Enter += OnInnerFocus;
				inner.Leave += OnInnerBlur;
				inner.GotFocus += OnInnerFocus;
				inner.LostFocus += OnInnerBlur;
				MouseDown += delegate { inner.Focus(); };
			}
			NumericUpDown numeric = inner as NumericUpDown;
			if (numeric != null)
			{
				// 隐藏原生上下按钮（系统灰白小方块，无法跟随主题），换成自绘箭头，行为不变
				try
				{
					numeric.BorderStyle = BorderStyle.None;
					if (numeric.Controls.Count > 0)
					{
						numeric.Controls[0].Visible = false;
					}
				}
				catch
				{
				}
				spinButtons = new ModernSpinButtons(numeric);
				Controls.Add(spinButtons);
			}
			TextBox textBox = inner as TextBox;
			if (textBox != null)
			{
				textBox.BorderStyle = BorderStyle.None;
			}
		}

		/// <summary>被包裹的原始输入控件。</summary>
		public Control Inner
		{
			get
			{
				return inner;
			}
		}

		/// <summary>错误态：输入校验不通过时把边框标红（纯视觉，不影响任何判断逻辑）。</summary>
		public bool ShowError
		{
			get
			{
				return showError;
			}
			set
			{
				if (showError != value)
				{
					showError = value;
					Invalidate();
				}
			}
		}

		private void OnInnerFocus(object sender, EventArgs e)
		{
			focused = true;
			Invalidate();
		}

		private void OnInnerBlur(object sender, EventArgs e)
		{
			focused = false;
			Invalidate();
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Rectangle bounds = ClientRectangle;
			if (bounds.Width < 2 || bounds.Height < 2)
			{
				return;
			}
			bounds.Width -= 1;
			bounds.Height -= 1;
			// 矩形窗口 + 双缓冲：聚焦/错误态切换时只重绘自身，不会闪
			Ui.FillSurface(this, e.Graphics);
			Color border = showError ? Theme.Error : (focused ? Theme.Accent : Theme.Border);
			float thickness = (focused || showError) ? 1.6f : 1f;
			Color fill = inner != null && !inner.Enabled ? Theme.ButtonDisabled : Theme.Input;
			using (GraphicsPath path = ModernButton.RoundedPath(bounds, UiRadius.Control))
			{
				using (SolidBrush brush = new SolidBrush(fill))
				{
					e.Graphics.FillPath(brush, path);
				}
				using (Pen pen = new Pen(border, thickness))
				{
					e.Graphics.DrawPath(pen, path);
				}
			}
		}

		protected override void OnLayout(LayoutEventArgs levent)
		{
			base.OnLayout(levent);
			if (inner == null)
			{
				return;
			}
			int padX = Ui.P(10);
			int padY = Ui.P(2);
			Rectangle content = ClientRectangle;
			content = new Rectangle(content.Left + padX, content.Top + padY, Math.Max(1, content.Width - padX * 2), Math.Max(1, content.Height - padY * 2));
			int spinWidth = (spinButtons == null) ? 0 : Ui.P(22);
			TextBox textBox = inner as TextBox;
			NumericUpDown numeric = inner as NumericUpDown;
			if (textBox != null && !textBox.Multiline)
			{
				int height = Math.Min(content.Height, textBox.PreferredHeight);
				inner.SetBounds(content.Left, content.Top + Math.Max(0, (content.Height - height) / 2), content.Width, height);
			}
			else if (numeric != null)
			{
				int height = Math.Min(content.Height, Ui.P(24));
				// 数字框保持整幅宽度，自绘箭头浮在它右端上方，把系统那对灰白小按钮盖住
				inner.SetBounds(content.Left, content.Top + Math.Max(0, (content.Height - height) / 2), content.Width, height);
				if (spinButtons != null)
				{
					spinButtons.SetBounds(ClientRectangle.Right - padX - spinWidth, content.Top, spinWidth, content.Height);
					spinButtons.BringToFront();
				}
				if (numeric.Controls.Count > 0 && numeric.Controls[0].Visible)
				{
					numeric.Controls[0].Visible = false;
				}
			}
			else
			{
				inner.SetBounds(content.Left, content.Top, content.Width, content.Height);
			}
		}

		public void ApplyTheme()
		{
			BackColor = Theme.Input;
			Invalidate();
		}
	}

	/// <summary>数字框右侧的自绘上下箭头：点击调用 NumericUpDown.UpButton / DownButton，行为与原生一致。</summary>
	public class ModernSpinButtons : Control, IThemedControl
	{
		private readonly NumericUpDown owner;

		private int hovering = -1;

		public ModernSpinButtons(NumericUpDown numeric)
		{
			owner = numeric;
			SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
				ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
			Cursor = Cursors.Hand;
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
			Ui.FillSurface(this, e.Graphics);
			int half = Height / 2;
			Rectangle top = new Rectangle(0, 0, Width, half);
			Rectangle bottom = new Rectangle(0, half, Width, Height - half);
			Color baseColor = (owner != null && !owner.Enabled) ? Theme.DisabledText : Theme.Dim;
			int iconSize = Ui.P(10);
			Color topColor = (hovering == 0) ? Theme.Accent : baseColor;
			Color bottomColor = (hovering == 1) ? Theme.Accent : baseColor;
			IconPainter.Draw(e.Graphics, new Rectangle((Width - iconSize) / 2, top.Top + (top.Height - iconSize) / 2, iconSize, iconSize), ModernIcon.ArrowUp, topColor, 1.3);
			IconPainter.Draw(e.Graphics, new Rectangle((Width - iconSize) / 2, bottom.Top + (bottom.Height - iconSize) / 2, iconSize, iconSize), ModernIcon.ArrowDown, bottomColor, 1.3);
		}

		protected override void OnMouseMove(MouseEventArgs e)
		{
			base.OnMouseMove(e);
			int index = (e.Y < Height / 2) ? 0 : 1;
			if (index != hovering)
			{
				hovering = index;
				Invalidate();
			}
		}

		protected override void OnMouseLeave(EventArgs e)
		{
			base.OnMouseLeave(e);
			hovering = -1;
			Invalidate();
		}

		protected override void OnMouseDown(MouseEventArgs e)
		{
			base.OnMouseDown(e);
			if (owner == null || !owner.Enabled)
			{
				return;
			}
			try
			{
				if (e.Y < Height / 2)
				{
					owner.UpButton();
				}
				else
				{
					owner.DownButton();
				}
			}
			catch
			{
			}
			Invalidate();
		}

		public void ApplyTheme()
		{
			Invalidate();
		}
	}

	/// <summary>
	/// 模态遮罩层：打开弹窗前铺一层半透明暗色，点击遮罩关闭弹窗。
	/// 只做视觉遮罩，不拦截业务，也不抢焦点（WS_EX_NOACTIVATE）。
	/// </summary>
	public sealed class DimLayer : Form
	{
		private static DimLayer active;

		private Form ownerForm;

		private DimLayer()
		{
			FormBorderStyle = FormBorderStyle.None;
			ShowInTaskbar = false;
			StartPosition = FormStartPosition.Manual;
			BackColor = Theme.IsDark ? Color.FromArgb(5, 7, 11) : Color.FromArgb(26, 30, 42);
			Opacity = Theme.IsDark ? 0.55 : 0.32;
			DoubleBuffered = true;
			ShowWithoutActivationAlways();
			Click += OnLayerClick;
		}

		private void ShowWithoutActivationAlways()
		{
			SetStyle(ControlStyles.Selectable, false);
			TabStop = false;
		}

		protected override bool ShowWithoutActivation
		{
			get
			{
				return true;
			}
		}

		protected override CreateParams CreateParams
		{
			get
			{
				CreateParams cp = base.CreateParams;
				cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
				cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
				return cp;
			}
		}

		/// <summary>在宿主窗体前面铺一层遮罩（必须在 ShowDialog 之前调用）。</summary>
		public static void ShowFor(Form owner)
		{
			try
			{
				if (owner == null || owner.IsDisposed || !owner.Visible || active != null)
				{
					return;
				}
				DimLayer layer = new DimLayer();
				layer.ownerForm = owner;
				layer.Owner = owner;
				layer.Bounds = owner.Bounds;
				layer.Show();
				active = layer;
			}
			catch
			{
				active = null;
			}
		}

		/// <summary>弹窗关闭后收起遮罩。</summary>
		public static void HideLayer()
		{
			try
			{
				if (active != null)
				{
					active.Hide();
					active.Dispose();
				}
			}
			catch
			{
			}
			active = null;
		}

		/// <summary>点遮罩 = 取消当前模态弹窗。</summary>
		private void OnLayerClick(object sender, EventArgs e)
		{
			try
			{
				FormCollection forms = Application.OpenForms;
				for (int i = forms.Count - 1; i >= 0; i--)
				{
					Form form = forms[i];
					if (form != null && form != this && form.Visible && form.Modal && form.Owner == ownerForm)
					{
						form.DialogResult = DialogResult.Cancel;
						form.Close();
						return;
					}
				}
			}
			catch
			{
			}
		}
	}

	/// <summary>
	/// 主题化悬浮提示：替代系统黄色 tooltip，圆角、跟随主题、超长内容自动换行。
	/// 用于表意不清的图标按钮、被省略号截断的长文本。
	/// </summary>
	public static class Tip
	{
		private static TipForm form;

		private static Timer hideTimer;

		/// <summary>给控件挂上提示（文字默认取控件 Text，空则不显示）。</summary>
		public static void Attach(Control control)
		{
			Attach(control, null);
		}

		public static void Attach(Control control, string text)
		{
			if (control == null)
			{
				return;
			}
			control.MouseEnter += delegate
			{
				string caption = text;
				if (string.IsNullOrEmpty(caption))
				{
					caption = control.Text;
				}
				Show(control, caption);
			};
			control.MouseLeave += delegate
			{
				Hide();
			};
			control.MouseDown += delegate
			{
				Hide();
			};
		}

		/// <summary>长文本行悬浮提示：鼠标停在日志行上时显示该行完整内容。</summary>
		public static void AttachLineHover(RichTextBox box, int minLength)
		{
			if (box == null)
			{
				return;
			}
			box.MouseHover += delegate(object sender, EventArgs e)
			{
				try
				{
					Point position = box.PointToClient(Cursor.Position);
					int index = box.GetCharIndexFromPosition(position);
					int line = box.GetLineFromCharIndex(index);
					string[] lines = box.Lines;
					if (line >= 0 && line < lines.Length)
					{
						string text = lines[line].Trim();
						if (text.Length >= minLength)
						{
							Show(box, text);
						}
					}
				}
				catch
				{
				}
			};
			box.MouseLeave += delegate
			{
				Hide();
			};
		}

		public static void Show(Control control, string text)
		{
			if (control == null || string.IsNullOrEmpty(text) || text.Length < 2)
			{
				return;
			}
			try
			{
				if (hideTimer != null)
				{
					hideTimer.Stop();
				}
				if (form == null || form.IsDisposed)
				{
					form = new TipForm();
				}
				Point screen = Cursor.Position;
				form.ShowTip(text, new Point(screen.X + Ui.P(12), screen.Y + Ui.P(20)));
				if (hideTimer == null)
				{
					hideTimer = new Timer();
					hideTimer.Interval = 8000;
					hideTimer.Tick += delegate
					{
						hideTimer.Stop();
						Hide();
					};
				}
				hideTimer.Start();
			}
			catch
			{
			}
		}

		public static void Hide()
		{
			try
			{
				if (hideTimer != null)
				{
					hideTimer.Stop();
				}
				if (form != null && !form.IsDisposed)
				{
					form.Hide();
				}
			}
			catch
			{
			}
		}

		private sealed class TipForm : Form
		{
			private string caption = "";

			public TipForm()
			{
				FormBorderStyle = FormBorderStyle.None;
				ShowInTaskbar = false;
				StartPosition = FormStartPosition.Manual;
				TopMost = true;
				BackColor = Theme.TooltipBg;
				DoubleBuffered = true;
				Font = UiFonts.Small;
			}

			protected override bool ShowWithoutActivation
			{
				get
				{
					return true;
				}
			}

			protected override CreateParams CreateParams
			{
				get
				{
					CreateParams cp = base.CreateParams;
					cp.ExStyle |= 0x08000000; // WS_EX_NOACTIVATE
					cp.ExStyle |= 0x00000080; // WS_EX_TOOLWINDOW
					return cp;
				}
			}

			public void ShowTip(string text, Point screen)
			{
				caption = text;
				BackColor = Theme.TooltipBg;
				Font = UiFonts.Small;
				int padding = Ui.P(10);
				int maxWidth = Ui.P(430);
				Size measured = TextRenderer.MeasureText(text, Font, new Size(maxWidth, 0), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
				int width = Math.Min(maxWidth, measured.Width) + padding * 2;
				int height = measured.Height + padding * 2 - Ui.P(2);
				Rectangle screenBounds = Screen.FromPoint(screen).WorkingArea;
				int x = Math.Min(screen.X, screenBounds.Right - width - Ui.P(4));
				int y = Math.Min(screen.Y, screenBounds.Bottom - height - Ui.P(4));
				if (x < screenBounds.Left)
				{
					x = screenBounds.Left;
				}
				if (y < screenBounds.Top)
				{
					y = screenBounds.Top;
				}
				Bounds = new Rectangle(x, y, width, height);
				UpdateRegion();
				if (!Visible)
				{
					Show();
				}
				Invalidate();
			}

			private void UpdateRegion()
			{
				try
				{
					using (GraphicsPath path = ModernButton.RoundedPath(new Rectangle(0, 0, Width, Height), UiRadius.Small + Ui.P(2)))
					{
						Region old = Region;
						Region = new Region(path);
						if (old != null)
						{
							old.Dispose();
						}
					}
				}
				catch
				{
				}
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				using (SolidBrush brush = new SolidBrush(Theme.TooltipBg))
				{
					e.Graphics.FillRectangle(brush, ClientRectangle);
				}
				int padding = Ui.P(10);
				Rectangle textBounds = new Rectangle(padding, padding - Ui.P(1), ClientSize.Width - padding * 2, ClientSize.Height - padding * 2);
				TextRenderer.DrawText(e.Graphics, caption, Font, textBounds, Theme.TooltipText,
					TextFormatFlags.Left | TextFormatFlags.Top | TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
			}
		}
	}

	/// <summary>
	/// 主题化消息框：替代系统 MessageBox（原生弹窗在深色主题下是刺眼的纯白），
	/// 返回值和 MessageBox.Show 一致，调用方判断逻辑不变。
	/// </summary>
	public static class MessageDialog
	{
		public static DialogResult Show(IWin32Window owner, string text, string title, MessageBoxIcon icon, MessageBoxButtons buttons)
		{
			Form ownerForm = owner as Form;
			bool dimmed = false;
			try
			{
				if (ownerForm != null && ownerForm.Visible)
				{
					DimLayer.ShowFor(ownerForm);
					dimmed = true;
				}
				using (MessageForm dialog = new MessageForm(text, title, icon, buttons))
				{
					return (ownerForm != null) ? dialog.ShowDialog(ownerForm) : dialog.ShowDialog();
				}
			}
			catch
			{
				// 极少数情况（窗口销毁中）退回原生消息框，保证提示不丢
				return MessageBox.Show(text, title, buttons, icon);
			}
			finally
			{
				if (dimmed)
				{
					DimLayer.HideLayer();
				}
			}
		}

		private sealed class MessageForm : Form
		{
			private readonly string body;

			private readonly MessageBoxIcon kind;

			public MessageForm(string text, string title, MessageBoxIcon icon, MessageBoxButtons buttons)
			{
				body = text;
				kind = icon;
				Text = string.IsNullOrEmpty(title) ? "EAgentX" : title;
				FormBorderStyle = FormBorderStyle.FixedDialog;
				StartPosition = FormStartPosition.CenterParent;
				MaximizeBox = false;
				MinimizeBox = false;
				ShowInTaskbar = false;
				ShowIcon = false;
				DoubleBuffered = true;
				Font = UiFonts.Body;
				BackColor = Theme.Panel;
				ForeColor = Theme.Text;
				ClientSize = Ui.Sz(460, 200);

				int padding = UiSpace.Xl;
				int iconSize = Ui.P(26);
				Label message = new Label();
				message.Text = text;
				message.Font = UiFonts.Body;
				message.ForeColor = Theme.Text;
				message.BackColor = Color.Transparent;
				Size measured = TextRenderer.MeasureText(text, UiFonts.Body, new Size(Ui.P(360), 0), TextFormatFlags.WordBreak);
				int textHeight = Math.Max(Ui.P(40), measured.Height + Ui.P(4));
				message.SetBounds(padding + iconSize + UiSpace.Md, padding + Ui.P(6), Ui.P(370), textHeight);
				Controls.Add(message);

				int buttonHeight = Ui.P(38);
				int buttonWidth = Ui.P(104);
				int bottom = padding + Math.Max(textHeight, iconSize) + Ui.P(24) + buttonHeight / 2;

				Button primary = new ModernButton();
				primary.Size = Ui.Sz(buttonWidth, buttonHeight);
				Theme.StyleButton(primary, accent: true);
				primary.DialogResult = (buttons == MessageBoxButtons.YesNo) ? DialogResult.Yes : DialogResult.OK;
				primary.Text = (buttons == MessageBoxButtons.YesNo) ? "确定" : "好的";
				Controls.Add(primary);

				if (buttons == MessageBoxButtons.YesNo)
				{
					Button secondary = new ModernButton();
					secondary.Size = Ui.Sz(buttonWidth, buttonHeight);
					Theme.StyleButton(secondary, accent: false);
					secondary.Text = "取消";
					secondary.DialogResult = DialogResult.No;
					secondary.Location = new Point(ClientSize.Width - padding - buttonWidth, bottom);
					primary.Location = new Point(secondary.Left - UiSpace.Sm - buttonWidth, bottom);
					Controls.Add(secondary);
					CancelButton = secondary;
				}
				else
				{
					primary.Location = new Point(ClientSize.Width - padding - buttonWidth, bottom);
					CancelButton = primary;
				}
				AcceptButton = primary;
				ClientSize = new Size(ClientSize.Width, bottom + buttonHeight + padding);
			}

			protected override void OnPaint(PaintEventArgs e)
			{
				base.OnPaint(e);
				e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
				// 注意：MessageBoxIcon 里 Error/Hand/Stop、Warning/Exclamation 取值相同，
				// 用 switch 会命重标签，这里改成 if 判断更安全
				Color accent;
				ModernIcon icon;
				if (kind == MessageBoxIcon.Error || kind == MessageBoxIcon.Hand || kind == MessageBoxIcon.Stop)
				{
					accent = Theme.Error;
					icon = ModernIcon.Close;
				}
				else if (kind == MessageBoxIcon.Warning || kind == MessageBoxIcon.Exclamation)
				{
					accent = Theme.Warning;
					icon = ModernIcon.Info;
				}
				else if (kind == MessageBoxIcon.Question)
				{
					accent = Theme.Accent;
					icon = ModernIcon.Info;
				}
				else
				{
					accent = Theme.Info;
					icon = ModernIcon.Info;
				}
				int size = Ui.P(26);
				Rectangle badge = new Rectangle(UiSpace.Xl, UiSpace.Xl + Ui.P(4), size, size);
				using (GraphicsPath path = ModernButton.RoundedPath(badge, size / 2))
				{
					using (SolidBrush brush = new SolidBrush(Color.FromArgb(38, accent)))
					{
						e.Graphics.FillPath(brush, path);
					}
					using (Pen pen = new Pen(Color.FromArgb(150, accent), 1.4f))
					{
						e.Graphics.DrawPath(pen, path);
					}
				}
				IconPainter.Draw(e.Graphics, badge, icon, accent, 1.5);
			}
		}
	}
}
