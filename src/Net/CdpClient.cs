using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAgentX.Util;

namespace EAgentX.Net
{
	/// <summary>
	/// 极简 CDP（Chrome DevTools Protocol）客户端：
	/// 一条 WebSocket 长连接，请求带自增 id，回包按 id 匹配到对应的 TaskCompletionSource。
	/// 只实现 Runtime.evaluate 以及“发任意方法 + 等结果”，够用就行。
	/// </summary>
	public class CdpClient : IDisposable
	{
		private readonly ClientWebSocket Ws = new ClientWebSocket();

		private readonly Dictionary<int, TaskCompletionSource<Dictionary<string, object>>> Pending = new Dictionary<int, TaskCompletionSource<Dictionary<string, object>>>();

		private readonly object Gate = new object();

		private readonly SemaphoreSlim SendGate = new SemaphoreSlim(1, 1);

		private readonly CancellationTokenSource LoopCts = new CancellationTokenSource();

		private int NextId;

		private bool Disposed;

		public string LastError { get; private set; }

		public async Task ConnectAsync(string wsUrl, int timeoutMs)
		{
			Ws.Options.KeepAliveInterval = TimeSpan.FromSeconds(20.0);
			Task connect = Ws.ConnectAsync(new Uri(wsUrl), CancellationToken.None);
			if (await Task.WhenAny(connect, Task.Delay(timeoutMs)) != connect)
			{
				// 连接没完成就超时：把 socket 停掉，否则它会一直挂在 Connecting 状态
				try
				{
					Ws.Abort();
				}
				catch
				{
				}
				throw new TimeoutException("连接调试端口超时（" + timeoutMs + " ms）");
			}
			await connect;
			// 后台收包循环：不等它结束（它会一直跑到 Dispose），赋值给变量只是为了避免「未 await」提示
			Task receiveLoop = Task.Run(() => ReceiveLoopAsync(LoopCts.Token));
		}

		private async Task ReceiveLoopAsync(CancellationToken ct)
		{
			byte[] buffer = new byte[65536];
			MemoryStream frame = new MemoryStream();
			try
			{
				while (!ct.IsCancellationRequested && Ws.State == WebSocketState.Open)
				{
					WebSocketReceiveResult received = await Ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
					if (received.MessageType != WebSocketMessageType.Close)
					{
						frame.Write(buffer, 0, received.Count);
						if (received.EndOfMessage)
						{
							string json = Encoding.UTF8.GetString(frame.ToArray());
							frame.SetLength(0L);
							Dispatch(json);
						}
						continue;
					}
					break;
				}
			}
			catch (Exception ex)
			{
				if (!ct.IsCancellationRequested)
				{
					LastError = ex.Message;
				}
			}
		}

		private void Dispatch(string json)
		{
			Dictionary<string, object> message = Json.Obj(Json.Decode(json));
			if (message == null)
			{
				return;
			}
			object idValue = Json.Get(message, "id");
			if (idValue == null)
			{
				return;
			}
			int id;
			try
			{
				id = Convert.ToInt32(idValue);
			}
			catch
			{
				return;
			}
			TaskCompletionSource<Dictionary<string, object>> pending = null;
			lock (Gate)
			{
				if (Pending.TryGetValue(id, out pending))
				{
					Pending.Remove(id);
				}
			}
			if (pending != null)
			{
				pending.TrySetResult(message);
			}
		}

		/// <summary>发送一条 CDP 命令并等待响应；超时抛 TimeoutException。</summary>
		public async Task<Dictionary<string, object>> SendAsync(string method, Dictionary<string, object> parameters, int timeoutMs)
		{
			if (Disposed)
			{
				throw new ObjectDisposedException("CdpClient");
			}
			TaskCompletionSource<Dictionary<string, object>> pending = new TaskCompletionSource<Dictionary<string, object>>(TaskCreationOptions.RunContinuationsAsynchronously);
			int id;
			lock (Gate)
			{
				id = ++NextId;
				Pending[id] = pending;
			}
			Dictionary<string, object> message = new Dictionary<string, object>
			{
				{ "id", id },
				{ "method", method }
			};
			if (parameters != null)
			{
				message["params"] = parameters;
			}
			byte[] payload = Encoding.UTF8.GetBytes(Json.Encode(message));
			await SendGate.WaitAsync();
			try
			{
				// WebSocket 不允许并发发送，所以串行化
				await Ws.SendAsync(new ArraySegment<byte>(payload), WebSocketMessageType.Text, true, CancellationToken.None);
			}
			catch (Exception ex)
			{
				// 发不出去就把等待槽位清掉，否则这条请求会一直挂在字典里
				RemovePending(id);
				throw new Exception("发送 CDP 命令失败（" + method + "）：" + ex.Message);
			}
			finally
			{
				SendGate.Release();
			}
			if (await Task.WhenAny(pending.Task, Task.Delay(timeoutMs)) != pending.Task)
			{
				RemovePending(id);
				throw new TimeoutException("CDP 调用超时: " + method);
			}
			return await pending.Task;
		}

		private void RemovePending(int id)
		{
			lock (Gate)
			{
				Pending.Remove(id);
			}
		}

		/// <summary>把还在等结果的所有调用一次性失败掉，避免 Dispose 之后有人干等到超时。</summary>
		private void FailPending(Exception error)
		{
			List<TaskCompletionSource<Dictionary<string, object>>> waiting;
			lock (Gate)
			{
				waiting = new List<TaskCompletionSource<Dictionary<string, object>>>(Pending.Values);
				Pending.Clear();
			}
			for (int i = 0; i < waiting.Count; i++)
			{
				waiting[i].TrySetException(error);
			}
		}

		/// <summary>在页面里执行一段 JS 并取回返回值（returnByValue）。</summary>
		public async Task<object> EvaluateAsync(string expression, int timeoutMs)
		{
			Dictionary<string, object> response = await SendAsync("Runtime.evaluate", new Dictionary<string, object>
			{
				{ "expression", expression },
				{ "returnByValue", true },
				{ "awaitPromise", true },
				{ "userGesture", true },
				{ "includeCommandLineAPI", true }
			}, timeoutMs);
			Dictionary<string, object> error = Json.Obj(Json.Get(response, "error"));
			if (error != null)
			{
				throw new Exception("CDP 错误: " + Json.GetStr(error, "message"));
			}
			Dictionary<string, object> result = Json.Obj(Json.Get(response, "result"));
			if (result == null)
			{
				return null;
			}
			Dictionary<string, object> exception = Json.Obj(Json.Get(result, "exceptionDetails"));
			if (exception != null)
			{
				Dictionary<string, object> exceptionObject = Json.Obj(Json.Get(exception, "exception"));
				string description = ((exceptionObject != null) ? Json.GetStr(exceptionObject, "description") : Json.GetStr(exception, "text"));
				throw new Exception("页面脚本异常: " + (description ?? "unknown"));
			}
			Dictionary<string, object> value = Json.Obj(Json.Get(result, "result"));
			if (value == null)
			{
				return null;
			}
			return Json.Get(value, "value");
		}

		public void Dispose()
		{
			if (Disposed)
			{
				return;
			}
			Disposed = true;
			FailPending(new ObjectDisposedException("CdpClient"));
			try
			{
				LoopCts.Cancel();
			}
			catch
			{
			}
			try
			{
				Ws.Abort();
			}
			catch
			{
			}
			try
			{
				Ws.Dispose();
			}
			catch
			{
			}
			try
			{
				SendGate.Dispose();
			}
			catch
			{
			}
		}
	}
}
