using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using EAgentX.Util;
using Microsoft.Win32;

namespace EAgentX.Net
{
	/// <summary>
	/// 浏览器启动器：探测 msedge.exe / chrome.exe，用独立 profile 和远程调试端口启动，
	/// 再通过 http://127.0.0.1:端口/json/list 找到聊天页面的 WebSocket 地址。
	/// </summary>
	public class EdgeLauncher : IDisposable
	{
		private readonly Settings S;

		private readonly HttpClient Http;

		private readonly string Base;

		public string BrowserPath { get; private set; }

		/// <summary>本次启动的浏览器主进程 ID（复用时为 0）；窗口置入用它找窗口。</summary>
		public int ProcessId { get; private set; }

		public string DebugBase
		{
			get
			{
				return Base;
			}
		}

		public EdgeLauncher(Settings settings)
		{
			S = settings;
			Base = "http://127.0.0.1:" + S.CdpPort;
			Http = new HttpClient();
			Http.Timeout = TimeSpan.FromSeconds(3.0);
		}

		/// <summary>调试端口是否已经在监听（可能是本程序上次留下的浏览器）。</summary>
		public bool IsDebuggerReady()
		{
			try
			{
				string version = Http.GetStringAsync(Base + "/json/version").Result;
				return !string.IsNullOrEmpty(version);
			}
			catch
			{
				return false;
			}
		}

		public string ResolveBrowser()
		{
			if (!string.IsNullOrEmpty(S.EdgePath) && File.Exists(S.EdgePath))
			{
				return S.EdgePath;
			}
			List<string> candidates = new List<string>();
			string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
			string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
			string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
			candidates.Add(Path.Combine(programFilesX86, "Microsoft\\Edge\\Application\\msedge.exe"));
			candidates.Add(Path.Combine(programFiles, "Microsoft\\Edge\\Application\\msedge.exe"));
			candidates.Add(Path.Combine(localAppData, "Microsoft\\Edge\\Application\\msedge.exe"));
			candidates.Add(Path.Combine(programFiles, "Google\\Chrome\\Application\\chrome.exe"));
			candidates.Add(Path.Combine(programFilesX86, "Google\\Chrome\\Application\\chrome.exe"));
			candidates.Add(Path.Combine(localAppData, "Google\\Chrome\\Application\\chrome.exe"));
			foreach (string candidate in candidates)
			{
				if (File.Exists(candidate))
				{
					return candidate;
				}
			}
			// 常见路径都没有时查注册表 App Paths
			string registered = LookupAppPath("msedge.exe");
			if (registered != null)
			{
				return registered;
			}
			registered = LookupAppPath("chrome.exe");
			if (registered != null)
			{
				return registered;
			}
			return null;
		}

		private static string LookupAppPath(string exe)
		{
			try
			{
				using (RegistryKey appPath = Registry.LocalMachine.OpenSubKey("SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\App Paths\\" + exe))
				{
					if (appPath != null)
					{
						object value = appPath.GetValue(null);
						if (value != null && File.Exists(value.ToString()))
						{
							return value.ToString();
						}
					}
				}
			}
			catch
			{
			}
			return null;
		}

		/// <summary>端口没开就启动浏览器，最多等 25 秒。</summary>
		public void EnsureStarted()
		{
			if (IsDebuggerReady())
			{
				Log.Dim("调试端口 " + S.CdpPort + " 已在监听，复用现有浏览器。");
				return;
			}
			BrowserPath = ResolveBrowser();
			if (BrowserPath == null)
			{
				throw new Exception("没有找到 Edge 或 Chrome，请在设置里手动指定浏览器路径。");
			}
			Directory.CreateDirectory(Settings.ProfileDir);
			// 兼容启动参数 + --app 无边框窗口（便于嵌入主窗口）
			string stealth = "";
			string target = "\"" + S.StartUrl + "\"";
			if (S.StealthFlags)
			{
				stealth = "--disable-blink-features=AutomationControlled --test-type ";
				target = "--app=\"" + S.StartUrl + "\"";
			}
			string arguments = stealth + "--remote-debugging-port=" + S.CdpPort + " --remote-allow-origins=* --user-data-dir=\"" + Settings.ProfileDir + "\" --no-first-run --no-default-browser-check --disable-sync --disable-features=msEdgeFirstRunExperience,msEdgeSidebarV2 " + target;
			Log.Info("启动浏览器: " + BrowserPath + (S.StealthFlags ? "（兼容参数 + --app）" : ""));
			ProcessStartInfo startInfo = new ProcessStartInfo();
			startInfo.FileName = BrowserPath;
			startInfo.Arguments = arguments;
			startInfo.UseShellExecute = false;
			startInfo.CreateNoWindow = false;
			Process browser = Process.Start(startInfo);
			ProcessId = ((browser == null) ? 0 : browser.Id);
			Stopwatch watch = Stopwatch.StartNew();
			while (watch.ElapsedMilliseconds < 25000)
			{
				if (IsDebuggerReady())
				{
					return;
				}
				Thread.Sleep(400);
			}
			throw new TimeoutException("浏览器已启动，但调试端口 " + S.CdpPort + " 未就绪。请检查是否被安全软件拦截。");
		}

		/// <summary>等一个可用的聊天页面；10 秒还没找到就主动新建标签页。</summary>
		public async Task<string> WaitForChatTargetAsync(int timeoutMs, CancellationToken ct)
		{
			Stopwatch watch = Stopwatch.StartNew();
			bool openedNewTab = false;
			while (watch.ElapsedMilliseconds < timeoutMs)
			{
				ct.ThrowIfCancellationRequested();
				string wsUrl = PickChat(await ListPagesAsync());
				if (wsUrl != null)
				{
					return wsUrl;
				}
				if (!openedNewTab && watch.ElapsedMilliseconds > 10000)
				{
					openedNewTab = true;
					await OpenTabAsync(S.StartUrl);
				}
				await Task.Delay(600, ct);
			}
			throw new TimeoutException("等待 DeepSeek 页面超时，请确认浏览器已经打开 " + S.StartUrl);
		}

		/// <summary>优先返回 deepseek.com 的页面，没有就返回第一个普通页面。</summary>
		private string PickChat(List<Dictionary<string, object>> pages)
		{
			string fallback = null;
			foreach (Dictionary<string, object> page in pages)
			{
				string type = Json.GetStr(page, "type");
				if (type != "page")
				{
					continue;
				}
				string url = Json.GetStr(page, "url") ?? "";
				string wsUrl = Json.GetStr(page, "webSocketDebuggerUrl");
				if (!string.IsNullOrEmpty(wsUrl))
				{
					if (url.IndexOf("deepseek.com", StringComparison.OrdinalIgnoreCase) >= 0)
					{
						return wsUrl;
					}
					if (fallback == null)
					{
						fallback = wsUrl;
					}
				}
			}
			return fallback;
		}

		private async Task<List<Dictionary<string, object>>> ListPagesAsync()
		{
			List<Dictionary<string, object>> pages = new List<Dictionary<string, object>>();
			try
			{
				List<object> entries = Json.List(Json.Decode(await Http.GetStringAsync(Base + "/json/list")));
				if (entries != null)
				{
					foreach (object entry in entries)
					{
						Dictionary<string, object> page = Json.Obj(entry);
						if (page != null)
						{
							pages.Add(page);
						}
					}
				}
			}
			catch
			{
			}
			return pages;
		}

		/// <summary>新版 Chromium 用 PUT /json/new，旧版用 GET，两个都试。</summary>
		private async Task OpenTabAsync(string url)
		{
			string target = Base + "/json/new?" + Uri.EscapeDataString(url);
			try
			{
				HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, target);
				if ((await Http.SendAsync(request)).IsSuccessStatusCode)
				{
					return;
				}
			}
			catch
			{
			}
			try
			{
				await Http.GetStringAsync(target);
			}
			catch
			{
			}
		}

		public void Dispose()
		{
			try
			{
				Http.Dispose();
			}
			catch
			{
			}
		}
	}
}
