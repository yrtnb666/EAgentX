using System;
using System.Collections.Generic;
using System.IO;

namespace EAgentX.Util
{
	/// <summary>
	/// 三路日志：内存环形缓冲（给界面用）、事件（通知界面刷新）、文件（%APPDATA%\EAgentX\logs\app.log）。
	/// 写日志永远不应该抛异常打断主流程，所以文件与事件两路都整体 try/catch。
	/// </summary>
	public static class Log
	{
		private const int MaxBufferLines = 500;

		private static readonly object Gate = new object();

		private static readonly List<string> Buffer = new List<string>();

		private static int WritesSinceRotateCheck;

		public static string FilePath
		{
			get
			{
				return Path.Combine(Settings.Dir, "logs", "app.log");
			}
		}

		public static event Action<LogKind, string> Line;

		public static void Write(LogKind kind, string message)
		{
			if (message == null)
			{
				message = "";
			}
			lock (Gate)
			{
				Buffer.Add(string.Format("[{0:HH:mm:ss}] [{1}] {2}", DateTime.Now, kind, message));
				if (Buffer.Count > MaxBufferLines)
				{
					Buffer.RemoveRange(0, Buffer.Count - MaxBufferLines);
				}
			}
			Action<LogKind, string> handler = Log.Line;
			if (handler != null)
			{
				try
				{
					handler(kind, message);
				}
				catch
				{
				}
			}
			try
			{
				string directory = Path.GetDirectoryName(FilePath);
				if (!Directory.Exists(directory))
				{
					Directory.CreateDirectory(directory);
				}
				RotateIfNeeded();
				string line = string.Format("[{0:yyyy-MM-dd HH:mm:ss}] [{1}] {2}\r\n", DateTime.Now, kind, message);
				File.AppendAllText(FilePath, line);
			}
			catch
			{
			}
		}

		/// <summary>app.log 超过 4 MB 就滚成 app.log.1（只保留一份历史），避免长期使用后无限增长。</summary>
		private static void RotateIfNeeded()
		{
			WritesSinceRotateCheck++;
			if (WritesSinceRotateCheck < 100)
			{
				return;
			}
			WritesSinceRotateCheck = 0;
			try
			{
				FileInfo file = new FileInfo(FilePath);
				if (!file.Exists || file.Length < 4194304)
				{
					return;
				}
				string backup = FilePath + ".1";
				if (File.Exists(backup))
				{
					File.Delete(backup);
				}
				File.Move(FilePath, backup);
			}
			catch
			{
			}
		}

		public static void Info(string message)
		{
			Write(LogKind.Info, message);
		}

		public static void Dim(string message)
		{
			Write(LogKind.Dim, message);
		}

		public static void Error(string message)
		{
			Write(LogKind.Error, message);
		}

		public static void Error(Exception ex)
		{
			Write(LogKind.Error, (ex == null) ? "未知错误" : (ex.GetType().Name + ": " + ex.Message));
		}
	}
}
