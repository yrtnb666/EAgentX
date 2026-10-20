using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace EAgentX.Net
{
	/// <summary>
	/// 测试版新增的 Win32 封装：窗口查找 / 置入、截图、鼠标、键盘。
	/// 只做最薄的 P/Invoke，业务判断放在 ToolRunner 和 BrowserHost 里。
	/// </summary>
	internal static class Win32
	{
		public const int GWL_STYLE = -16;

		public const int WS_CHILD = 0x40000000;

		public const int WS_CAPTION = 0x00C00000;

		public const int WS_THICKFRAME = 0x00040000;

		public const int WS_POPUP = unchecked((int)0x80000000);

		public const int SW_SHOW = 5;

		public const int SW_MINIMIZE = 6;

		public const int SW_RESTORE = 9;

		public const uint MOUSEEVENTF_LEFTDOWN = 0x0002;

		public const uint MOUSEEVENTF_LEFTUP = 0x0004;

		public const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;

		public const uint MOUSEEVENTF_RIGHTUP = 0x0010;

		public const uint KEYEVENTF_KEYUP = 0x0002;

		public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

		[StructLayout(LayoutKind.Sequential)]
		public struct RECT
		{
			public int Left;

			public int Top;

			public int Right;

			public int Bottom;

			public int Width
			{
				get
				{
					return Right - Left;
				}
			}

			public int Height
			{
				get
				{
					return Bottom - Top;
				}
			}
		}

		[DllImport("user32.dll")]
		public static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern int GetClassName(IntPtr hWnd, StringBuilder name, int maxCount);

		[DllImport("user32.dll", CharSet = CharSet.Unicode)]
		public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int maxCount);

		[DllImport("user32.dll")]
		public static extern int GetWindowThreadProcessId(IntPtr hWnd, out int processId);

		[DllImport("user32.dll")]
		public static extern bool IsWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool IsWindowVisible(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern IntPtr SetParent(IntPtr child, IntPtr newParent);

		[DllImport("user32.dll")]
		public static extern IntPtr GetParent(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool MoveWindow(IntPtr hWnd, int x, int y, int width, int height, bool repaint);

		[DllImport("user32.dll")]
		public static extern int GetWindowLong(IntPtr hWnd, int index);

		[DllImport("user32.dll")]
		public static extern int SetWindowLong(IntPtr hWnd, int index, int value);

		[DllImport("user32.dll")]
		public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);

		[DllImport("user32.dll")]
		public static extern bool ShowWindow(IntPtr hWnd, int command);

		[DllImport("user32.dll")]
		public static extern bool SetForegroundWindow(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		public static extern bool BringWindowToTop(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool IsIconic(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern bool SetCursorPos(int x, int y);

		[DllImport("user32.dll")]
		public static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);

		[DllImport("user32.dll")]
		public static extern void keybd_event(byte virtualKey, byte scanCode, uint flags, UIntPtr extraInfo);

		[DllImport("user32.dll")]
		public static extern bool PrintWindow(IntPtr hWnd, IntPtr hdc, uint flags);

		[DllImport("user32.dll")]
		public static extern IntPtr GetWindowDC(IntPtr hWnd);

		[DllImport("user32.dll")]
		public static extern int ReleaseDC(IntPtr hWnd, IntPtr hdc);

		/// <summary>
		/// 找浏览器窗口：优先按进程 ID 匹配，进程 ID 不可用时退化为「类名 + 标题包含 DeepSeek」。
		/// 返回 IntPtr.Zero 表示没找到。
		/// </summary>
		public static IntPtr FindBrowserWindow(int processId, string titlePart)
		{
			IntPtr exact = IntPtr.Zero;
			IntPtr byTitle = IntPtr.Zero;
			EnumWindowsProc callback = delegate(IntPtr hWnd, IntPtr lParam)
			{
				try
				{
					if (!IsWindowVisible(hWnd))
					{
						return true;
					}
					StringBuilder className = new StringBuilder(256);
					GetClassName(hWnd, className, className.Capacity);
					string cls = className.ToString();
					if (!string.Equals(cls, "Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) &&
						!string.Equals(cls, "Chrome_WidgetWin_0", StringComparison.OrdinalIgnoreCase))
					{
						return true;
					}
					int pid;
					GetWindowThreadProcessId(hWnd, out pid);
					if (processId > 0 && pid == processId)
					{
						exact = hWnd;
						return false;
					}
					if (byTitle == IntPtr.Zero && !string.IsNullOrEmpty(titlePart))
					{
						StringBuilder title = new StringBuilder(512);
						GetWindowText(hWnd, title, title.Capacity);
						string text = title.ToString();
						if (text.IndexOf(titlePart, StringComparison.OrdinalIgnoreCase) >= 0 &&
							text.IndexOf("Microsoft Edge", StringComparison.OrdinalIgnoreCase) < 0)
						{
							// --app 窗口的标题就是页面标题；优先选最短的，避免命中普通浏览器窗口
							if (byTitle == IntPtr.Zero || text.Length < GetTitle(byTitle).Length)
							{
								byTitle = hWnd;
							}
						}
					}
				}
				catch
				{
				}
				return true;
			};
			try
			{
				EnumWindows(callback, IntPtr.Zero);
			}
			catch
			{
			}
			GC.KeepAlive(callback);
			return (exact != IntPtr.Zero) ? exact : byTitle;
		}

		/// <summary>把所有可见的顶层窗口列出来，自检/排错用。</summary>
		public static List<string> ListWindows(int max)
		{
			List<string> list = new List<string>();
			EnumWindowsProc callback = delegate(IntPtr hWnd, IntPtr lParam)
			{
				if (!IsWindowVisible(hWnd))
				{
					return true;
				}
				try
				{
					StringBuilder title = new StringBuilder(256);
					StringBuilder cls = new StringBuilder(128);
					GetWindowText(hWnd, title, title.Capacity);
					GetClassName(hWnd, cls, cls.Capacity);
					int pid;
					GetWindowThreadProcessId(hWnd, out pid);
					list.Add(pid + " | " + cls + " | " + title);
				}
				catch
				{
				}
				return list.Count < max;
			};
			try
			{
				EnumWindows(callback, IntPtr.Zero);
			}
			catch
			{
			}
			GC.KeepAlive(callback);
			return list;
		}

		public static string GetTitle(IntPtr hWnd)
		{
			try
			{
				StringBuilder title = new StringBuilder(512);
				GetWindowText(hWnd, title, title.Capacity);
				return title.ToString();
			}
			catch
			{
				return "";
			}
		}

		public static string GetClass(IntPtr hWnd)
		{
			try
			{
				StringBuilder cls = new StringBuilder(256);
				GetClassName(hWnd, cls, cls.Capacity);
				return cls.ToString();
			}
			catch
			{
				return "";
			}
		}
	}
}
