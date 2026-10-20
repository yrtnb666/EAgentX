using System;
using System.Collections.Generic;

namespace EAgentX.Core
{
	/// <summary>
	/// 命令完整性检查。作用：模型的长回复被截断时，会剩下 "$a"、"("、"Add-Content ... '"
	/// 这类残片，直接丢给 PowerShell 只会得到一堆语法错误，还会污染上下文。
	/// 这里做一个轻量扫描，判断引号 / here-string / 括号是否闭合，以及结尾是否停在操作符上。
	/// </summary>
	public static class CommandShape
	{
		/// <summary>命令是否看起来完整（可以安全执行）。</summary>
		public static bool IsComplete(string command)
		{
			if (command == null)
			{
				return false;
			}
			string text = command.Replace("\r\n", "\n").Replace('\r', '\n');
			if (text.Trim().Length == 0)
			{
				return false;
			}
			Stack<char> open = new Stack<char>();
			char lastSignificant = '\0';
			int i = 0;
			while (i < text.Length)
			{
				char ch = text[i];
				if (ch == ' ' || ch == '\t' || ch == '\n')
				{
					i++;
					continue;
				}
				// 行注释：# 到行尾
				if (ch == '#')
				{
					for (; i < text.Length && text[i] != '\n'; i++)
					{
					}
					continue;
				}
				// 块注释：<# ... #>
				if (ch == '<' && i + 1 < text.Length && text[i + 1] == '#')
				{
					int commentEnd = text.IndexOf("#>", i + 2, StringComparison.Ordinal);
					if (commentEnd < 0)
					{
						return false;
					}
					i = commentEnd + 2;
					continue;
				}
				switch (ch)
				{
				case '\'':
				{
					// 单引号字符串：内部 '' 表示一个转义的单引号
					int scan = i + 1;
					bool closed = false;
					while (scan < text.Length)
					{
						if (text[scan] == '\'')
						{
							if (scan + 1 >= text.Length || text[scan + 1] != '\'')
							{
								closed = true;
								break;
							}
							scan += 2;
						}
						else
						{
							scan++;
						}
					}
					if (!closed)
					{
						return false;
					}
					lastSignificant = '\'';
					i = scan + 1;
					continue;
				}
				case '"':
				{
					// 双引号字符串：` 转义下一个字符，"" 表示一个转义的双引号
					int scan = i + 1;
					bool closed = false;
					while (scan < text.Length)
					{
						if (text[scan] == '`' && scan + 1 < text.Length)
						{
							scan += 2;
						}
						else if (text[scan] == '"')
						{
							if (scan + 1 >= text.Length || text[scan + 1] != '"')
							{
								closed = true;
								break;
							}
							scan += 2;
						}
						else
						{
							scan++;
						}
					}
					if (!closed)
					{
						return false;
					}
					lastSignificant = '"';
					i = scan + 1;
					continue;
				}
				case '@':
				{
					// here-string：@' ... '@ 或 @" ... "@，结束符必须在行首
					if (i + 1 >= text.Length || (text[i + 1] != '\'' && text[i + 1] != '"'))
					{
						break;
					}
					int bodyStart;
					for (bodyStart = i + 2; bodyStart < text.Length && (text[bodyStart] == ' ' || text[bodyStart] == '\t'); bodyStart++)
					{
					}
					if (bodyStart >= text.Length || text[bodyStart] == '\n')
					{
						char quote = text[i + 1];
						int hereEnd = FindHereStringEnd(text, bodyStart, quote);
						if (hereEnd < 0)
						{
							return false;
						}
						lastSignificant = quote;
						i = hereEnd;
						continue;
					}
					break;
				}
				}
				switch (ch)
				{
				case '(':
				case '[':
				case '{':
					open.Push(ch);
					lastSignificant = ch;
					i++;
					break;
				case ')':
				case ']':
				case '}':
				{
					if (open.Count == 0)
					{
						return false;
					}
					char pair = open.Pop();
					if ((ch == ')' && pair != '(') || (ch == ']' && pair != '[') || (ch == '}' && pair != '{'))
					{
						return false;
					}
					lastSignificant = ch;
					i++;
					break;
				}
				default:
					lastSignificant = ch;
					i++;
					break;
				}
			}
			if (open.Count > 0)
			{
				return false;
			}
			// 结尾停在操作符上，说明命令被截断了
			switch (lastSignificant)
			{
			case '\0':
				return false;
			case '&':
			case '+':
			case ',':
			case '-':
			case '=':
			case '`':
			case '|':
				return false;
			default:
				return true;
			}
		}

		/// <summary>从 start 之后逐行找 here-string 的结束符（行首的 quote + @），返回结束位置之后的下标。</summary>
		private static int FindHereStringEnd(string text, int start, char quote)
		{
			if (start >= text.Length)
			{
				return -1;
			}
			for (int newline = text.IndexOf('\n', start); newline >= 0; newline = text.IndexOf('\n', newline + 1))
			{
				int lineStart;
				for (lineStart = newline + 1; lineStart < text.Length && (text[lineStart] == ' ' || text[lineStart] == '\t'); lineStart++)
				{
				}
				if (lineStart + 1 < text.Length && text[lineStart] == quote && text[lineStart + 1] == '@')
				{
					return lineStart + 2;
				}
			}
			return -1;
		}
	}
}
