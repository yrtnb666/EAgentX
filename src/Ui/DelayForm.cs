using System;
using System.Drawing;
using System.Windows.Forms;

namespace EAgentX.Ui
{
	/// <summary>
	/// 顶部「发送间隔」按钮打开的小对话框：
	/// 设置「模型回答结束」到「下一次发送」之间等几秒；最多大于最少时每一步在区间内随机。
	/// 只调整了排版、圆角、间距和配色，取值逻辑（Seconds / MaxSeconds）完全不变。
	/// </summary>
	public class DelayForm : Form
	{
		private readonly NumericUpDown MinInput;

		private readonly NumericUpDown MaxInput;

		public int Seconds
		{
			get
			{
				return (int)MinInput.Value;
			}
		}

		public int MaxSeconds
		{
			get
			{
				return (int)MaxInput.Value;
			}
		}

		public DelayForm(int seconds)
			: this(seconds, 0)
		{
		}

		public DelayForm(int seconds, int maxSeconds)
		{
			Text = "发送间隔";
			base.AutoScaleMode = AutoScaleMode.None;
			base.ClientSize = Ui.FitToScreen(470, 292, 0.80);
			base.FormBorderStyle = FormBorderStyle.FixedDialog;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.StartPosition = FormStartPosition.CenterParent;
			base.ShowInTaskbar = false;
			base.ShowIcon = false;
			Theme.StyleForm(this);
			base.DoubleBuffered = true;

			Label title = Theme.Label("发送间隔", UiFonts.DialogTitle, Theme.Text);
			title.Location = Ui.Pt(26, 22);
			base.Controls.Add(title);

			Label hint = Theme.Label("模型回答结束到下一次发送之间至少等待「最少」秒（命令执行时间也算在内）。\r\n「最多」大于「最少」时每一步随机取值，避免固定节奏；设 0 或 ≤ 最少表示固定间隔。\r\n建议 3–10 秒；最少设为 0 表示不等待。", Theme.UiFontSmall, Theme.Dim);
			hint.AutoSize = false;
			hint.Size = Ui.Sz(418, 88);
			hint.Location = Ui.Pt(28, 64);
			base.Controls.Add(hint);

			Label minLabel = Theme.Label("最少", Theme.UiFont, Theme.Dim);
			minLabel.Location = Ui.Pt(26, 172);
			base.Controls.Add(minLabel);

			MinInput = new NumericUpDown();
			MinInput.Minimum = 0m;
			MinInput.Maximum = 300m;
			MinInput.Increment = 1m;
			MinInput.Value = Math.Max(0, Math.Min(300, seconds));
			MinInput.Size = Ui.Sz(96, 36);
			Theme.StyleInput(MinInput);
			MinInput.BorderStyle = BorderStyle.FixedSingle;
			MinInput.Font = Theme.UiFont;
			MinInput.TextAlign = HorizontalAlignment.Left;
			ModernField minField = new ModernField(MinInput);
			minField.Location = Ui.Pt(74, 160);
			minField.Size = Ui.Sz(96, 38);
			base.Controls.Add(minField);

			Label maxLabel = Theme.Label("最多", Theme.UiFont, Theme.Dim);
			maxLabel.Location = Ui.Pt(190, 172);
			base.Controls.Add(maxLabel);

			MaxInput = new NumericUpDown();
			MaxInput.Minimum = 0m;
			MaxInput.Maximum = 300m;
			MaxInput.Increment = 1m;
			MaxInput.Value = Math.Max(0, Math.Min(300, maxSeconds));
			MaxInput.Size = Ui.Sz(96, 36);
			Theme.StyleInput(MaxInput);
			MaxInput.BorderStyle = BorderStyle.FixedSingle;
			MaxInput.Font = Theme.UiFont;
			MaxInput.TextAlign = HorizontalAlignment.Left;
			ModernField maxField = new ModernField(MaxInput);
			maxField.Location = Ui.Pt(238, 160);
			maxField.Size = Ui.Sz(96, 38);
			base.Controls.Add(maxField);

			Label unit = Theme.Label("秒", Theme.UiFont, Theme.Dim);
			unit.Location = Ui.Pt(348, 172);
			base.Controls.Add(unit);

			Button cancelButton = new ModernButton
			{
				Text = "取消",
				Size = Ui.Sz(104, 38),
				Location = new Point(base.ClientSize.Width - Ui.P(24) - Ui.P(104), base.ClientSize.Height - Ui.P(24) - Ui.P(38))
			};
			Theme.StyleButton(cancelButton, accent: false);
			cancelButton.DialogResult = DialogResult.Cancel;
			base.Controls.Add(cancelButton);

			Button saveButton = new ModernButton
			{
				Text = "保存",
				Size = Ui.Sz(104, 38),
				Location = new Point(base.ClientSize.Width - Ui.P(24) - Ui.P(104) * 2 - UiSpace.Sm, base.ClientSize.Height - Ui.P(24) - Ui.P(38))
			};
			Theme.StyleButton(saveButton, accent: true);
			saveButton.DialogResult = DialogResult.OK;
			base.Controls.Add(saveButton);

			base.AcceptButton = saveButton;
			base.CancelButton = cancelButton;
		}
	}
}
