using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAgentX.Net;
using EAgentX.Util;

namespace EAgentX.Engines
{

	public class WebEngine : IChatEngine, IDisposable
	{
		private const string InputMissingMarker = "输入框: 未找到";

		private const string Helper = "\n(function () {\n  try {\n    var CFG = __CFG__;\n    var old = window.__eagentx;\n    if (old && old.v === 10) { old.cfg = CFG; return 'ok'; }\n\n    var E = { v: 10, cfg: CFG };\n\n    E.all = function (sel) {\n      try { return Array.prototype.slice.call(document.querySelectorAll(sel)); } catch (e) { return []; }\n    };\n\n    E.vis = function (el) {\n      if (!el) return false;\n      var r = el.getBoundingClientRect();\n      if (r.width < 2 || r.height < 2) return false;\n      var st = window.getComputedStyle(el);\n      return st.visibility !== 'hidden' && st.display !== 'none' && parseFloat(st.opacity || '1') > 0.01;\n    };\n\n    E.first = function (sel) {\n      var a = E.all(sel);\n      for (var i = 0; i < a.length; i++) { if (E.vis(a[i])) return a[i]; }\n      return null;\n    };\n\n    E.norm = function (s) {\n      return (s || '').replace(/\\s+/g, ' ').trim();\n    };\n\n    E.msgCount = function () {\n      return E.all('.ds-message').length;\n    };\n\n    E.userSent = function (text) {\n      var needle = E.norm(text);\n      if (!needle) return false;\n      var ms = E.all('.ds-message');\n      for (var i = ms.length - 1; i >= 0 && i >= ms.length - 4; i--) {\n        if (ms[i].querySelector('.ds-markdown')) continue;\n        var t = E.norm(ms[i].innerText || '');\n        if (t === needle || (t.length > 40 && needle.indexOf(t) === 0)) return true;\n      }\n      return false;\n    };\n\n    E.latestReply = function () {\n      var ms = E.all('.ds-message');\n      if (!ms.length) {\n        var rs = E.replies();\n        return rs.length ? E.serialize(rs[rs.length - 1]) : '';\n      }\n      for (var i = ms.length - 1; i >= 0; i--) {\n        if (!E.vis(ms[i])) continue;\n        var md = ms[i].querySelector('.ds-markdown');\n        if (md) return E.serialize(md);\n        if (E.norm(ms[i].innerText || '').length > 0) return '';\n      }\n      return '';\n    };\n\n    E.input = function () {\n      var el = E.first(E.cfg.input);\n      if (el) return el;\n      var t = E.all('textarea');\n      for (var i = 0; i < t.length; i++) { if (E.vis(t[i])) return t[i]; }\n      var e = E.all('[contenteditable=\"true\"]');\n      for (var j = 0; j < e.length; j++) { if (E.vis(e[j])) return e[j]; }\n      return null;\n    };\n\n    E.sendBtn = function () {\n      var b = E.first(E.cfg.send);\n      if (b && !b.disabled && b.getAttribute('aria-disabled') !== 'true') return b;\n\n      var el = E.input();\n      if (!el) return null;\n      var ir = el.getBoundingClientRect();\n      var c = E.all('button,div[role=\"button\"],div[class*=\"button\"]');\n      var best = null, bestScore = -1000;\n\n      for (var i = 0; i < c.length; i++) {\n        var x = c[i];\n        if (!E.vis(x)) continue;\n        if (x.disabled || x.getAttribute('aria-disabled') === 'true') continue;\n\n        var txt = ((x.getAttribute('aria-label') || '') + ' ' + (x.getAttribute('title') || '') + ' ' + (x.innerText || ''))\n          .replace(/\\s+/g, ' ').trim();\n        if (/attach|upload|附件|上传|文件|deep|思考|search|联网|语音|voice/i.test(txt)) continue;\n\n        var r = x.getBoundingClientRect();\n        var score = 0;\n        if (/send|发送|submit/i.test(txt)) score += 100;\n        if ((x.className || '').toString().indexOf('primary') >= 0) score += 25;\n        var dy = Math.abs((r.top + r.height / 2) - (ir.top + ir.height / 2));\n        var dx = r.left - ir.right;\n        if (dx > -60 && dx < 260) score += 20;\n        if (dy < ir.height) score += 20;\n        score -= Math.min(60, dy / 8);\n\n        if (score > bestScore) { bestScore = score; best = x; }\n      }\n      return bestScore >= 30 ? best : null;\n    };\n\n    E.replies = function () {\n      var a = E.all(E.cfg.reply);\n      var keep = [];\n      for (var i = 0; i < a.length; i++) {\n        if (!E.vis(a[i])) continue;\n        var nested = false;\n        for (var j = 0; j < a.length; j++) {\n          if (i !== j && a[j] && a[j] !== a[i] && a[j].contains(a[i])) { nested = true; break; }\n        }\n        if (!nested) keep.push(a[i]);\n      }\n      return keep;\n    };\n\n    E.serialize = function (el) {\n      var clone = el.cloneNode(true);\n      var pres = clone.querySelectorAll('pre');\n      for (var i = 0; i < pres.length; i++) {\n        var pre = pres[i];\n        var code = pre.querySelector('code');\n        var lang = '';\n        if (code) {\n          var cls = ((code.className || '') + ' ' + (pre.className || '')).toString();\n          var m = /language-([a-z0-9+#_-]+)/i.exec(cls);\n          if (m) lang = m[1];\n        }\n        if (!lang) {\n          var prev = pre.previousElementSibling;\n          if (prev) {\n            var t = (prev.innerText || '').trim();\n            var lm = /[A-Za-z0-9+#_-]+/.exec(t);\n            if (lm && t.length < 40) lang = lm[0].toLowerCase();\n          }\n        }\n        var content = code ? (code.innerText || code.textContent || '') : (pre.innerText || '');\n        var block = '\\n```' + lang + '\\n' + content.replace(/\\s+$/, '') + '\\n```\\n';\n        pre.parentNode.replaceChild(document.createTextNode(block), pre);\n      }\n      return (clone.innerText || clone.textContent || '').trim();\n    };\n\n    E.replyText = function (idx) {\n      var rs = E.replies();\n      if (idx < 0) idx = rs.length + idx;\n      if (idx < 0 || idx >= rs.length) return '';\n      var t = E.serialize(rs[idx]);\n      return t.replace(/\\u00a0/g, ' ').trim();\n    };\n\n    E.generating = function () {\n      // DeepSeek 生成时右侧主按钮会从“发送箭头”换成“停止方块”。\n      // 注意：不能先看 disabled 类——服务端慢时停止按钮也可能短暂带 disabled。\n      // 图标是停止方块就一定在生成；图标还没换过来时，非 disabled 也按生成中处理。\n      var primary = E.first('.ds-button--primary');\n      if (primary) {\n        var cls = String(primary.className || '');\n        var svg = primary.querySelector('svg');\n        var icon = svg ? (svg.innerHTML || '') : '';\n        if (icon.indexOf('M2 4.88') >= 0) return true;   // 停止方块\n        if (icon.indexOf('M8.3125') >= 0) return false; // 发送箭头\n        if (cls.indexOf('disabled') >= 0) return false;\n        return true;\n      }\n\n      if (E.first(E.cfg.generating)) return true;\n      var c = E.all('div[role=\"button\"],button');\n      for (var i = 0; i < c.length; i++) {\n        if (!E.vis(c[i])) continue;\n        var t = (c[i].getAttribute('aria-label') || '') + ' ' + (c[i].className || '');\n        var s = c[i].querySelector('svg');\n        var ic = s ? (s.innerHTML || '') : '';\n        if (ic.indexOf('M2 4.88') >= 0) return true;\n        if (/stop|停止|中止/i.test(t)) return true;\n      }\n      return false;\n    };\n\n    // 一次返回：回复文本、是否找到助手消息、消息底部动作栏按钮数、是否仍在生成。\n    // 动作栏（复制/重新生成等）只在消息完成后出现，比单纯看按钮更可靠。\n    E.replyState = function () {\n      var ms = E.all('.ds-message');\n      for (var i = ms.length - 1; i >= 0; i--) {\n        if (!E.vis(ms[i])) continue;\n        var md = ms[i].querySelector('.ds-markdown');\n        if (md) {\n          var actions = 0;\n          var p = ms[i].parentElement;\n          if (p) {\n            for (var k = 0; k < p.children.length; k++) {\n              var sib = p.children[k];\n              if (sib === ms[i]) continue;\n              actions += sib.querySelectorAll('button,div[role=\"button\"]').length;\n            }\n          }\n          return {\n            has: true,\n            text: E.serialize(md),\n            actions: actions,\n            messages: ms.length,\n            generating: E.generating()\n          };\n        }\n        if (E.norm(ms[i].innerText || '').length > 0) {\n          return { has: false, text: '', actions: 0, messages: ms.length, generating: E.generating() };\n        }\n      }\n      return { has: false, text: '', actions: 0, messages: ms.length, generating: E.generating() };\n    };\n\n    E.rateLimit = function () {\n      var ms = E.all('.ds-message');\n      if (!ms.length) return false;\n      var last = ms[ms.length - 1];\n      var t = (last.parentElement ? last.parentElement.innerText : last.innerText) || '';\n      return t.indexOf('消息发送过于频繁') >= 0 ||\n             t.indexOf('请求过于频繁') >= 0 ||\n             t.indexOf('发送太频繁') >= 0;\n    };\n\n    E.beforeSend = function () {\n      var rs = E.replies();\n      return { count: rs.length, messages: E.msgCount(), sample: rs.length ? (rs[rs.length - 1].innerText || '').slice(-200) : '' };\n    };\n\n    E.setInput = function (text) {\n      var el = E.input();\n      if (!el) return { ok: false, reason: '找不到输入框' };\n      el.focus();\n      var tag = (el.tagName || '').toUpperCase();\n      if (tag === 'TEXTAREA' || tag === 'INPUT') {\n        var proto = tag === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;\n        var d = Object.getOwnPropertyDescriptor(proto, 'value');\n        if (d && d.set) d.set.call(el, text); else el.value = text;\n        el.dispatchEvent(new Event('input', { bubbles: true }));\n        el.dispatchEvent(new Event('change', { bubbles: true }));\n      } else {\n        el.innerHTML = '';\n        var sel = window.getSelection();\n        var range = document.createRange();\n        range.selectNodeContents(el);\n        sel.removeAllRanges();\n        sel.addRange(range);\n        var ok = false;\n        try { ok = document.execCommand('insertText', false, text); } catch (e1) { ok = false; }\n        if (!ok) {\n          el.textContent = text;\n          try { el.dispatchEvent(new InputEvent('input', { bubbles: true, data: text, inputType: 'insertText' })); }\n          catch (e2) { el.dispatchEvent(new Event('input', { bubbles: true })); }\n        }\n        sel.removeAllRanges();\n      }\n      var cur = (tag === 'TEXTAREA' || tag === 'INPUT') ? el.value : (el.innerText || '');\n      return { ok: true, length: (cur || '').length, tag: tag };\n    };\n\n    E.inputEmpty = function () {\n      var el = E.input();\n      if (!el) return true;\n      var tag = (el.tagName || '').toUpperCase();\n      var v = (tag === 'TEXTAREA' || tag === 'INPUT') ? el.value : (el.innerText || '');\n      return !v || v.replace(/\\s+/g, '').length === 0;\n    };\n\n    E.clickSend = function () {\n      var b = E.sendBtn();\n      if (b) { b.click(); return 'click'; }\n      var el = E.input();\n      if (!el) return 'no-input';\n      el.focus();\n      var o = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true, composed: true };\n      ['keydown', 'keypress', 'keyup'].forEach(function (t) {\n        try { el.dispatchEvent(new KeyboardEvent(t, o)); } catch (e) {}\n      });\n      return 'enter';\n    };\n\n    E.buttons = function () {\n      var a = E.all('button,div[role=\"button\"],a[role=\"button\"]');\n      var out = [];\n      for (var i = 0; i < a.length && out.length < 12; i++) {\n        if (!E.vis(a[i])) continue;\n        var label = (a[i].getAttribute('aria-label') || a[i].getAttribute('title') || '').replace(/\\s+/g, ' ').slice(0, 24);\n        var text = (a[i].innerText || '').replace(/\\s+/g, ' ').trim().slice(0, 16);\n        var cls = (a[i].className || '').toString().slice(0, 32);\n        out.push(a[i].tagName + '|' + cls + '|' + label + '|' + text);\n      }\n      return out;\n    };\n\n    E.classes = function () {\n      var set = {}, out = [];\n      var a = E.all('[class]');\n      for (var i = 0; i < a.length && out.length < 18; i++) {\n        var c = (a[i].className || '').toString();\n        if (/markdown|message|chat|answer|reply/i.test(c) && !set[c]) {\n          set[c] = 1;\n          out.push(c.slice(0, 60));\n        }\n      }\n      return out;\n    };\n\n    E.diag = function () {\n      var inp = E.input();\n      var b = E.sendBtn();\n      return {\n        url: location.href,\n        title: document.title,\n        input: !!inp,\n        inputTag: inp ? inp.tagName : '',\n        placeholder: inp ? (inp.getAttribute('placeholder') || '') : '',\n        send: !!b,\n        sendLabel: b ? (((b.getAttribute('aria-label') || b.getAttribute('title') || '') + ' ' + (b.className || '')).slice(0, 120)) : '',\n        replies: E.replies().length,\n        generating: E.generating(),\n        login: !!document.querySelector('input[type=\"password\"]'),\n        buttons: E.buttons(),\n        classes: E.classes(),\n        sample: (document.body ? document.body.innerText : '').replace(/\\s+/g, ' ').slice(0, 160)\n      };\n    };\n\n    window.__eagentx = E;\n    return 'ok';\n  } catch (err) {\n    return 'err:' + err;\n  }\n})()\n";

		private readonly Settings S;

		private EdgeLauncher Launcher;

		private CdpClient Cdp;

		public string Name
		{
			get
			{
				return "DeepSeek 网页版（CDP）";
			}
		}

		public string Diagnostics { get; private set; }

		/// <summary>本次启动的浏览器进程 ID（复用时为 0）。</summary>
		public int BrowserProcessId
		{
			get
			{
				return (Launcher == null) ? 0 : Launcher.ProcessId;
			}
		}

		public WebEngine(Settings settings)
		{
			S = settings;
			Diagnostics = "";
		}

		/// <summary>
		/// 截图工具的浏览器兜底：用 CDP 的 Page.captureScreenshot 直接取页面渲染结果。
		/// 它不经过屏幕合成，所以浏览器被别的软件盖住、甚至最小化时也能截到。
		/// 不是本程序启动/连接的浏览器窗口时返回 null，交给 ToolRunner 走其它截图方案。
		/// </summary>
		public byte[] TryCaptureWindow(IntPtr hwnd)
		{
			if (Cdp == null || !IsOurBrowserWindow(hwnd))
			{
				return null;
			}
			try
			{
				Dictionary<string, object> parameters = new Dictionary<string, object>
				{
					{ "format", "jpeg" },
					{ "quality", 85 },
					{ "fromSurface", true },
					{ "captureBeyondViewport", false }
				};
				Dictionary<string, object> response = Cdp.SendAsync("Page.captureScreenshot", parameters, 20000).GetAwaiter().GetResult();
				Dictionary<string, object> result = Json.Obj(Json.Get(response, "result"));
				string data = Json.GetStr(result, "data");
				if (string.IsNullOrEmpty(data))
				{
					return null;
				}
				return Convert.FromBase64String(data);
			}
			catch (Exception ex)
			{
				Log.Dim("CDP 页面截图失败: " + ex.Message);
				return null;
			}
		}

		private bool IsOurBrowserWindow(IntPtr hwnd)
		{
			if (hwnd == IntPtr.Zero)
			{
				return false;
			}
			int pid;
			Win32.GetWindowThreadProcessId(hwnd, out pid);
			if (BrowserProcessId > 0)
			{
				return pid == BrowserProcessId;
			}
			string cls = Win32.GetClass(hwnd);
			if (!string.Equals(cls, "Chrome_WidgetWin_1", StringComparison.OrdinalIgnoreCase) &&
				!string.Equals(cls, "Chrome_WidgetWin_0", StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}
			string title = Win32.GetTitle(hwnd);
			return title.IndexOf("DeepSeek", StringComparison.OrdinalIgnoreCase) >= 0;
		}

		public async Task<string> StartAsync(CancellationToken ct)
		{
			if (Cdp != null && !string.IsNullOrEmpty(Diagnostics))
			{
				return Diagnostics;
			}
			string diag = await ConnectAsync(ct);
			if (diag.IndexOf(InputMissingMarker, StringComparison.Ordinal) >= 0)
			{
				// 还停在登录页：给用户时间在浏览器里登录，而不是立刻报「找不到输入框」
				if (diag.IndexOf("检测到登录表单", StringComparison.Ordinal) >= 0)
				{
					diag = await WaitForLoginAsync(ct);
				}
				if (diag.IndexOf(InputMissingMarker, StringComparison.Ordinal) >= 0)
				{
					throw new Exception("没有在页面上找到聊天输入框。\r\n请先在打开的浏览器窗口里登录 " + S.StartUrl + "，进入对话页后点「重新检测」。\r\n\r\n" + diag);
				}
			}
			return diag;
		}

		/// <summary>DeepSeek 登录页的地址特征（登录页不一定有密码框，微信/短信登录都没有）。</summary>
		private static bool LooksLikeLoginUrl(string url)
		{
			if (string.IsNullOrEmpty(url))
			{
				return false;
			}
			string lower = url.ToLowerInvariant();
			return lower.IndexOf("/sign_in", StringComparison.Ordinal) >= 0 || lower.IndexOf("/signin", StringComparison.Ordinal) >= 0 || lower.IndexOf("/sign-in", StringComparison.Ordinal) >= 0 || lower.IndexOf("/login", StringComparison.Ordinal) >= 0;
		}

		/// <summary>
		/// 页面停留在登录页时，最多等 2 分钟让用户完成登录。
		/// 登录成功会整页刷新，window.__eagentx 丢失，所以每轮都要重新注入再判断。
		/// </summary>
		private async Task<string> WaitForLoginAsync(CancellationToken ct)
		{
			const int LoginWaitMs = 120000;
			Log.Info("浏览器还停在登录页，请在打开的窗口里登录 DeepSeek；程序最多等 " + (LoginWaitMs / 1000) + " 秒…");
			Stopwatch watch = Stopwatch.StartNew();
			string diag = Diagnostics;
			int round = 0;
			while (watch.ElapsedMilliseconds < LoginWaitMs)
			{
				ct.ThrowIfCancellationRequested();
				await Task.Delay(2500, ct);
				round++;
				try
				{
					object injected = await Cdp.EvaluateAsync(BuildHelper(), 15000);
					if (injected == null || injected.ToString() != "ok")
					{
						continue;
					}
					diag = await ReadDiagnosticsAsync();
					if (diag.IndexOf(InputMissingMarker, StringComparison.Ordinal) < 0)
					{
						Log.Info("检测到页面已进入对话状态，继续连接。");
						return diag;
					}
					if (round % 8 == 0)
					{
						Log.Dim("仍在等待登录…（已等 " + (watch.ElapsedMilliseconds / 1000) + " 秒）");
					}
				}
				catch (OperationCanceledException)
				{
					throw;
				}
				catch
				{
					// 页面正在跳转时注入会失败，下一轮再试
				}
			}
			return diag;
		}

		public async Task<string> ProbeAsync(CancellationToken ct)
		{
			return await ConnectAsync(ct);
		}

		private async Task<string> ConnectAsync(CancellationToken ct)
		{
			Launcher = new EdgeLauncher(S);
			Launcher.EnsureStarted();
			string ws = await Launcher.WaitForChatTargetAsync(90000, ct);
			Cdp = new CdpClient();
			await Cdp.ConnectAsync(ws, 15000);
			await Cdp.SendAsync("Runtime.enable", null, 8000);
			await Cdp.SendAsync("Page.enable", null, 8000);
			await EnsureStartUrlAsync(ct);
			object injected = await Cdp.EvaluateAsync(BuildHelper(), 15000);
			string status = ((injected == null) ? "" : injected.ToString());
			if (status != "ok")
			{
				throw new Exception("注入页面脚本失败: " + status);
			}
			Diagnostics = await ReadDiagnosticsAsync();
			return Diagnostics;
		}

		public async Task ResetAsync(CancellationToken ct)
		{
			if (Cdp == null)
			{
				throw new Exception("网页引擎还没有连接。");
			}
			Dictionary<string, object> navParams = new Dictionary<string, object> { { "url", S.StartUrl } };
			await Cdp.SendAsync("Page.navigate", navParams, 15000);
			for (int i = 0; i < 60; i++)
			{
				ct.ThrowIfCancellationRequested();
				await Task.Delay(500, ct);
				object state;
				try
				{
					state = await Cdp.EvaluateAsync("document.readyState", 8000);
				}
				catch
				{
					state = null;
				}
				if (state != null && state.ToString() == "complete")
				{
					break;
				}
			}
			await Task.Delay(1200, ct);
			object injected = await Cdp.EvaluateAsync(BuildHelper(), 15000);
			string status = ((injected == null) ? "" : injected.ToString());
			if (status != "ok")
			{
				throw new Exception("注入页面脚本失败: " + status);
			}
			for (int j = 0; j < 40; j++)
			{
				ct.ThrowIfCancellationRequested();
				object has;
				try
				{
					has = await Cdp.EvaluateAsync("!!(window.__eagentx && window.__eagentx.input())", 8000);
				}
				catch
				{
					has = null;
				}
				if (has is bool && (bool)has)
				{
					break;
				}
				await Task.Delay(500, ct);
			}
			Diagnostics = await ReadDiagnosticsAsync();
		}

		private async Task EnsureStartUrlAsync(CancellationToken ct)
		{
			string href = await GetHrefAsync();
			if (href.IndexOf("deepseek.com", StringComparison.OrdinalIgnoreCase) >= 0)
			{
				return;
			}
			Log.Dim("当前标签页: " + href + "，正在跳转到 " + S.StartUrl);
			Dictionary<string, object> navParams = new Dictionary<string, object> { { "url", S.StartUrl } };
			await Cdp.SendAsync("Page.navigate", navParams, 15000);
			for (int i = 0; i < 60; i++)
			{
				ct.ThrowIfCancellationRequested();
				await Task.Delay(1000, ct);
				if ((await GetHrefAsync()).IndexOf("deepseek.com", StringComparison.OrdinalIgnoreCase) >= 0)
				{
					break;
				}
			}
			for (int j = 0; j < 40; j++)
			{
				ct.ThrowIfCancellationRequested();
				object state;
				try
				{
					state = await Cdp.EvaluateAsync("document.readyState", 8000);
				}
				catch
				{
					state = null;
				}
				if (state != null && state.ToString() == "complete")
				{
					break;
				}
				await Task.Delay(500, ct);
			}
		}

		/// <summary>当前页面地址；页面正在跳转时返回空串。</summary>
		private async Task<string> GetHrefAsync()
		{
			try
			{
				object href = await Cdp.EvaluateAsync("location.href", 8000);
				return (href == null) ? "" : href.ToString();
			}
			catch
			{
				return "";
			}
		}

		/// <summary>
		/// 发送一条消息并等回复。命中网页版「消息发送过于频繁」时按设置等待后自动重发，
		/// 避免一次限流就把整条任务链打断（代价是会话里会多出一条重复的用户消息）。
		/// 测试版：attachments 非空时先把文件塞进网页附件框。
		/// </summary>
		public async Task<string> SendAsync(string text, string[] attachments, CancellationToken ct)
		{
			int retries = Math.Max(0, Math.Min(10, S.RateLimitRetries));
			int waitSec = Math.Max(10, Math.Min(1800, S.RateLimitWaitSec));
			for (int attempt = 0; ; attempt++)
			{
				string reply = null;
				RateLimitException limited = null;
				// C# 5 不允许在 catch 里 await，所以先把异常存下来，出了 try 再决定等多久
				try
				{
					if (attachments != null && attachments.Length > 0)
					{
						Log.Info(await AttachFilesAsync(attachments, ct));
						await Task.Delay(800, ct);
					}
					reply = await SendOnceAsync(text, ct);
				}
				catch (RateLimitException ex)
				{
					limited = ex;
				}
				if (limited == null)
				{
					return reply;
				}
				if (attempt >= retries)
				{
					if (attempt > 0)
					{
						throw new RateLimitException(limited.Message + "\r\n\r\n已自动等待重发 " + attempt + " 次仍未恢复。\r\n可以稍后再试，或在设置里调大「限流重试次数 / 限流等待」。");
					}
					throw limited;
				}
				int delaySec = waitSec << attempt;
				if (delaySec > 1800)
				{
					delaySec = 1800;
				}
				Log.Info("DeepSeek 限流：等待 " + delaySec + " 秒后自动重发（第 " + (attempt + 1) + "/" + retries + " 次重试）…");
				await Task.Delay(delaySec * 1000, ct);
				Log.Info("限流等待结束，重新发送上一条消息…");
			}
		}

		private async Task<string> SendOnceAsync(string text, CancellationToken ct)
		{
			if (Cdp == null)
			{
				throw new Exception("网页引擎还没有连接。");
			}
			Log.Dim("输入框已写入 " + await SetInputAsync(text) + " 字符");
			Dictionary<string, object> beforeMap = Json.Obj(await Cdp.EvaluateAsync("window.__eagentx.beforeSend()", 10000));
			int before = Json.GetInt(beforeMap, "count", 0);
			int beforeMsgs = Json.GetInt(beforeMap, "messages", 0);
			Log.Dim("发送前回复节点: " + before + " · 消息节点: " + beforeMsgs);
			await DispatchEnterAsync();
			bool submitted = await WaitForSubmittedAsync(text, beforeMsgs, 8000, ct);
			if (!submitted)
			{
				Log.Dim("首次提交未被页面接收，正在重试…");
				object emptyValue = await Cdp.EvaluateAsync("window.__eagentx.inputEmpty()", 8000);
				if (emptyValue is bool && (bool)emptyValue)
				{
					await SetInputAsync(text);
				}
				object clicked = await Cdp.EvaluateAsync("window.__eagentx.clickSend()", 10000);
				string clickInfo = ((clicked == null) ? "" : clicked.ToString());
				if (clickInfo == "no-input")
				{
					throw new Exception("找不到输入框，请确认浏览器已登录且停留在对话页。");
				}
				Log.Dim("点击发送按钮: " + clickInfo);
				submitted = await WaitForSubmittedAsync(text, beforeMsgs, 8000, ct);
			}
			if (!submitted)
			{
				throw new Exception("消息没有成功提交到 DeepSeek 页面。\r\n已尝试真实回车和点击发送按钮，但页面没有新增消息节点。\r\n请在浏览器里手动点一次发送，或刷新页面后重试。");
			}
			object leftValue = await Cdp.EvaluateAsync("window.__eagentx.inputEmpty()", 8000);
			bool inputEmpty = leftValue is bool && (bool)leftValue;
			Log.Dim("消息已提交 · 输入框" + (inputEmpty ? "已清空" : "仍有内容"));
			Stopwatch watch = Stopwatch.StartNew();
			int timeoutMs = Math.Max(30, S.ReplyTimeoutSec) * 1000;
			string last = null;
			long stableMs = 0L;
			long firstTextMs = -1L;
			bool sawGenerating = false;
			int ticks = 0;
			string sentTrim = text.Trim();
			while (watch.ElapsedMilliseconds < timeoutMs)
			{
				ct.ThrowIfCancellationRequested();
				await Task.Delay(800, ct);
				Dictionary<string, object> replyState = Json.Obj(await Cdp.EvaluateAsync("window.__eagentx.replyState()", 10000));
				bool hasReply = replyState != null && Json.GetBool(replyState, "has", fallback: false);
				string current = ((replyState == null) ? "" : (Json.GetStr(replyState, "text") ?? ""));
				int actions = ((replyState != null) ? Json.GetInt(replyState, "actions", 0) : 0);
				int count = ((replyState != null) ? Json.GetInt(replyState, "messages", 0) : 0);
				bool generating = replyState == null || Json.GetBool(replyState, "generating", fallback: true);
				object rateLimitedValue = await Cdp.EvaluateAsync("window.__eagentx.rateLimit()", 8000);
				if (rateLimitedValue is bool && (bool)rateLimitedValue)
				{
					throw RateLimitedException();
				}
				if (hasReply && current.Length > 0 && current.Trim() != sentTrim)
				{
					if (firstTextMs < 0)
					{
						firstTextMs = watch.ElapsedMilliseconds;
					}
					if (current == last)
					{
						stableMs += 800;
					}
					else
					{
						last = current;
						stableMs = 0L;
					}
					if (generating)
					{
						sawGenerating = true;
					}
					bool finished = !generating;
					// 停止按钮消失 + 动作栏已出现 + 文本稳定 1.6 秒：正常完成
					if (finished && actions >= 1 && stableMs >= 1600)
					{
						return current.Trim();
					}
					// 见过“生成中”但没有动作栏（旧版页面）：文本稳定 6.4 秒也算完成
					if (finished && actions < 1 && sawGenerating && stableMs >= 6400)
					{
						return current.Trim();
					}
					// 全程没捕捉到“生成中”（短回复）：文本稳定 4.8 秒即认为完成
					if (finished && !sawGenerating && firstTextMs >= 0 && watch.ElapsedMilliseconds - firstTextMs >= 4800 && stableMs >= 4800)
					{
						return current.Trim();
					}
				}
				ticks++;
				if (ticks % 12 == 0)
				{
					Log.Dim("等待回复: 消息 " + count + " · 生成中 " + (generating ? "是" : "否") + " · 动作栏 " + actions + " · 已抓取 " + ((last == null) ? 0 : last.Length) + " 字");
				}
				if (watch.ElapsedMilliseconds > 30000 && last == null)
				{
					Dictionary<string, object> diag = Json.Obj(await Cdp.EvaluateAsync("window.__eagentx.diag()", 8000));
					string sample = ((diag == null) ? "" : (Json.GetStr(diag, "sample") ?? ""));
					if (sample.IndexOf("服务器繁忙", StringComparison.Ordinal) >= 0 || sample.IndexOf("请稍后再试", StringComparison.Ordinal) >= 0)
					{
						throw new Exception("DeepSeek 网页提示繁忙 / 限流，请稍后再试。");
					}
				}
			}
			string extra = "";
			try
			{
				extra = await ReadDiagnosticsAsync();
			}
			catch
			{
			}
			throw new TimeoutException("等待 DeepSeek 回复超时（" + S.ReplyTimeoutSec + " 秒）。\r\n可能原因：未登录、页面结构变化、网络中断，或页面仍在深度思考。\r\n可以点「检测页面」查看页面状态，或在设置里调大回复超时。\r\n\r\n" + extra);
		}

		public async Task<string> AttachFilesAsync(string[] paths, CancellationToken ct)
		{
			if (Cdp == null)
			{
				throw new Exception("网页引擎还没有连接。");
			}
			if (paths == null || paths.Length == 0)
			{
				throw new Exception("没有选择文件。");
			}
			List<string> files = new List<string>();
			StringBuilder names = new StringBuilder();
			foreach (string text in paths)
			{
				if (!string.IsNullOrEmpty(text) && File.Exists(text))
				{
					files.Add(Path.GetFullPath(text));
					if (names.Length > 0)
					{
						names.Append("、");
					}
					names.Append(Path.GetFileName(text));
				}
			}
			if (files.Count == 0)
			{
				throw new Exception("选中的文件都不存在。");
			}
			int nodeId = await FindFileInputNodeAsync();
			if (nodeId <= 0)
			{
				throw new Exception("页面上没有找到文件上传输入框。请确认已登录 DeepSeek 并停留在对话页。");
			}
			Dictionary<string, object> uploadParams = new Dictionary<string, object>
			{
				{ "files", files },
				{ "nodeId", nodeId }
			};
			await Cdp.SendAsync("DOM.setFileInputFiles", uploadParams, 30000);
			await Task.Delay(1200, ct);
			return "已附加 " + files.Count + " 个文件到 DeepSeek 输入框：" + names;
		}

		private async Task<int> FindFileInputNodeAsync()
		{
			Dictionary<string, object> docParams = new Dictionary<string, object> { { "depth", 1 } };
			Dictionary<string, object> docResult = Json.Obj(Json.Get(await Cdp.SendAsync("DOM.getDocument", docParams, 10000), "result"));
			Dictionary<string, object> root = Json.Obj(Json.Get(docResult, "root"));
			int rootId = Json.GetInt(root, "nodeId", 0);
			if (rootId <= 0)
			{
				return 0;
			}
			Dictionary<string, object> queryParams = new Dictionary<string, object>
			{
				{ "nodeId", rootId },
				{ "selector", "input[type=file]" }
			};
			Dictionary<string, object> queryResult = Json.Obj(Json.Get(await Cdp.SendAsync("DOM.querySelector", queryParams, 10000), "result"));
			return Json.GetInt(queryResult, "nodeId", 0);
		}

		public async Task<string> ReadDiagnosticsAsync()
		{
			if (Cdp == null)
			{
				return "尚未连接。";
			}
			Dictionary<string, object> diag = Json.Obj(await Cdp.EvaluateAsync("window.__eagentx.diag()", 12000));
			if (diag == null)
			{
				return "无法读取页面状态。";
			}
			StringBuilder report = new StringBuilder();
			report.AppendLine("页面: " + Json.GetStr(diag, "url"));
			report.AppendLine("标题: " + Json.GetStr(diag, "title"));
			bool hasInput = Json.GetBool(diag, "input", fallback: false);
			report.AppendLine("输入框: " + (hasInput ? ("已找到 (" + Json.GetStr(diag, "inputTag") + " " + Json.GetStr(diag, "placeholder") + ")") : "未找到"));
			bool hasSend = Json.GetBool(diag, "send", fallback: false);
			report.AppendLine("发送按钮: " + (hasSend ? "已找到" : "未找到（会用真实回车键兜底）"));
			List<object> buttons = Json.List(Json.Get(diag, "buttons"));
			if (buttons != null && buttons.Count > 0)
			{
				StringBuilder joined = new StringBuilder();
				for (int i = 0; i < buttons.Count; i++)
				{
					if (i > 0)
					{
						joined.Append(" ; ");
					}
					joined.Append(buttons[i]);
				}
				string buttonList = joined.ToString();
				if (buttonList.Length > 400)
				{
					buttonList = buttonList.Substring(0, 400) + "…";
				}
				report.AppendLine("按钮候选: " + buttonList);
			}
			report.AppendLine("回复节点: " + Json.GetInt(diag, "replies", 0) + " 个");
			List<object> classes = Json.List(Json.Get(diag, "classes"));
			if (classes != null && classes.Count > 0)
			{
				StringBuilder joinedClasses = new StringBuilder();
				for (int j = 0; j < classes.Count; j++)
				{
					if (j > 0)
					{
						joinedClasses.Append(" · ");
					}
					joinedClasses.Append(classes[j]);
				}
				string classList = joinedClasses.ToString();
				if (classList.Length > 500)
				{
					classList = classList.Substring(0, 500) + "…";
				}
				report.AppendLine("相关类名: " + classList);
			}
			report.AppendLine("正在生成: " + (Json.GetBool(diag, "generating", fallback: false) ? "是" : "否"));
			bool login = Json.GetBool(diag, "login", fallback: false) || LooksLikeLoginUrl(Json.GetStr(diag, "url"));
			report.AppendLine("登录状态: " + (login ? "检测到登录表单，请先登录" : "看起来已登录"));
			return report.ToString().TrimEnd();
		}

		private static RateLimitException RateLimitedException()
		{
			return new RateLimitException("DeepSeek 提示「消息发送过于频繁，请稍后重试」。\r\n这是网页版的账号级限流，不是程序故障。\r\n建议等 10–30 分钟再试；期间反复发送可能延长限制时间。");
		}

		private async Task<int> SetInputAsync(string text)
		{
			Dictionary<string, object> setResult = Json.Obj(await Cdp.EvaluateAsync("window.__eagentx.setInput(" + Json.Encode(text) + ")", 20000));
			if (setResult == null || !Json.GetBool(setResult, "ok", fallback: false))
			{
				string reason = ((setResult == null) ? "返回为空" : Json.GetStr(setResult, "reason"));
				throw new Exception("写入输入框失败: " + reason);
			}
			return Json.GetInt(setResult, "length", 0);
		}

		private async Task<bool> WaitForSubmittedAsync(string text, int beforeMsgs, int timeoutMs, CancellationToken ct)
		{
			Stopwatch watch = Stopwatch.StartNew();
			while (watch.ElapsedMilliseconds < timeoutMs)
			{
				ct.ThrowIfCancellationRequested();
				await Task.Delay(500, ct);
				try
				{
					object countObj = await Cdp.EvaluateAsync("window.__eagentx.msgCount()", 8000);
					int now = ((countObj != null) ? Convert.ToInt32(countObj) : 0);
					if (now > beforeMsgs)
					{
						return true;
					}
				}
				catch
				{
				}
				bool limited = false;
				try
				{
					object rateLimitedValue = await Cdp.EvaluateAsync("window.__eagentx.rateLimit()", 8000);
					limited = rateLimitedValue is bool && (bool)rateLimitedValue;
				}
				catch
				{
				}
				if (limited)
				{
					throw RateLimitedException();
				}
				try
				{
					object generatingValue = await Cdp.EvaluateAsync("window.__eagentx.generating()", 8000);
					if (generatingValue is bool && (bool)generatingValue)
					{
						return true;
					}
				}
				catch
				{
				}
				try
				{
					object userSentValue = await Cdp.EvaluateAsync("window.__eagentx.userSent(" + Json.Encode(text) + ")", 8000);
					if (userSentValue is bool && (bool)userSentValue)
					{
						return true;
					}
				}
				catch
				{
				}
			}
			return false;
		}

		private async Task DispatchEnterAsync()
		{
			Dictionary<string, object> keyDownParams = new Dictionary<string, object>
			{
				{ "type", "keyDown" },
				{ "key", "Enter" },
				{ "code", "Enter" },
				{ "windowsVirtualKeyCode", 13 },
				{ "nativeVirtualKeyCode", 13 },
				{ "text", "\r" },
				{ "unmodifiedText", "\r" }
			};
			await Cdp.SendAsync("Input.dispatchKeyEvent", keyDownParams, 8000);
			Dictionary<string, object> keyUpParams = new Dictionary<string, object>
			{
				{ "type", "keyUp" },
				{ "key", "Enter" },
				{ "code", "Enter" },
				{ "windowsVirtualKeyCode", 13 },
				{ "nativeVirtualKeyCode", 13 }
			};
			await Cdp.SendAsync("Input.dispatchKeyEvent", keyUpParams, 8000);
		}

		private string BuildHelper()
		{
			Dictionary<string, object> selectors = new Dictionary<string, object>();
			selectors["input"] = S.Sel.Input;
			selectors["send"] = S.Sel.Send;
			selectors["reply"] = S.Sel.Reply;
			selectors["generating"] = S.Sel.Generating;
			return "\n(function () {\n  try {\n    var CFG = __CFG__;\n    var old = window.__eagentx;\n    if (old && old.v === 10) { old.cfg = CFG; return 'ok'; }\n\n    var E = { v: 10, cfg: CFG };\n\n    E.all = function (sel) {\n      try { return Array.prototype.slice.call(document.querySelectorAll(sel)); } catch (e) { return []; }\n    };\n\n    E.vis = function (el) {\n      if (!el) return false;\n      var r = el.getBoundingClientRect();\n      if (r.width < 2 || r.height < 2) return false;\n      var st = window.getComputedStyle(el);\n      return st.visibility !== 'hidden' && st.display !== 'none' && parseFloat(st.opacity || '1') > 0.01;\n    };\n\n    E.first = function (sel) {\n      var a = E.all(sel);\n      for (var i = 0; i < a.length; i++) { if (E.vis(a[i])) return a[i]; }\n      return null;\n    };\n\n    E.norm = function (s) {\n      return (s || '').replace(/\\s+/g, ' ').trim();\n    };\n\n    E.msgCount = function () {\n      return E.all('.ds-message').length;\n    };\n\n    E.userSent = function (text) {\n      var needle = E.norm(text);\n      if (!needle) return false;\n      var ms = E.all('.ds-message');\n      for (var i = ms.length - 1; i >= 0 && i >= ms.length - 4; i--) {\n        if (ms[i].querySelector('.ds-markdown')) continue;\n        var t = E.norm(ms[i].innerText || '');\n        if (t === needle || (t.length > 40 && needle.indexOf(t) === 0)) return true;\n      }\n      return false;\n    };\n\n    E.latestReply = function () {\n      var ms = E.all('.ds-message');\n      if (!ms.length) {\n        var rs = E.replies();\n        return rs.length ? E.serialize(rs[rs.length - 1]) : '';\n      }\n      for (var i = ms.length - 1; i >= 0; i--) {\n        if (!E.vis(ms[i])) continue;\n        var md = ms[i].querySelector('.ds-markdown');\n        if (md) return E.serialize(md);\n        if (E.norm(ms[i].innerText || '').length > 0) return '';\n      }\n      return '';\n    };\n\n    E.input = function () {\n      var el = E.first(E.cfg.input);\n      if (el) return el;\n      var t = E.all('textarea');\n      for (var i = 0; i < t.length; i++) { if (E.vis(t[i])) return t[i]; }\n      var e = E.all('[contenteditable=\"true\"]');\n      for (var j = 0; j < e.length; j++) { if (E.vis(e[j])) return e[j]; }\n      return null;\n    };\n\n    E.sendBtn = function () {\n      var b = E.first(E.cfg.send);\n      if (b && !b.disabled && b.getAttribute('aria-disabled') !== 'true') return b;\n\n      var el = E.input();\n      if (!el) return null;\n      var ir = el.getBoundingClientRect();\n      var c = E.all('button,div[role=\"button\"],div[class*=\"button\"]');\n      var best = null, bestScore = -1000;\n\n      for (var i = 0; i < c.length; i++) {\n        var x = c[i];\n        if (!E.vis(x)) continue;\n        if (x.disabled || x.getAttribute('aria-disabled') === 'true') continue;\n\n        var txt = ((x.getAttribute('aria-label') || '') + ' ' + (x.getAttribute('title') || '') + ' ' + (x.innerText || ''))\n          .replace(/\\s+/g, ' ').trim();\n        if (/attach|upload|附件|上传|文件|deep|思考|search|联网|语音|voice/i.test(txt)) continue;\n\n        var r = x.getBoundingClientRect();\n        var score = 0;\n        if (/send|发送|submit/i.test(txt)) score += 100;\n        if ((x.className || '').toString().indexOf('primary') >= 0) score += 25;\n        var dy = Math.abs((r.top + r.height / 2) - (ir.top + ir.height / 2));\n        var dx = r.left - ir.right;\n        if (dx > -60 && dx < 260) score += 20;\n        if (dy < ir.height) score += 20;\n        score -= Math.min(60, dy / 8);\n\n        if (score > bestScore) { bestScore = score; best = x; }\n      }\n      return bestScore >= 30 ? best : null;\n    };\n\n    E.replies = function () {\n      var a = E.all(E.cfg.reply);\n      var keep = [];\n      for (var i = 0; i < a.length; i++) {\n        if (!E.vis(a[i])) continue;\n        var nested = false;\n        for (var j = 0; j < a.length; j++) {\n          if (i !== j && a[j] && a[j] !== a[i] && a[j].contains(a[i])) { nested = true; break; }\n        }\n        if (!nested) keep.push(a[i]);\n      }\n      return keep;\n    };\n\n    E.serialize = function (el) {\n      var clone = el.cloneNode(true);\n      var pres = clone.querySelectorAll('pre');\n      for (var i = 0; i < pres.length; i++) {\n        var pre = pres[i];\n        var code = pre.querySelector('code');\n        var lang = '';\n        if (code) {\n          var cls = ((code.className || '') + ' ' + (pre.className || '')).toString();\n          var m = /language-([a-z0-9+#_-]+)/i.exec(cls);\n          if (m) lang = m[1];\n        }\n        if (!lang) {\n          var prev = pre.previousElementSibling;\n          if (prev) {\n            var t = (prev.innerText || '').trim();\n            var lm = /[A-Za-z0-9+#_-]+/.exec(t);\n            if (lm && t.length < 40) lang = lm[0].toLowerCase();\n          }\n        }\n        var content = code ? (code.innerText || code.textContent || '') : (pre.innerText || '');\n        var block = '\\n```' + lang + '\\n' + content.replace(/\\s+$/, '') + '\\n```\\n';\n        pre.parentNode.replaceChild(document.createTextNode(block), pre);\n      }\n      return (clone.innerText || clone.textContent || '').trim();\n    };\n\n    E.replyText = function (idx) {\n      var rs = E.replies();\n      if (idx < 0) idx = rs.length + idx;\n      if (idx < 0 || idx >= rs.length) return '';\n      var t = E.serialize(rs[idx]);\n      return t.replace(/\\u00a0/g, ' ').trim();\n    };\n\n    E.generating = function () {\n      // DeepSeek 生成时右侧主按钮会从“发送箭头”换成“停止方块”。\n      // 注意：不能先看 disabled 类——服务端慢时停止按钮也可能短暂带 disabled。\n      // 图标是停止方块就一定在生成；图标还没换过来时，非 disabled 也按生成中处理。\n      var primary = E.first('.ds-button--primary');\n      if (primary) {\n        var cls = String(primary.className || '');\n        var svg = primary.querySelector('svg');\n        var icon = svg ? (svg.innerHTML || '') : '';\n        if (icon.indexOf('M2 4.88') >= 0) return true;   // 停止方块\n        if (icon.indexOf('M8.3125') >= 0) return false; // 发送箭头\n        if (cls.indexOf('disabled') >= 0) return false;\n        return true;\n      }\n\n      if (E.first(E.cfg.generating)) return true;\n      var c = E.all('div[role=\"button\"],button');\n      for (var i = 0; i < c.length; i++) {\n        if (!E.vis(c[i])) continue;\n        var t = (c[i].getAttribute('aria-label') || '') + ' ' + (c[i].className || '');\n        var s = c[i].querySelector('svg');\n        var ic = s ? (s.innerHTML || '') : '';\n        if (ic.indexOf('M2 4.88') >= 0) return true;\n        if (/stop|停止|中止/i.test(t)) return true;\n      }\n      return false;\n    };\n\n    // 一次返回：回复文本、是否找到助手消息、消息底部动作栏按钮数、是否仍在生成。\n    // 动作栏（复制/重新生成等）只在消息完成后出现，比单纯看按钮更可靠。\n    E.replyState = function () {\n      var ms = E.all('.ds-message');\n      for (var i = ms.length - 1; i >= 0; i--) {\n        if (!E.vis(ms[i])) continue;\n        var md = ms[i].querySelector('.ds-markdown');\n        if (md) {\n          var actions = 0;\n          var p = ms[i].parentElement;\n          if (p) {\n            for (var k = 0; k < p.children.length; k++) {\n              var sib = p.children[k];\n              if (sib === ms[i]) continue;\n              actions += sib.querySelectorAll('button,div[role=\"button\"]').length;\n            }\n          }\n          return {\n            has: true,\n            text: E.serialize(md),\n            actions: actions,\n            messages: ms.length,\n            generating: E.generating()\n          };\n        }\n        if (E.norm(ms[i].innerText || '').length > 0) {\n          return { has: false, text: '', actions: 0, messages: ms.length, generating: E.generating() };\n        }\n      }\n      return { has: false, text: '', actions: 0, messages: ms.length, generating: E.generating() };\n    };\n\n    E.rateLimit = function () {\n      var ms = E.all('.ds-message');\n      if (!ms.length) return false;\n      var last = ms[ms.length - 1];\n      var t = (last.parentElement ? last.parentElement.innerText : last.innerText) || '';\n      return t.indexOf('消息发送过于频繁') >= 0 ||\n             t.indexOf('请求过于频繁') >= 0 ||\n             t.indexOf('发送太频繁') >= 0;\n    };\n\n    E.beforeSend = function () {\n      var rs = E.replies();\n      return { count: rs.length, messages: E.msgCount(), sample: rs.length ? (rs[rs.length - 1].innerText || '').slice(-200) : '' };\n    };\n\n    E.setInput = function (text) {\n      var el = E.input();\n      if (!el) return { ok: false, reason: '找不到输入框' };\n      el.focus();\n      var tag = (el.tagName || '').toUpperCase();\n      if (tag === 'TEXTAREA' || tag === 'INPUT') {\n        var proto = tag === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype;\n        var d = Object.getOwnPropertyDescriptor(proto, 'value');\n        if (d && d.set) d.set.call(el, text); else el.value = text;\n        el.dispatchEvent(new Event('input', { bubbles: true }));\n        el.dispatchEvent(new Event('change', { bubbles: true }));\n      } else {\n        el.innerHTML = '';\n        var sel = window.getSelection();\n        var range = document.createRange();\n        range.selectNodeContents(el);\n        sel.removeAllRanges();\n        sel.addRange(range);\n        var ok = false;\n        try { ok = document.execCommand('insertText', false, text); } catch (e1) { ok = false; }\n        if (!ok) {\n          el.textContent = text;\n          try { el.dispatchEvent(new InputEvent('input', { bubbles: true, data: text, inputType: 'insertText' })); }\n          catch (e2) { el.dispatchEvent(new Event('input', { bubbles: true })); }\n        }\n        sel.removeAllRanges();\n      }\n      var cur = (tag === 'TEXTAREA' || tag === 'INPUT') ? el.value : (el.innerText || '');\n      return { ok: true, length: (cur || '').length, tag: tag };\n    };\n\n    E.inputEmpty = function () {\n      var el = E.input();\n      if (!el) return true;\n      var tag = (el.tagName || '').toUpperCase();\n      var v = (tag === 'TEXTAREA' || tag === 'INPUT') ? el.value : (el.innerText || '');\n      return !v || v.replace(/\\s+/g, '').length === 0;\n    };\n\n    E.clickSend = function () {\n      var b = E.sendBtn();\n      if (b) { b.click(); return 'click'; }\n      var el = E.input();\n      if (!el) return 'no-input';\n      el.focus();\n      var o = { key: 'Enter', code: 'Enter', keyCode: 13, which: 13, bubbles: true, cancelable: true, composed: true };\n      ['keydown', 'keypress', 'keyup'].forEach(function (t) {\n        try { el.dispatchEvent(new KeyboardEvent(t, o)); } catch (e) {}\n      });\n      return 'enter';\n    };\n\n    E.buttons = function () {\n      var a = E.all('button,div[role=\"button\"],a[role=\"button\"]');\n      var out = [];\n      for (var i = 0; i < a.length && out.length < 12; i++) {\n        if (!E.vis(a[i])) continue;\n        var label = (a[i].getAttribute('aria-label') || a[i].getAttribute('title') || '').replace(/\\s+/g, ' ').slice(0, 24);\n        var text = (a[i].innerText || '').replace(/\\s+/g, ' ').trim().slice(0, 16);\n        var cls = (a[i].className || '').toString().slice(0, 32);\n        out.push(a[i].tagName + '|' + cls + '|' + label + '|' + text);\n      }\n      return out;\n    };\n\n    E.classes = function () {\n      var set = {}, out = [];\n      var a = E.all('[class]');\n      for (var i = 0; i < a.length && out.length < 18; i++) {\n        var c = (a[i].className || '').toString();\n        if (/markdown|message|chat|answer|reply/i.test(c) && !set[c]) {\n          set[c] = 1;\n          out.push(c.slice(0, 60));\n        }\n      }\n      return out;\n    };\n\n    E.diag = function () {\n      var inp = E.input();\n      var b = E.sendBtn();\n      return {\n        url: location.href,\n        title: document.title,\n        input: !!inp,\n        inputTag: inp ? inp.tagName : '',\n        placeholder: inp ? (inp.getAttribute('placeholder') || '') : '',\n        send: !!b,\n        sendLabel: b ? (((b.getAttribute('aria-label') || b.getAttribute('title') || '') + ' ' + (b.className || '')).slice(0, 120)) : '',\n        replies: E.replies().length,\n        generating: E.generating(),\n        login: !!document.querySelector('input[type=\"password\"]'),\n        buttons: E.buttons(),\n        classes: E.classes(),\n        sample: (document.body ? document.body.innerText : '').replace(/\\s+/g, ' ').slice(0, 160)\n      };\n    };\n\n    window.__eagentx = E;\n    return 'ok';\n  } catch (err) {\n    return 'err:' + err;\n  }\n})()\n".Replace("__CFG__", Json.Encode(selectors));
		}

		public void Dispose()
		{
			try
			{
				if (Cdp != null)
				{
					Cdp.Dispose();
				}
			}
			catch
			{
			}
			try
			{
				if (Launcher != null)
				{
					Launcher.Dispose();
				}
			}
			catch
			{
			}
			Cdp = null;
			Launcher = null;
		}
	}

	/// <summary>网页版限流的专用异常，用于和普通错误区分（只有它会被自动等待重发）。</summary>
	internal sealed class RateLimitException : Exception
	{
		public RateLimitException(string message)
			: base(message)
		{
		}
	}
}
