using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace EAgentX.Util
{
	/// <summary>
	/// 测试版：把内嵌的 26 个技能解压到 &lt;工作目录&gt;\.eagentx\skills\。
	/// 用 .version 文件做幂等标记，重复调用不会反复解压。
	/// </summary>
	public static class Skills
	{
		private const string ResourceName = "skills.zip";

		private const string Version = "eagent-2026-10-19";

		public static string Ensure(Settings s)
		{
			if (s == null || string.IsNullOrEmpty(s.WorkDir))
			{
				return null;
			}
			string target = Path.Combine(s.WorkDir, ".eagentx", "skills");
			string marker = Path.Combine(target, ".version");
			try
			{
				if (File.Exists(marker) && File.ReadAllText(marker).Trim() == Version && CountSkills(target) > 0)
				{
					return target;
				}
				Directory.CreateDirectory(target);
				Assembly assembly = typeof(Skills).Assembly;
				using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
				{
					if (stream == null)
					{
						Log.Error("没有找到内嵌技能包资源: " + ResourceName);
						return null;
					}
					using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
					{
						string root = Path.GetFullPath(target) + Path.DirectorySeparatorChar;
						foreach (ZipArchiveEntry entry in archive.Entries)
						{
							if (string.IsNullOrEmpty(entry.Name))
							{
								continue;
							}
							string full = Path.GetFullPath(Path.Combine(target, entry.FullName.Replace('/', '\\')));
							if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
							{
								continue;
							}
							string folder = Path.GetDirectoryName(full);
							if (!string.IsNullOrEmpty(folder))
							{
								Directory.CreateDirectory(folder);
							}
							using (Stream input = entry.Open())
							using (FileStream output = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.None))
							{
								input.CopyTo(output);
							}
						}
					}
				}
				File.WriteAllText(marker, Version);
				Log.Info("技能包已解压: " + target + "（" + CountSkills(target) + " 个技能）");
				return target;
			}
			catch (Exception ex)
			{
				Log.Error("解压技能包失败: " + ex.Message);
				return null;
			}
		}

		/// <summary>统计含 SKILL.md 的技能目录数（技能包是「分类/技能名/SKILL.md」两层结构）。</summary>
		public static int CountSkills(string root)
		{
			try
			{
				if (!Directory.Exists(root))
				{
					return 0;
				}
				int count = 0;
				string[] files = Directory.GetFiles(root, "SKILL.md", SearchOption.AllDirectories);
				count = files.Length;
				return count;
			}
			catch
			{
				return 0;
			}
		}
	}
}
