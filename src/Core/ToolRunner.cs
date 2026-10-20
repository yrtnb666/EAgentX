using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using EAgentX.Net;
using EAgentX.Util;

namespace EAgentX.Core
{
	/// <summary>工具执行结果：文本反馈 + 需要作为下一条消息附件回传的文件。</summary>
	public class ToolResult
	{
		public bool Ok;

		public string Text;

		public string[] Attachments;
	}

	/// <summary>
	/// 测试版新增的 4 个工具：截图、上传、鼠标、键盘。
	/// 参数都从模型给出的代码块里宽松解析（网页版会把代码块工具栏文字一起抓进来）。
	/// </summary>
	public static class ToolRunner
	{
		private static readonly Regex QuotedPath = new Regex("'([^']+\\.\\w{1,6})'|\"([^\"]+\\.\\w{1,6})\"", RegexOptions.Compiled);

		private static readonly Regex RawPath = new Regex("([A-Za-z]:\\\\[^\\r\\n\\t\"'*?<>|]+)", RegexOptions.Compiled);

		private static readonly Regex MouseArg = new Regex("(-?\\d{1,5})\\s*[,，]\\s*(-?\\d{1,5})(?:\\s*[,，]\\s*(left|right|double|左键|右键|双击))?", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex IntArg = new Regex("\\d{1,6}", RegexOptions.Compiled);

		private static readonly Regex EAgentJpgArg = new Regex("agent_getjpg_1_star\\s*\\(\\s*(?<arg>[^)]*?)\\s*\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex EAgentFileArgs = new Regex("agent_getfile_head\\s*\\((?<args>[^)]*)\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex EAgentMouseArg = new Regex("agent_mouse_simulate_(?<i>[1-3])_start\\s*\\(\\s*(?<v>-?\\d+)\\s*\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		private static readonly Regex EAgentKeyArg = new Regex("agent_keyboard_simulate_(?<i>[1-4])_start\\s*\\(\\s*(?<v>-?\\d+)\\s*\\)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

		/// <summary>截图前把主窗口调到几乎透明（由主窗口注册），避免挡住目标。</summary>
		public static Action HideSelf;

		/// <summary>截图后恢复主窗口。</summary>
		public static Action ShowSelf;

		/// <summary>
		/// 浏览器页面截图回调（由 AgentSession 注册到 WebEngine）：
		/// 传入窗口句柄，返回 PNG/JPG 字节；不是本程序的浏览器或失败时返回 null。
		/// CDP 的 Page.captureScreenshot 直接取渲染页面，即使窗口被其它软件覆盖也能截到。
		/// </summary>
		public static Func<IntPtr, byte[]> BrowserCapture;

		public static ToolResult Run(Settings s, ToolKind kind, string payload)
		{
			switch (kind)
			{
			case ToolKind.Screenshot:
				return Screenshot(s, payload);
			case ToolKind.Upload:
				return Upload(s, payload);
			case ToolKind.Mouse:
				return Mouse(payload);
			case ToolKind.Keyboard:
				return Keyboard(payload);
			default:
				return Fail("未知工具: " + kind);
			}
		}

		private static ToolResult Fail(string message)
		{
			ToolResult result = new ToolResult();
			result.Ok = false;
			result.Text = message;
			return result;
		}

		private static ToolResult Ok(string message)
		{
			return Ok(message, null);
		}

		private static ToolResult Ok(string message, string[] attachments)
		{
			ToolResult result = new ToolResult();
			result.Ok = true;
			result.Text = message;
			result.Attachments = attachments;
			return result;
		}

		// ---------------- 截图 ----------------

		public static ToolResult Screenshot(Settings s, string payload)
		{
			string text = (payload ?? "").Trim();
			string target = text;
			Match marker = EAgentJpgArg.Match(text);
			if (marker.Success)
			{
				target = marker.Groups["arg"].Value.Trim();
			}
			bool wholeScreen = target.Length == 0 ||
				target.IndexOf("屏幕", StringComparison.Ordinal) >= 0 ||
				target.IndexOf("全屏", StringComparison.Ordinal) >= 0 ||
				target.IndexOf("screen", StringComparison.OrdinalIgnoreCase) >= 0;
			IntPtr hwnd = IntPtr.Zero;
			if (!wholeScreen)
			{
				Match handleMatch = Regex.Match(target, "\\b(\\d{3,})\\b");
				if (!handleMatch.Success)
				{
					return Fail("截图参数里没有窗口句柄。写“屏幕”截整屏，或写窗口句柄数字（例如 1234）。");
				}
				long value;
				if (long.TryParse(handleMatch.Groups[1].Value, out value))
				{
					hwnd = new IntPtr(value);
				}
				if (!Win32.IsWindow(hwnd))
				{
					return Fail("窗口句柄无效：" + handleMatch.Groups[1].Value + "。可以先用 ```powershell 执行 Get-Process | Select-Object Id,MainWindowTitle 查窗口。");
				}
			}

			string dir = Path.Combine(s.WorkDir, ".eagentx", "shots");
			Directory.CreateDirectory(dir);
			string file = Path.Combine(dir, "shot-" + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".jpg");

			bool hidden = false;
			try
			{
				if (HideSelf != null)
				{
					HideSelf();
					hidden = true;
					Thread.Sleep(180);
				}
			}
			catch
			{
			}
			Bitmap bitmap = null;
			bool fileWritten = false;
			bool restoredFromMinimize = false;
			string method;
			try
			{
				if (wholeScreen)
				{
					bitmap = CaptureScreen();
					method = "整屏截图";
				}
				else
				{
					// ① 如果目标就是本程序的浏览器，优先走 CDP：它不依赖窗口可见性，也不会拿到黑图
					byte[] page = TryBrowserCapture(hwnd);
					if (page != null && page.Length > 0)
					{
						File.WriteAllBytes(file, page);
						fileWritten = true;
						method = "浏览器页面截图（CDP，不受遮挡影响）";
					}
					else
					{
						// ② 传统 GDI 窗口：PrintWindow 即使被完全覆盖也能取到内容
						bitmap = TryPrintWindow(hwnd);
						if (bitmap != null)
						{
							method = "后台截图（PrintWindow，不受遮挡影响）";
						}
						else
						{
							// ③ 最后的兜底：临时把目标窗口置前，截完恢复原前台窗口
							bitmap = CaptureWindowFront(hwnd, out restoredFromMinimize);
							method = "临时前置目标窗口后截图（已恢复原前台窗口）";
						}
					}
				}
				if (!fileWritten)
				{
					if (bitmap == null)
					{
						throw new Exception("无法截取该窗口");
					}
					SaveJpeg(bitmap, file);
				}
				int width;
				int height;
				using (Image image = Image.FromFile(file))
				{
					width = image.Width;
					height = image.Height;
				}
				StringBuilder note = new StringBuilder();
				note.AppendLine("截图完成，已作为附件回传：" + file);
				note.AppendLine("图片尺寸: " + width + "×" + height);
				note.AppendLine("截图方式: " + method);
				note.AppendLine("鼠标位置: " + Cursor.Position.X + "," + Cursor.Position.Y + "（物理像素）");
				if (!wholeScreen)
				{
					note.AppendLine("窗口: " + Win32.GetTitle(hwnd));
				}
				else
				{
					note.AppendLine("提示：整屏截图会包含遮挡在目标上的其它窗口；要截被遮挡的窗口，请改用窗口句柄截图。");
				}
				if (restoredFromMinimize)
				{
					note.AppendLine("目标窗口原本最小化，为了截图已恢复显示。");
				}
				note.Append("请查看附件后继续，不要重复截同一张图。");
				return Ok(note.ToString(), new string[1] { file });
			}
			catch (Exception ex)
			{
				return Fail("截图失败：" + ex.Message);
			}
			finally
			{
				if (bitmap != null)
				{
					bitmap.Dispose();
				}
				if (hidden && ShowSelf != null)
				{
					try
					{
						ShowSelf();
					}
					catch
					{
					}
				}
			}
		}

		private static Bitmap CaptureScreen()
		{
			Rectangle bounds = SystemInformation.VirtualScreen;
			Bitmap bitmap = new Bitmap(Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), PixelFormat.Format24bppRgb);
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bounds.Size, CopyPixelOperation.SourceCopy);
				DrawMouseMarker(graphics, bounds);
			}
			return bitmap;
		}

		/// <summary>PrintWindow 先试 PW_RENDERFULLCONTENT(2)，黑图/纯色再试 0；都失败返回 null。</summary>
		private static Bitmap TryPrintWindow(IntPtr hwnd)
		{
			Bitmap first = PrintWindowBitmap(hwnd, 2);
			if (first != null && !LooksBlank(first))
			{
				return first;
			}
			if (first != null)
			{
				first.Dispose();
			}
			Bitmap second = PrintWindowBitmap(hwnd, 0);
			if (second != null && !LooksBlank(second))
			{
				return second;
			}
			if (second != null)
			{
				second.Dispose();
			}
			return null;
		}

		private static Bitmap PrintWindowBitmap(IntPtr hwnd, uint flags)
		{
			Win32.RECT rect;
			if (!Win32.GetWindowRect(hwnd, out rect) || rect.Width < 2 || rect.Height < 2)
			{
				return null;
			}
			Bitmap bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
			bool printed = false;
			using (Graphics graphics = Graphics.FromImage(bitmap))
			{
				IntPtr hdc = graphics.GetHdc();
				try
				{
					printed = Win32.PrintWindow(hwnd, hdc, flags);
				}
				finally
				{
					graphics.ReleaseHdc(hdc);
				}
			}
			// 有些窗口 PrintWindow 返回 false 但其实已经画上了内容；只要不是纯色就接受
			if (!printed && LooksBlank(bitmap))
			{
				bitmap.Dispose();
				return null;
			}
			return bitmap;
		}

		/// <summary>采样判断是不是“纯黑/纯色”的空图（Chromium/UWP 用 PrintWindow 的典型结果）。</summary>
		private static bool LooksBlank(Bitmap bitmap)
		{
			if (bitmap == null || bitmap.Width < 2 || bitmap.Height < 2)
			{
				return true;
			}
			int min = 255;
			int max = 0;
			int dark = 0;
			int samples = 0;
			int stepX = Math.Max(1, bitmap.Width / 24);
			int stepY = Math.Max(1, bitmap.Height / 24);
			for (int y = 0; y < bitmap.Height; y += stepY)
			{
				for (int x = 0; x < bitmap.Width; x += stepX)
				{
					Color color;
					try
					{
						color = bitmap.GetPixel(x, y);
					}
					catch
					{
						continue;
					}
					int lum = (color.R * 299 + color.G * 587 + color.B * 114) / 1000;
					if (lum < min)
					{
						min = lum;
					}
					if (lum > max)
					{
						max = lum;
					}
					if (lum < 4)
					{
						dark++;
					}
					samples++;
				}
			}
			if (samples == 0)
			{
				return true;
			}
			return (max - min < 8) || (dark * 100 >= samples * 98);
		}

		private static byte[] TryBrowserCapture(IntPtr hwnd)
		{
			Func<IntPtr, byte[]> callback = BrowserCapture;
			if (callback == null)
			{
				return null;
			}
			try
			{
				return callback(hwnd);
			}
			catch (Exception ex)
			{
				Log.Dim("CDP 页面截图回退失败: " + ex.Message);
				return null;
			}
		}

		/// <summary>
		/// 最后兜底：被 DWM 合成、PrintWindow 拿不到的窗口，临时置前 0.2 秒再抓屏。
		/// 截完把原来的前台窗口还回去；目标原本最小化则恢复显示（模型后续可能要点击它）。
		/// </summary>
		private static Bitmap CaptureWindowFront(IntPtr hwnd, out bool wasMinimized)
		{
			wasMinimized = Win32.IsIconic(hwnd);
			IntPtr previous = Win32.GetForegroundWindow();
			try
			{
				if (wasMinimized)
				{
					Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
					Thread.Sleep(150);
				}
				// SetForegroundWindow 有前台锁：先模拟一次 ALT，解锁后再置前（EAgent 提示词里的做法）
				try
				{
					Win32.keybd_event(0x12, 0, 0, UIntPtr.Zero);
					Thread.Sleep(30);
					Win32.keybd_event(0x12, 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
				}
				catch
				{
				}
				Win32.BringWindowToTop(hwnd);
				Win32.SetForegroundWindow(hwnd);
				Thread.Sleep(260);

				Win32.RECT rect;
				if (!Win32.GetWindowRect(hwnd, out rect) || rect.Width < 2 || rect.Height < 2)
				{
					throw new Exception("读不到窗口尺寸");
				}
				Bitmap bitmap = new Bitmap(rect.Width, rect.Height, PixelFormat.Format24bppRgb);
				try
				{
					using (Graphics graphics = Graphics.FromImage(bitmap))
					{
						graphics.CopyFromScreen(rect.Left, rect.Top, 0, 0, new Size(rect.Width, rect.Height), CopyPixelOperation.SourceCopy);
					}
					return bitmap;
				}
				catch
				{
					bitmap.Dispose();
					throw;
				}
			}
			finally
			{
				if (previous != IntPtr.Zero && previous != hwnd)
				{
					try
					{
						Win32.SetForegroundWindow(previous);
					}
					catch
					{
					}
				}
			}
		}

		private static void DrawMouseMarker(Graphics graphics, Rectangle virtualScreen)
		{
			try
			{
				Point p = Cursor.Position;
				int x = p.X - virtualScreen.Left;
				int y = p.Y - virtualScreen.Top;
				using (SolidBrush brush = new SolidBrush(Color.FromArgb(150, 255, 220, 0)))
				using (Pen pen = new Pen(Color.FromArgb(220, 200, 120, 0), 1f))
				{
					graphics.FillEllipse(brush, x - 10, y - 10, 20, 20);
					graphics.DrawEllipse(pen, x - 10, y - 10, 20, 20);
				}
			}
			catch
			{
			}
		}

		private static void SaveJpeg(Bitmap bitmap, string path)
		{
			ImageCodecInfo jpeg = null;
			ImageCodecInfo[] codecs = ImageCodecInfo.GetImageEncoders();
			for (int i = 0; i < codecs.Length; i++)
			{
				if (codecs[i].FormatID == ImageFormat.Jpeg.Guid)
				{
					jpeg = codecs[i];
					break;
				}
			}
			if (jpeg == null)
			{
				bitmap.Save(path, ImageFormat.Jpeg);
				return;
			}
			using (EncoderParameters parameters = new EncoderParameters(1))
			{
				parameters.Param[0] = new EncoderParameter(System.Drawing.Imaging.Encoder.Quality, 85L);
				bitmap.Save(path, jpeg, parameters);
			}
		}

		// ---------------- 上传 ----------------

		public static ToolResult Upload(Settings s, string payload)
		{
			List<string> paths = ParsePaths(payload);
			if (paths.Count == 0)
			{
				return Fail("没有解析到文件路径。EAgent 写法：\r\n```agent_getfile\r\nagent_getfile_head('C:\\Users\\me\\Desktop\\1.png')agent_getfile_end\r\n```\r\n也兼容在 ```upload 块里一行写一个路径。");
			}
			List<string> existing = new List<string>();
			List<string> missing = new List<string>();
			for (int i = 0; i < paths.Count && existing.Count < 50; i++)
			{
				string path = paths[i];
				if (File.Exists(path))
				{
					existing.Add(path);
				}
				else
				{
					missing.Add(path);
				}
			}
			if (existing.Count == 0)
			{
				return Fail("这些文件都不存在：\r\n" + string.Join("\r\n", missing.ToArray()));
			}
			StringBuilder text = new StringBuilder();
			text.AppendLine("已把 " + existing.Count + " 个文件作为附件回传：");
			for (int i = 0; i < existing.Count; i++)
			{
				text.AppendLine("- " + existing[i]);
			}
			if (missing.Count > 0)
			{
				text.AppendLine("以下路径不存在，已跳过：");
				for (int i = 0; i < missing.Count; i++)
				{
					text.AppendLine("- " + missing[i]);
				}
			}
			text.Append("请查看附件后继续。");
			return Ok(text.ToString(), existing.ToArray());
		}

		public static List<string> ParsePaths(string payload)
		{
			List<string> paths = new List<string>();
			HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string text = payload ?? "";
			// EAgent 协议：只认 agent_getfile_head(...) 里的路径，避免把描述注释里的示例一起上传
			Match marker = EAgentFileArgs.Match(text);
			if (marker.Success)
			{
				text = marker.Groups["args"].Value;
			}
			foreach (Match quoted in QuotedPath.Matches(text))
			{
				string value = quoted.Groups[1].Success ? quoted.Groups[1].Value : quoted.Groups[2].Value;
				if (seen.Add(value))
				{
					paths.Add(value);
				}
			}
			foreach (Match raw in RawPath.Matches(text))
			{
				string value = raw.Groups[1].Value.Trim().TrimEnd(',', ';');
				if (seen.Add(value))
				{
					paths.Add(value);
				}
			}
			return paths;
		}

		// ---------------- 鼠标 ----------------

		public static ToolResult Mouse(string payload)
		{
			int x;
			int y;
			string action;
			if (!TryParseMouse(payload, out x, out y, out action))
			{
				return Fail("鼠标参数格式：EAgent 写法是 agent_mouse_simulate_1_start(x)、_2_start(y)、_3_start(动作)，动作 0=左键单击 1=左键双击 2=右键单击 3=右键双击。");
			}
			try
			{
				Win32.SetCursorPos(x, y);
				Thread.Sleep(60);
				if (action == "right")
				{
					ClickRight();
				}
				else if (action == "rightdouble")
				{
					ClickRight();
					Thread.Sleep(90);
					ClickRight();
				}
				else if (action == "double")
				{
					ClickLeft();
					Thread.Sleep(90);
					ClickLeft();
				}
				else
				{
					ClickLeft();
				}
				Point now = Cursor.Position;
				return Ok("已执行鼠标 " + action + "，目标 (" + x + "," + y + ")，当前光标 (" + now.X + "," + now.Y + ")。");
			}
			catch (Exception ex)
			{
				return Fail("鼠标操作失败：" + ex.Message);
			}
		}

		private static void ClickLeft()
		{
			Win32.mouse_event(Win32.MOUSEEVENTF_LEFTDOWN, 0, 0, 0, UIntPtr.Zero);
			Thread.Sleep(40);
			Win32.mouse_event(Win32.MOUSEEVENTF_LEFTUP, 0, 0, 0, UIntPtr.Zero);
		}

		private static void ClickRight()
		{
			Win32.mouse_event(Win32.MOUSEEVENTF_RIGHTDOWN, 0, 0, 0, UIntPtr.Zero);
			Thread.Sleep(40);
			Win32.mouse_event(Win32.MOUSEEVENTF_RIGHTUP, 0, 0, 0, UIntPtr.Zero);
		}

		/// <summary>解析 EAgent 的三行鼠标标记；不是这种格式时返回 false，走旧的 x,y,动作 兼容解析。</summary>
		private static bool TryParseEAgentMouse(string payload, out int x, out int y, out string action)
		{
			x = 0;
			y = 0;
			action = "left";
			int[] values = new int[4];
			bool[] seen = new bool[4];
			foreach (Match match in EAgentMouseArg.Matches(payload ?? ""))
			{
				int index;
				int value;
				if (int.TryParse(match.Groups["i"].Value, out index) && int.TryParse(match.Groups["v"].Value, out value) && index >= 1 && index <= 3)
				{
					values[index] = value;
					seen[index] = true;
				}
			}
			if (!seen[1] || !seen[2] || !seen[3])
			{
				return false;
			}
			int code = values[3];
			if (code == 0)
			{
				action = "left";
			}
			else if (code == 1)
			{
				action = "double";
			}
			else if (code == 2)
			{
				action = "right";
			}
			else if (code == 3)
			{
				action = "rightdouble";
			}
			else
			{
				return false;
			}
			x = values[1];
			y = values[2];
			return (x >= 0 && y >= 0 && x < 20000 && y < 20000);
		}

		public static bool TryParseMouse(string payload, out int x, out int y, out string action)
		{
			if (TryParseEAgentMouse(payload, out x, out y, out action))
			{
				return true;
			}
			x = 0;
			y = 0;
			action = "left";
			Match match = MouseArg.Match(payload ?? "");
			if (!match.Success)
			{
				return false;
			}
			if (!int.TryParse(match.Groups[1].Value, out x) || !int.TryParse(match.Groups[2].Value, out y))
			{
				return false;
			}
			if (match.Groups[3].Success)
			{
				string raw = match.Groups[3].Value.ToLowerInvariant();
				if (raw == "right" || raw == "右键")
				{
					action = "right";
				}
				else if (raw == "double" || raw == "双击")
				{
					action = "double";
				}
			}
			return (x >= 0 && y >= 0 && x < 20000 && y < 20000);
		}

		// ---------------- 键盘 ----------------

		public static ToolResult Keyboard(string payload)
		{
			int hwndValue;
			List<int> keys = ParseKeyboard(payload, out hwndValue);
			if (keys.Count == 0)
			{
				return Fail("键盘参数格式：EAgent 写法是 agent_keyboard_simulate_1_start(VK1)、_2_start(VK2)、_3_start(VK3)、_4_start(窗口句柄)，也兼容 vk1[,vk2[,vk3]][,窗口句柄]。例如 Win+R 是 91,82。");
			}
			try
			{
				if (hwndValue > 0)
				{
					IntPtr hwnd = new IntPtr(hwndValue);
					if (Win32.IsWindow(hwnd))
					{
						Win32.SetForegroundWindow(hwnd);
						Thread.Sleep(150);
					}
				}
				for (int i = 0; i < keys.Count; i++)
				{
					Win32.keybd_event((byte)keys[i], 0, 0, UIntPtr.Zero);
					Thread.Sleep(40);
				}
				for (int i = keys.Count - 1; i >= 0; i--)
				{
					Win32.keybd_event((byte)keys[i], 0, Win32.KEYEVENTF_KEYUP, UIntPtr.Zero);
					Thread.Sleep(40);
				}
				return Ok("已发送按键组合：" + string.Join("+", keys.ConvertAll(delegate(int v) { return v.ToString(); }).ToArray()) + "。");
			}
			catch (Exception ex)
			{
				return Fail("键盘操作失败：" + ex.Message);
			}
		}

		public static List<int> ParseKeyboard(string payload, out int hwnd)
		{
			hwnd = 0;
			List<int> eagentKeys = new List<int>();
			int[] eagentValues = new int[5];
			bool[] eagentSeen = new bool[5];
			foreach (Match match in EAgentKeyArg.Matches(payload ?? ""))
			{
				int index;
				int value;
				if (int.TryParse(match.Groups["i"].Value, out index) && int.TryParse(match.Groups["v"].Value, out value) && index >= 1 && index <= 4)
				{
					eagentValues[index] = value;
					eagentSeen[index] = true;
				}
			}
			if (eagentSeen[1])
			{
				for (int i = 1; i <= 3; i++)
				{
					if (eagentSeen[i] && eagentValues[i] >= 1 && eagentValues[i] <= 255)
					{
						eagentKeys.Add(eagentValues[i]);
					}
				}
				if (eagentSeen[4] && eagentValues[4] > 0)
				{
					hwnd = eagentValues[4];
				}
				return eagentKeys;
			}
			List<int> numbers = new List<int>();
			foreach (Match match in IntArg.Matches(payload ?? ""))
			{
				int value;
				if (int.TryParse(match.Value, out value))
				{
					numbers.Add(value);
				}
			}
			// 最后一位大于 255 视为窗口句柄（虚拟键码只有 0–255）
			if (numbers.Count >= 2 && numbers[numbers.Count - 1] > 255)
			{
				hwnd = numbers[numbers.Count - 1];
				numbers.RemoveAt(numbers.Count - 1);
			}
			if (numbers.Count > 3)
			{
				numbers = numbers.GetRange(0, 3);
			}
			for (int i = numbers.Count - 1; i >= 0; i--)
			{
				if (numbers[i] < 1 || numbers[i] > 255)
				{
					numbers.RemoveAt(i);
				}
			}
			return numbers;
		}

		// ---------------- 离线检查 ----------------

		/// <summary>安全的自检：只截一张图、解析参数、枚举窗口，不点击、不按键。</summary>
		public static string SelfCheck(Settings s)
		{
			StringBuilder report = new StringBuilder();
			ToolResult shot = Screenshot(s, "screen");
			if (!shot.Ok || shot.Attachments == null || shot.Attachments.Length == 0)
			{
				throw new Exception("截图失败: " + ((shot == null) ? "null" : shot.Text));
			}
			string file = shot.Attachments[0];
			if (!File.Exists(file))
			{
				throw new Exception("截图文件不存在: " + file);
			}
			using (Image image = Image.FromFile(file))
			{
				if (image.Width < 100 || image.Height < 100)
				{
					throw new Exception("截图尺寸异常: " + image.Width + "×" + image.Height);
				}
				report.AppendLine("截图: " + image.Width + "×" + image.Height + " -> " + file);
			}
			List<string> windows = Win32.ListWindows(20);
			if (windows.Count == 0)
			{
				throw new Exception("窗口枚举返回 0 个可见窗口");
			}
			report.AppendLine("可见顶层窗口: " + windows.Count + "（最多列 20 个）");
			int x;
			int y;
			string action;
			if (!TryParseMouse("200,300,double", out x, out y, out action) || x != 200 || y != 300 || action != "double")
			{
				throw new Exception("鼠标参数解析失败");
			}
			int hwnd;
			List<int> keys = ParseKeyboard("91,82", out hwnd);
			if (keys.Count != 2 || keys[0] != 91 || keys[1] != 82 || hwnd != 0)
			{
				throw new Exception("键盘参数解析失败");
			}
			report.AppendLine("鼠标/键盘参数解析: OK（未实际点击/按键）");
			return report.ToString();
		}
	}
}
