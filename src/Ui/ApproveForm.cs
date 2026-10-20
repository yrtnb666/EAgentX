using System;
using System.Drawing;
using System.Windows.Forms;

namespace EAgentX.Ui
{
	/// <summary>
	/// 命令审批窗口：显示模型给出的完整命令，Enter 允许、Esc 拒绝。
	/// 这是默认开启的最后一道防线（设置里可以关掉，关掉等于把电脑交给模型）。
	/// 按钮的 DialogResult、AcceptButton、CancelButton 与原来完全一致。
	/// </summary>
	public class ApproveForm : Form
	{
		public ApproveForm(string command)
		{
			Text = "确认执行";
			base.AutoScaleMode = AutoScaleMode.None;
			base.StartPosition = FormStartPosition.CenterParent;
			base.FormBorderStyle = FormBorderStyle.Sizable;
			Theme.StyleForm(this);
			base.ClientSize = Ui.FitToScreen(800, 440, 0.86);
			Size minSize = Ui.FitToScreen(580, 340, 0.70);
			MinimumSize = new Size(Math.Min(minSize.Width, base.ClientSize.Width), Math.Min(minSize.Height, base.ClientSize.Height));
			base.ShowInTaskbar = false;
			base.ShowIcon = false;
			base.MaximizeBox = false;
			base.MinimizeBox = false;
			base.TopMost = true;
			base.DoubleBuffered = true;
			base.Paint += delegate(object sender, PaintEventArgs e)
			{
				// 顶部 4px 强调色条：危险操作窗口的视觉标识
				using (SolidBrush brush = new SolidBrush(Theme.Warning))
				{
					e.Graphics.FillRectangle(brush, 0, 0, base.ClientSize.Width, Ui.P(4));
				}
			};

			Label title = Theme.Label("允许执行这条命令？", UiFonts.DialogTitle, Theme.Text);
			title.Location = Ui.Pt(24, 24);
			base.Controls.Add(title);

			Label hint = Theme.Label("请先核对命令内容。不清楚或不需要时，点「拒绝」。", Theme.UiFontSmall, Theme.Dim);
			hint.Location = Ui.Pt(26, 60);
			base.Controls.Add(hint);

			TextBox commandBox = new TextBox();
			commandBox.Multiline = true;
			commandBox.ReadOnly = true;
			commandBox.ScrollBars = ScrollBars.Both;
			commandBox.WordWrap = false;
			Theme.StyleInput(commandBox);
			commandBox.Font = Theme.MonoFont;
			Theme.SetTextRole(commandBox, ThemeRole.Command);
			commandBox.ForeColor = Theme.Command;
			commandBox.BorderStyle = BorderStyle.FixedSingle;
			commandBox.Text = command;

			// 命令区套圆角外框，自适应窗口大小
			ModernField commandField = new ModernField(commandBox);
			commandField.SetBounds(Ui.P(24), Ui.P(92), base.ClientSize.Width - Ui.P(48), base.ClientSize.Height - Ui.P(176));
			commandField.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			base.Controls.Add(commandField);

			Button denyButton = new ModernButton
			{
				Text = "拒绝  Esc",
				Size = Ui.Sz(126, 40)
			};
			((ModernButton)denyButton).Icon = ModernIcon.Close;
			Theme.StyleDangerButton(denyButton);
			denyButton.DialogResult = DialogResult.Cancel;
			denyButton.Location = new Point(base.ClientSize.Width - Ui.P(24) - Ui.P(126) * 2 - UiSpace.Md, base.ClientSize.Height - Ui.P(24) - Ui.P(40));
			denyButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			base.Controls.Add(denyButton);

			Button allowButton = new ModernButton
			{
				Text = "允许执行  Enter",
				Size = Ui.Sz(126, 40)
			};
			((ModernButton)allowButton).Icon = ModernIcon.Check;
			Theme.StyleButton(allowButton, accent: true);
			allowButton.DialogResult = DialogResult.OK;
			allowButton.Location = new Point(base.ClientSize.Width - Ui.P(24) - Ui.P(126), base.ClientSize.Height - Ui.P(24) - Ui.P(40));
			allowButton.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			base.Controls.Add(allowButton);

			base.AcceptButton = allowButton;
			base.CancelButton = denyButton;
		}
	}
}
