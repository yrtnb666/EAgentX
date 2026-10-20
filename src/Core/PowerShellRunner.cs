using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace EAgentX.Core
{
	/// <summary>
	/// PowerShell 执行器。几个关键设计：
	///   1. 用户命令写进 eagentx-cmd-*.ps1，宿主收尾脚本写进 eagentx-run-*.ps1，
	///      两者分文件，即使用户命令以半截括号结尾也不会把收尾代码吞进同一个语法单元。
	///   2. stdout / stderr 直接读原始字节，先严格按 UTF-8 解码，失败再按系统 ANSI（中文为 GBK）兜底，
	///      避免 PowerShell 语法错误这类“发生在设置编码之前”的输出变成乱码。
	///   3. 超时后 Kill + taskkill /T 杀整棵进程树。
	/// </summary>
	public class PowerShellRunner
	{
		private const int MaxOutput = 12000;

		public string WorkDir { get; set; }

		public int TimeoutMs { get; set; }

		public static string PowerShellPath
		{
			get
			{
				string systemPowerShell = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell\\v1.0\\powershell.exe");
				if (File.Exists(systemPowerShell))
				{
					return systemPowerShell;
				}
				return "powershell.exe";
			}
		}

		public PowerShellRunner(string workDir, int timeoutSec)
		{
			WorkDir = workDir;
			TimeoutMs = Math.Max(5, timeoutSec) * 1000;
		}

		public RunResult Run(string command)
		{
			RunResult result = new RunResult();
			result.Command = command;
			Stopwatch watch = Stopwatch.StartNew();
			if (string.IsNullOrEmpty(command) || command.Trim().Length == 0)
			{
				watch.Stop();
				result.ExitCode = 0;
				result.Output = "（空命令）";
				result.ElapsedMs = watch.ElapsedMilliseconds;
				return result;
			}
			string token = Guid.NewGuid().ToString("N");
			string commandScript = Path.Combine(Path.GetTempPath(), "eagentx-cmd-" + token + ".ps1");
			string runnerScript = Path.Combine(Path.GetTempPath(), "eagentx-run-" + token + ".ps1");
			string runnerContents = BuildRunnerScript(commandScript);
			try
			{
				File.WriteAllText(commandScript, command, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
				File.WriteAllText(runnerScript, runnerContents, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
			}
			catch (Exception ex)
			{
				watch.Stop();
				result.ExitCode = -1;
				result.Output = "写入临时脚本失败: " + ex.Message;
				result.ElapsedMs = watch.ElapsedMilliseconds;
				return result;
			}
			Process process = null;
			MemoryStream stdoutBuffer = new MemoryStream();
			MemoryStream stderrBuffer = new MemoryStream();
			Task stdoutCopy = null;
			Task stderrCopy = null;
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo();
				startInfo.FileName = PowerShellPath;
				startInfo.Arguments = "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" + runnerScript + "\"";
				startInfo.WorkingDirectory = (Directory.Exists(WorkDir) ? WorkDir : Environment.CurrentDirectory);
				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;
				startInfo.RedirectStandardOutput = true;
				startInfo.RedirectStandardError = true;
				process = new Process();
				process.StartInfo = startInfo;
				process.Start();
				stdoutCopy = process.StandardOutput.BaseStream.CopyToAsync(stdoutBuffer);
				stderrCopy = process.StandardError.BaseStream.CopyToAsync(stderrBuffer);
				if (!process.WaitForExit(TimeoutMs))
				{
					result.TimedOut = true;
					try
					{
						process.Kill();
					}
					catch
					{
					}
					try
					{
						KillTree(process.Id);
					}
					catch
					{
					}
					try
					{
						process.WaitForExit(3000);
					}
					catch
					{
					}
				}
				WaitCopy(stdoutCopy, 4000);
				WaitCopy(stderrCopy, 4000);
				string stdout = Decode(stdoutBuffer.ToArray());
				string stderr = Decode(stderrBuffer.ToArray());
				result.ExitCode = (result.TimedOut ? (-1) : process.ExitCode);
				StringBuilder output = new StringBuilder();
				if (!string.IsNullOrEmpty(stdout))
				{
					output.Append(stdout.TrimEnd());
				}
				if (!string.IsNullOrEmpty(stderr))
				{
					if (output.Length > 0)
					{
						output.AppendLine();
					}
					output.Append("【stderr】");
					output.AppendLine();
					output.Append(stderr.TrimEnd());
				}
				if (output.Length == 0)
				{
					output.Append("（无输出）");
				}
				result.Output = Truncate(output.ToString());
			}
			catch (Exception ex2)
			{
				result.ExitCode = -1;
				result.Output = "执行器异常: " + ex2.Message;
			}
			finally
			{
				watch.Stop();
				result.ElapsedMs = watch.ElapsedMilliseconds;
				try
				{
					if (process != null)
					{
						process.Dispose();
					}
				}
				catch
				{
				}
				try
				{
					if (File.Exists(commandScript))
					{
						File.Delete(commandScript);
					}
				}
				catch
				{
				}
				try
				{
					if (File.Exists(runnerScript))
					{
						File.Delete(runnerScript);
					}
				}
				catch
				{
				}
			}
			return result;
		}

		/// <summary>生成宿主收尾脚本：统一输出编码、调用用户命令、把退出码带出来。</summary>
		private static string BuildRunnerScript(string commandPath)
		{
			string quotedPath = "'" + commandPath.Replace("'", "''") + "'";
			StringBuilder sb = new StringBuilder();
			sb.AppendLine("$ErrorActionPreference = 'Continue'");
			sb.AppendLine("try { [Console]::OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}");
			sb.AppendLine("try { $OutputEncoding = [System.Text.Encoding]::UTF8 } catch {}");
			sb.AppendLine("$__eagentx_ok = $true");
			sb.AppendLine("try { & " + quotedPath + " } catch { Write-Error $_; $__eagentx_ok = $false }");
			sb.AppendLine("if (-not $__eagentx_ok) { exit 1 }");
			sb.AppendLine("if ($null -ne $LASTEXITCODE) { exit $LASTEXITCODE }");
			return sb.ToString();
		}

		private static void WaitCopy(Task copyTask, int waitMs)
		{
			if (copyTask == null)
			{
				return;
			}
			try
			{
				copyTask.Wait(waitMs);
			}
			catch
			{
			}
		}

		/// <summary>UTF-8 严格解码，失败再按系统 ANSI 兜底（中文系统即 GBK）。</summary>
		private static string Decode(byte[] data)
		{
			if (data == null || data.Length == 0)
			{
				return "";
			}
			int bomLength = 0;
			if (data.Length >= 3 && data[0] == 239 && data[1] == 187 && data[2] == 191)
			{
				bomLength = 3;
			}
			try
			{
				return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(data, bomLength, data.Length - bomLength);
			}
			catch
			{
				try
				{
					return Encoding.Default.GetString(data, bomLength, data.Length - bomLength);
				}
				catch
				{
					return Encoding.UTF8.GetString(data, bomLength, data.Length - bomLength);
				}
			}
		}

		/// <summary>超时后杀掉整棵进程树，避免 powershell 拉起的子进程继续跑。</summary>
		private static void KillTree(int pid)
		{
			try
			{
				ProcessStartInfo startInfo = new ProcessStartInfo();
				startInfo.FileName = "taskkill.exe";
				startInfo.Arguments = "/T /F /PID " + pid;
				startInfo.UseShellExecute = false;
				startInfo.CreateNoWindow = true;
				startInfo.RedirectStandardOutput = true;
				startInfo.RedirectStandardError = true;
				using (Process killer = Process.Start(startInfo))
				{
					if (killer != null)
					{
						killer.WaitForExit(2000);
					}
				}
			}
			catch
			{
			}
		}

		/// <summary>输出过长时保留头 8000 + 尾 3000 字符。</summary>
		private static string Truncate(string text)
		{
			if (text == null)
			{
				return "";
			}
			if (text.Length <= MaxOutput)
			{
				return text;
			}
			int head = 8000;
			int tail = 3000;
			return text.Substring(0, head) + "\r\n...（输出过长，已省略 " + (text.Length - head - tail) + " 字符）...\r\n" + text.Substring(text.Length - tail);
		}
	}
}
