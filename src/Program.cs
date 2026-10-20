using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using EAgentX.Ui;
using EAgentX.Util;

namespace EAgentX
{
	internal static class Program
	{
		[DllImport("user32.dll")]
		private static extern bool SetProcessDPIAware();

		[STAThread]
		private static void Main(string[] args)
		{
			bool selfTest = false;
			bool webCheck = false;
			bool chatCheck = false;
			bool agentCheck = false;
			bool longCheck = false;
			bool uploadCheck = false;
			bool embedCheck = false;
			bool shotCheck = false;
			string uploadFile = null;
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				if (string.Equals(arg, "--selftest", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "-selftest", StringComparison.OrdinalIgnoreCase) || string.Equals(arg, "/selftest", StringComparison.OrdinalIgnoreCase))
				{
					selfTest = true;
				}
				if (string.Equals(arg, "--webcheck", StringComparison.OrdinalIgnoreCase))
				{
					webCheck = true;
				}
				if (string.Equals(arg, "--chatcheck", StringComparison.OrdinalIgnoreCase))
				{
					chatCheck = true;
				}
				if (string.Equals(arg, "--agentcheck", StringComparison.OrdinalIgnoreCase))
				{
					agentCheck = true;
				}
				if (string.Equals(arg, "--longcheck", StringComparison.OrdinalIgnoreCase))
				{
					longCheck = true;
				}
				if (string.Equals(arg, "--uploadcheck", StringComparison.OrdinalIgnoreCase))
				{
					uploadCheck = true;
					if (i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal))
					{
						uploadFile = args[i + 1];
						i++;
					}
				}
				if (string.Equals(arg, "--embedcheck", StringComparison.OrdinalIgnoreCase))
				{
					embedCheck = true;
				}
				if (string.Equals(arg, "--shotcheck", StringComparison.OrdinalIgnoreCase))
				{
					shotCheck = true;
				}
			}

			// 自检模式：附加到控制台、把结果写进 exe 同目录的 *.log，并用退出码表示成功失败
			if (uploadCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.UploadCheck(uploadFile);
				return;
			}
			if (agentCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.AgentCheck();
				return;
			}
			if (longCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.LongCheck();
				return;
			}
			if (chatCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.ChatCheck();
				return;
			}
			if (shotCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.ShotCheck();
				return;
			}
			if (embedCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.EmbedCheck();
				return;
			}
			if (webCheck)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.WebCheck();
				return;
			}
			if (selfTest)
			{
				SelfTest.AttachConsole();
				Environment.ExitCode = SelfTest.Run();
				return;
			}

			try
			{
				SetProcessDPIAware();
			}
			catch
			{
			}

			// 单实例：第二个进程直接提示并退出
			bool createdNew;
			using (Mutex instanceLock = new Mutex(true, "EAgentX.UI.SingleInstance.5C8A72", out createdNew))
			{
				if (!createdNew)
				{
					// 换成跟随主题的自绘消息框（原生 MessageBox 在深色主题下是刺眼的纯白）
					MessageDialog.Show(null, "EAgentX 已经在运行了，请在任务栏切换。", "EAgentX", MessageBoxIcon.Asterisk, MessageBoxButtons.OK);
					return;
				}
				Application.EnableVisualStyles();
				Application.SetCompatibleTextRenderingDefault(defaultValue: false);
				// 读取上次保存的主题；没有配置或读取失败时默认浅色。
				Theme.Initialize();
				Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
				Application.ThreadException += OnThreadException;
				AppDomain.CurrentDomain.UnhandledException += OnDomainException;
				try
				{
					Application.Run(new MainForm());
				}
				catch (Exception ex)
				{
					Fatal(ex);
				}
				GC.KeepAlive(instanceLock);
			}
		}

		private static void OnThreadException(object sender, ThreadExceptionEventArgs e)
		{
			Fatal(e.Exception);
		}

		private static void OnDomainException(object sender, UnhandledExceptionEventArgs e)
		{
			Fatal(e.ExceptionObject as Exception);
		}

		/// <summary>兜底异常处理：写日志 + 弹窗，绝不静默退出。</summary>
		private static void Fatal(Exception ex)
		{
			try
			{
				Log.Error((ex == null) ? "未知异常" : ex.ToString());
			}
			catch
			{
			}
			try
			{
				MessageDialog.Show(null, "EAgentX 遇到错误：\r\n\r\n" + ((ex == null) ? "未知异常" : ex.Message) + "\r\n\r\n日志: " + Log.FilePath, "EAgentX", MessageBoxIcon.Hand, MessageBoxButtons.OK);
			}
			catch
			{
			}
		}
	}
}
