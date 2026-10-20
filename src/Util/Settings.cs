using System;
using System.IO;

namespace EAgentX.Util
{
	/// <summary>
	/// 配置：%APPDATA%\EAgentX\config.json。设置窗口里改，保存即生效。
	/// 反序列化失败或字段非法时由 Normalize() 回落到默认值。
	/// </summary>
	public class Settings
	{
		/// <summary>web = DeepSeek 网页版；api = OpenAI 兼容接口。</summary>
		public string Engine { get; set; }

		/// <summary>命令执行目录，也是 .eagentx 会话轨迹的保存位置。</summary>
		public string WorkDir { get; set; }

		public string ApiBase { get; set; }

		public string ApiKey { get; set; }

		public string ApiModel { get; set; }

		/// <summary>留空则使用内置系统提示词；非空时整体覆盖。</summary>
		public string SystemPrompt { get; set; }

		/// <summary>Edge 远程调试端口。</summary>
		public int CdpPort { get; set; }

		/// <summary>自定义 msedge.exe / chrome.exe，留空自动探测。</summary>
		public string EdgePath { get; set; }

		public string StartUrl { get; set; }

		public int ReplyTimeoutSec { get; set; }

		public int CommandTimeoutSec { get; set; }

		public int MaxSteps { get; set; }

		public int SendIntervalSec { get; set; }

		/// <summary>发送间隔上限；0 或小于下限表示固定间隔，不随机。</summary>
		public int SendIntervalMaxSec { get; set; }

		/// <summary>启用截图 / 上传 / 鼠标 / 键盘工具。</summary>
		public bool EnableTools { get; set; }

		/// <summary>连接后把 Edge 窗口嵌进主窗口。</summary>
		public bool EmbedBrowser { get; set; }

		/// <summary>Edge 启动时隐藏自动化标识，并用 --app 打开。</summary>
		public bool StealthFlags { get; set; }

		/// <summary>命中网页版限流后自动等待重发的次数；0 表示不重试、直接报错。</summary>
		public int RateLimitRetries { get; set; }

		/// <summary>限流后首次等待的秒数，之后按 2 倍递增（每次最多 30 分钟）。</summary>
		public int RateLimitWaitSec { get; set; }

		public bool AutoApprove { get; set; }

		public Selectors Sel { get; set; }

		public static string Dir
		{
			get
			{
				// UI 版用独立目录，避免覆盖测试版 / 稳定版的 config.json 和浏览器 profile
				return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "EAgentX-UI");
			}
		}

		public static string ConfigPath
		{
			get
			{
				return Path.Combine(Dir, "config.json");
			}
		}

		/// <summary>网页版用的独立浏览器配置目录，和用户日常浏览器互不干扰。</summary>
		public static string ProfileDir
		{
			get
			{
				return Path.Combine(Dir, "edge-profile");
			}
		}

		public Settings()
		{
			Engine = "web";
			WorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			ApiBase = "https://api.deepseek.com/v1";
			ApiKey = "";
			ApiModel = "deepseek-chat";
			SystemPrompt = "";
			CdpPort = 9555;
			EdgePath = "";
			StartUrl = "https://chat.deepseek.com/";
			ReplyTimeoutSec = 300;
			CommandTimeoutSec = 120;
			MaxSteps = 30;
			SendIntervalSec = 3;
			SendIntervalMaxSec = 0;
			EnableTools = true;
			EmbedBrowser = false;
			StealthFlags = true;
			RateLimitRetries = 2;
			RateLimitWaitSec = 60;
			AutoApprove = false;
			Sel = new Selectors();
		}

		public static Settings Load()
		{
			Settings loaded = null;
			try
			{
				if (File.Exists(ConfigPath))
				{
					string json = File.ReadAllText(ConfigPath);
					loaded = Json.Decode<Settings>(json);
				}
			}
			catch
			{
				loaded = null;
			}
			if (loaded == null)
			{
				loaded = new Settings();
			}
			loaded.Normalize();
			return loaded;
		}

		public Settings Clone()
		{
			Settings copy = new Settings();
			copy.Engine = Engine;
			copy.WorkDir = WorkDir;
			copy.ApiBase = ApiBase;
			copy.ApiKey = ApiKey;
			copy.ApiModel = ApiModel;
			copy.SystemPrompt = SystemPrompt;
			copy.CdpPort = CdpPort;
			copy.EdgePath = EdgePath;
			copy.StartUrl = StartUrl;
			copy.ReplyTimeoutSec = ReplyTimeoutSec;
			copy.CommandTimeoutSec = CommandTimeoutSec;
			copy.MaxSteps = MaxSteps;
			copy.SendIntervalSec = SendIntervalSec;
			copy.SendIntervalMaxSec = SendIntervalMaxSec;
			copy.EnableTools = EnableTools;
			copy.EmbedBrowser = EmbedBrowser;
			copy.StealthFlags = StealthFlags;
			copy.RateLimitRetries = RateLimitRetries;
			copy.RateLimitWaitSec = RateLimitWaitSec;
			copy.AutoApprove = AutoApprove;
			copy.Sel = ((Sel == null) ? new Selectors() : Sel.Clone());
			return copy;
		}

		/// <summary>把所有非法值收回到合理范围，缺的选择器补回默认值。</summary>
		public void Normalize()
		{
			if (string.IsNullOrEmpty(WorkDir) || !Directory.Exists(WorkDir))
			{
				WorkDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
			}
			if (string.IsNullOrEmpty(Engine))
			{
				Engine = "web";
			}
			if (string.IsNullOrEmpty(ApiBase))
			{
				ApiBase = "https://api.deepseek.com/v1";
			}
			if (string.IsNullOrEmpty(ApiModel))
			{
				ApiModel = "deepseek-chat";
			}
			if (string.IsNullOrEmpty(StartUrl))
			{
				StartUrl = "https://chat.deepseek.com/";
			}
			if (CdpPort < 1 || CdpPort > 65535)
			{
				// UI 版默认用 9555，避免和稳定版（9222）、测试版（9444）复用同一个浏览器
				CdpPort = 9555;
			}
			if (ReplyTimeoutSec < 30)
			{
				ReplyTimeoutSec = 30;
			}
			if (ReplyTimeoutSec > 3600)
			{
				ReplyTimeoutSec = 3600;
			}
			if (CommandTimeoutSec < 5)
			{
				CommandTimeoutSec = 5;
			}
			if (CommandTimeoutSec > 3600)
			{
				CommandTimeoutSec = 3600;
			}
			if (MaxSteps < 1)
			{
				MaxSteps = 1;
			}
			if (MaxSteps > 200)
			{
				MaxSteps = 200;
			}
			if (SendIntervalSec < 0)
			{
				SendIntervalSec = 0;
			}
			if (SendIntervalSec > 300)
			{
				SendIntervalSec = 300;
			}
			if (SendIntervalMaxSec < 0)
			{
				SendIntervalMaxSec = 0;
			}
			if (SendIntervalMaxSec > 300)
			{
				SendIntervalMaxSec = 300;
			}
			if (SendIntervalMaxSec > 0 && SendIntervalMaxSec < SendIntervalSec)
			{
				SendIntervalMaxSec = SendIntervalSec;
			}
			if (RateLimitRetries < 0)
			{
				RateLimitRetries = 0;
			}
			if (RateLimitRetries > 10)
			{
				RateLimitRetries = 10;
			}
			if (RateLimitWaitSec < 10)
			{
				RateLimitWaitSec = 10;
			}
			if (RateLimitWaitSec > 1800)
			{
				RateLimitWaitSec = 1800;
			}
			if (Sel == null)
			{
				Sel = new Selectors();
			}
			Selectors defaults = new Selectors();
			if (string.IsNullOrEmpty(Sel.Input))
			{
				Sel.Input = defaults.Input;
			}
			if (string.IsNullOrEmpty(Sel.Send))
			{
				Sel.Send = defaults.Send;
			}
			if (string.IsNullOrEmpty(Sel.Reply))
			{
				Sel.Reply = defaults.Reply;
			}
			if (string.IsNullOrEmpty(Sel.Generating))
			{
				Sel.Generating = defaults.Generating;
			}
			if (string.IsNullOrEmpty(Sel.Login))
			{
				Sel.Login = defaults.Login;
			}
		}

		public void Save()
		{
			try
			{
				if (!Directory.Exists(Dir))
				{
					Directory.CreateDirectory(Dir);
				}
				// 先写临时文件再替换：中途断电/崩溃也不会留下半截 config.json
				string tempPath = ConfigPath + ".tmp";
				File.WriteAllText(tempPath, Json.Encode(this));
				bool replaced = false;
				try
				{
					if (File.Exists(ConfigPath))
					{
						File.Replace(tempPath, ConfigPath, null);
					}
					else
					{
						File.Move(tempPath, ConfigPath);
					}
					replaced = true;
				}
				catch
				{
				}
				if (!replaced)
				{
					// 某些文件系统（FAT、部分网络盘）不支持 Replace，退回到直接覆盖
					File.Copy(tempPath, ConfigPath, true);
					try
					{
						File.Delete(tempPath);
					}
					catch
					{
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error("保存配置失败: " + ex.Message);
			}
		}
	}
}
