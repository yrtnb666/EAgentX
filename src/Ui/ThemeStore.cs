using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EAgentX.Util;

namespace EAgentX.Ui
{
	/// <summary>
	/// 主题持久化（分区：本地存储）。
	/// 独立文件：%APPDATA%\EAgentX-UI\theme.json，不碰 config.json，不改变原有配置字段和保存逻辑。
	/// 读取失败 / 文件损坏 / 没有文件时一律回退到浅色日间主题，绝不抛异常给主程序。
	/// </summary>
	internal static class ThemeStore
	{
		/// <summary>配置文件的完整路径（使用说明里要告诉用户的就是这个路径）。</summary>
		public static string FilePath
		{
			get
			{
				return Path.Combine(Settings.Dir, "theme.json");
			}
		}

		public static ThemeMode Load()
		{
			try
			{
				string path = FilePath;
				if (!File.Exists(path))
				{
					// 首次打开软件：没有配置记录 → 默认浅色日间主题
					return ThemeMode.Light;
				}
				string text = File.ReadAllText(path, Encoding.UTF8);
				if (string.IsNullOrEmpty(text))
				{
					return ThemeMode.Light;
				}
				// 只认 "mode" 字段，避免把注释里的 dark 当成主题
				Match match = Regex.Match(text, "\"mode\"\\s*:\\s*\"([A-Za-z]+)\"");
				if (match.Success)
				{
					string value = match.Groups[1].Value;
					if (string.Equals(value, "dark", StringComparison.OrdinalIgnoreCase))
					{
						return ThemeMode.Dark;
					}
					return ThemeMode.Light;
				}
				// 兼容裸值写法（例如整个文件只有 dark）
				string trimmed = text.Trim().Trim('"', '\'', '\r', '\n', ' ');
				if (string.Equals(trimmed, "dark", StringComparison.OrdinalIgnoreCase))
				{
					return ThemeMode.Dark;
				}
				return ThemeMode.Light;
			}
			catch
			{
				// 读取失败 → 浅色主题，软件照常启动
				return ThemeMode.Light;
			}
		}

		public static void Save(ThemeMode mode)
		{
			string temp = null;
			try
			{
				Directory.CreateDirectory(Settings.Dir);
				string value = (mode == ThemeMode.Dark) ? "dark" : "light";
				StringBuilder json = new StringBuilder();
				json.Append("{\r\n");
				json.Append("  \"version\": 1,\r\n");
				json.Append("  \"mode\": \"").Append(value).Append("\",\r\n");
				json.Append("  \"savedAt\": \"").Append(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")).Append("\"\r\n");
				json.Append("}\r\n");
				// 先写临时文件再替换，避免写一半断电产生半截文件
				temp = FilePath + ".tmp";
				File.WriteAllText(temp, json.ToString(), new UTF8Encoding(false));
				if (File.Exists(FilePath))
				{
					File.Replace(temp, FilePath, null);
				}
				else
				{
					File.Move(temp, FilePath);
				}
				temp = null;
			}
			catch
			{
				// 主题保存失败不能影响主程序；下次启动会回退浅色
			}
			finally
			{
				try
				{
					if (temp != null && File.Exists(temp))
					{
						File.Delete(temp);
					}
				}
				catch
				{
				}
			}
		}
	}
}
