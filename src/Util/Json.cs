using System;
using System.Collections.Generic;
using System.Web.Script.Serialization;

namespace EAgentX.Util
{
	public static class Json
	{
		private static readonly JavaScriptSerializer Ser;

		static Json()
		{
			Ser = new JavaScriptSerializer();
			Ser.MaxJsonLength = int.MaxValue;
			Ser.RecursionLimit = 200;
		}

		public static string Encode(object value)
		{
			return Ser.Serialize(value);
		}

		public static object Decode(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return null;
			}
			try
			{
				return Ser.DeserializeObject(text);
			}
			catch
			{
				return null;
			}
		}

		public static T Decode<T>(string text)
		{
			if (string.IsNullOrEmpty(text))
			{
				return default(T);
			}
			try
			{
				return Ser.Deserialize<T>(text);
			}
			catch
			{
				return default(T);
			}
		}

		/// <summary>把 JSON 对象结果转成字典，不是字典就返回 null。</summary>
		public static Dictionary<string, object> Obj(object value)
		{
			return value as Dictionary<string, object>;
		}

		/// <summary>取字典里的一个值，键不存在或字典为空时返回 null。</summary>
		public static object Get(Dictionary<string, object> map, string key)
		{
			object value;
			if (map != null && map.TryGetValue(key, out value))
			{
				return value;
			}
			return null;
		}

		public static string GetStr(Dictionary<string, object> map, string key)
		{
			object value = Get(map, key);
			return (value == null) ? null : value.ToString();
		}

		public static int GetInt(Dictionary<string, object> map, string key, int fallback)
		{
			object value = Get(map, key);
			if (value == null)
			{
				return fallback;
			}
			try
			{
				return Convert.ToInt32(value);
			}
			catch
			{
				return fallback;
			}
		}

		public static bool GetBool(Dictionary<string, object> map, string key, bool fallback)
		{
			object value = Get(map, key);
			if (value == null)
			{
				return fallback;
			}
			try
			{
				return Convert.ToBoolean(value);
			}
			catch
			{
				return fallback;
			}
		}

		/// <summary>把 JSON 数组结果转成 List，兼容 JavaScriptSerializer 返回的 object[]。</summary>
		public static List<object> List(object value)
		{
			List<object> list = value as List<object>;
			if (list != null)
			{
				return list;
			}
			object[] array = value as object[];
			if (array != null)
			{
				return new List<object>(array);
			}
			return null;
		}
	}
}
