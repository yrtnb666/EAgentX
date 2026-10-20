using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EAgentX.Core;
using EAgentX.Util;

namespace EAgentX.Engines
{
	/// <summary>
	/// OpenAI 兼容接口引擎（DeepSeek / 通义 / 本地 Ollama…）。
	/// 自己维护完整消息历史；仍然走「文本命令协议」，没实现 function calling，
	/// 好处是两种引擎的行为完全一致。
	/// </summary>
	public class ApiEngine : IChatEngine, IDisposable
	{
		private readonly Settings S;

		private readonly HttpClient Http;

		private readonly List<Dictionary<string, object>> Messages = new List<Dictionary<string, object>>();

		public string Name
		{
			get
			{
				return "API 模式（OpenAI 兼容）";
			}
		}

		public string Diagnostics { get; private set; }

		public ApiEngine(Settings settings)
		{
			S = settings;
			Http = new HttpClient();
			Http.Timeout = TimeSpan.FromSeconds(Math.Max(30, S.ReplyTimeoutSec));
			Diagnostics = "";
		}

		public Task<string> StartAsync(CancellationToken ct)
		{
			// 本地端点（Ollama 之类）不需要 Key
			if (string.IsNullOrEmpty(S.ApiKey) && !IsLocalEndpoint())
			{
				throw new Exception("API 模式需要在设置里填写 API Key（本地 Ollama 等可留空）。");
			}
			Messages.Clear();
			Messages.Add(Message("system", PromptBuilder.System(S)));
			Diagnostics = "API: " + Endpoint() + "  模型: " + S.ApiModel;
			return Task.FromResult(Diagnostics);
		}

		private bool IsLocalEndpoint()
		{
			string baseUrl = S.ApiBase.ToLowerInvariant();
			if (!baseUrl.Contains("127.0.0.1"))
			{
				return baseUrl.Contains("localhost");
			}
			return true;
		}

		/// <summary>Base URL 归一化：支持以 /v1 或 /chat/completions 结尾的写法。</summary>
		private string Endpoint()
		{
			string baseUrl = (S.ApiBase ?? "").Trim().TrimEnd('/');
			if (baseUrl.Length == 0)
			{
				baseUrl = "https://api.deepseek.com/v1";
			}
			if (baseUrl.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
			{
				return baseUrl;
			}
			if (baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
			{
				return baseUrl + "/chat/completions";
			}
			return baseUrl + "/v1/chat/completions";
		}

		private static Dictionary<string, object> Message(string role, object content)
		{
			Dictionary<string, object> message = new Dictionary<string, object>();
			message["role"] = role;
			message["content"] = content;
			return message;
		}

		/// <summary>
		/// 测试版：把附件拼进 API 消息。图片走 image_url(data URL)；
		/// 文本类小文件直接内联；其他文件只能给路径（API 模式读不了 PDF/Office）。
		/// </summary>
		private static object BuildContent(string text, string[] attachments)
		{
			if (attachments == null || attachments.Length == 0)
			{
				return text;
			}
			List<object> parts = new List<object>();
			List<string> notes = new List<string>();
			long totalBytes = 0L;
			for (int i = 0; i < attachments.Length; i++)
			{
				string path = attachments[i];
				if (string.IsNullOrEmpty(path) || !File.Exists(path))
				{
					continue;
				}
				string ext = Path.GetExtension(path).ToLowerInvariant();
				FileInfo info = new FileInfo(path);
				totalBytes += info.Length;
				if (totalBytes > 24L * 1024L * 1024L)
				{
					notes.Add(Path.GetFileName(path) + "（附件总量超过 24MB，已跳过）");
					continue;
				}
				bool isImage = (ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".gif" || ext == ".webp" || ext == ".bmp");
				if (isImage)
				{
					string mime = (ext == ".png") ? "image/png" : (ext == ".gif") ? "image/gif" : (ext == ".webp") ? "image/webp" : (ext == ".bmp") ? "image/bmp" : "image/jpeg";
					Dictionary<string, object> imageUrl = new Dictionary<string, object>();
					imageUrl["url"] = "data:" + mime + ";base64," + Convert.ToBase64String(File.ReadAllBytes(path));
					Dictionary<string, object> part = new Dictionary<string, object>();
					part["type"] = "image_url";
					part["image_url"] = imageUrl;
					parts.Add(part);
					notes.Add(Path.GetFileName(path) + "（图片，已内联）");
					continue;
				}
				bool isText = (ext == ".txt" || ext == ".md" || ext == ".csv" || ext == ".json" || ext == ".log" || ext == ".xml" || ext == ".html" || ext == ".cs" || ext == ".ps1" || ext == ".py" || ext == ".js" || ext == ".ini" || ext == ".yml" || ext == ".yaml");
				if (isText && info.Length <= 100L * 1024L)
				{
					string body = File.ReadAllText(path);
					Dictionary<string, object> part = new Dictionary<string, object>();
					part["type"] = "text";
					part["text"] = "【附件 " + Path.GetFileName(path) + "】\r\n```\r\n" + body + "\r\n```";
					parts.Add(part);
					notes.Add(Path.GetFileName(path) + "（文本，已内联）");
					continue;
				}
				notes.Add(Path.GetFileName(path) + "（API 模式只能给路径：" + path + "）");
			}
			if (parts.Count == 0)
			{
				string onlyText = text;
				if (notes.Count > 0)
				{
					onlyText += "\r\n\r\n【附件说明】\r\n- " + string.Join("\r\n- ", notes.ToArray());
				}
				return onlyText;
			}
			Dictionary<string, object> caption = new Dictionary<string, object>();
			caption["type"] = "text";
			caption["text"] = text + ((notes.Count > 0) ? ("\r\n\r\n【附件说明】\r\n- " + string.Join("\r\n- ", notes.ToArray())) : "");
			parts.Insert(0, caption);
			return parts;
		}

		public async Task<string> SendAsync(string text, string[] attachments, CancellationToken ct)
		{
			Messages.Add(Message("user", BuildContent(text, attachments)));
			string json = Json.Encode(new Dictionary<string, object>
			{
				{ "model", S.ApiModel },
				{ "messages", Messages },
				{ "stream", false },
				{ "temperature", 0.3 }
			});
			using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, Endpoint()))
			{
				request.Content = new StringContent(json, Encoding.UTF8, "application/json");
				if (!string.IsNullOrEmpty(S.ApiKey))
				{
					request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", S.ApiKey);
				}
				using (CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(ct))
				{
					// 全局超时之外再加一层，避免用户点了停止还要等 HttpClient 自己的超时
					linked.CancelAfter(TimeSpan.FromSeconds(S.ReplyTimeoutSec));
					HttpResponseMessage response;
					try
					{
						response = await Http.SendAsync(request, linked.Token);
					}
					catch (OperationCanceledException)
					{
						// 用户点了「停止」和真的超时要分开报，否则会被误认为网络问题
						if (ct.IsCancellationRequested)
						{
							throw;
						}
						throw new TimeoutException("API 请求超时（" + S.ReplyTimeoutSec + " 秒）。");
					}
					using (response)
					{
						string body = await response.Content.ReadAsStringAsync();
						if (!response.IsSuccessStatusCode)
						{
							string detail = ((body == null) ? "" : body.Trim());
							if (detail.Length > 500)
							{
								detail = detail.Substring(0, 500);
							}
							throw new Exception(string.Concat("API 返回 ", (int)response.StatusCode, " ", response.StatusCode, ": ", detail));
						}
						Dictionary<string, object> root = Json.Obj(Json.Decode(body));
						List<object> choices = Json.List(Json.Get(root, "choices"));
						if (choices == null || choices.Count == 0)
						{
							throw new Exception("API 响应里没有 choices: " + Truncate(body));
						}
						Dictionary<string, object> choice = Json.Obj(choices[0]);
						Dictionary<string, object> message = Json.Obj(Json.Get(choice, "message"));
						string content = Json.GetStr(message, "content");
						if (string.IsNullOrEmpty(content))
						{
							// 推理模型有时只给 reasoning_content
							content = Json.GetStr(message, "reasoning_content");
						}
						if (content == null)
						{
							content = "";
						}
						Messages.Add(Message("assistant", content));
						return content;
					}
				}
			}
		}

		private static string Truncate(string text)
		{
			if (text == null)
			{
				return "";
			}
			if (text.Length <= 300)
			{
				return text;
			}
			return text.Substring(0, 300);
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
