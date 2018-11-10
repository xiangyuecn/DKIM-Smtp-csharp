using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace DKIMSmtp {
	/// <summary>
	/// 封装的一些通用方法
	/// </summary>
	public class EMail_Unit {
		static public string Base64EncodeBytes(byte[] byts) {
			return Convert.ToBase64String(byts);
		}
		static public byte[] Base64DecodeBytes(string str) {
			try {
				return Convert.FromBase64String(str);
			} catch {
				return null;
			}
		}

		static public string STR(string str) {
			return str == null ? "" : str;
		}

		static public long GetMS() {
			return (DateTime.Now.Ticks - UTC) / TicksMSUnit;
		}
		static public int TicksMSUnit = 10000;
		static private readonly long UTC = TimeZone.CurrentTimeZone.ToLocalTime(new DateTime(1970, 1, 1)).Ticks;


		static private readonly Random _Random = new Random();
		/// <summary>
		/// 返回一个小于所指定最大值的非负随机数。
		/// </summary>
		public static int RandomInt(int maxValue) {
			return _Random.Next(maxValue);
		}
		/// <summary>
		/// 生成由chars组成的随机字符串
		/// </summary>
		public static string RandomString(char[] chars, int len) {
			StringBuilder s = new StringBuilder(len);
			int clen = chars.Length;
			for (var i = 0; i < len; i++) {
				s.Append(chars[RandomInt(clen)]);
			}
			return s.ToString();
		}
		private static readonly char[] RandomStringChar = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ".ToCharArray();
		private static readonly char[] RandomStringMixChar = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz".ToCharArray();
		/// <summary>
		/// 生成0-9A-Z组合
		/// </summary>
		public static string RandomString(int len) {
			return RandomString(RandomStringChar, len);
		}
		/// <summary>
		/// 生成0-9A-Za-z组合
		/// </summary>
		public static string RandomStringMix(int len) {
			return RandomString(RandomStringMixChar, len);
		}
	}

	public static class Extensions {
		static public List<KT> keys<KT, VT>(this IDictionary<KT, VT> dic) {
			List<KT> list = new List<KT>(dic.Keys);
			return list;
		}
		/// <summary>
		/// 用join字符串拼接数组内元素,list[0]+join+list[1]...
		/// getFn:返回格式化后的字符串，比如"aa"，返回null不拼接此字符串
		/// </summary>
		static public string join(this IEnumerable list, string join, Func<object, string> getFn = null) {
			StringBuilder str = new StringBuilder();
			bool start = false;
			foreach (object o in list) {
				if (getFn != null) {
					string val = getFn(o);
					if (val != null) {
						if (start) {
							str.Append(join);
						}
						str.Append(val);
					} else {
						continue;
					}
				} else {
					if (start) {
						str.Append(join);
					}
					str.Append(o);
				}
				start = true;
			}
			return str.ToString();
		}



		static public string getString<KT, VT>(this IDictionary<KT, VT> dic, KT key) {
			string str = dic.getStringOrNull(key);
			return str == null ? "" : str;
		}
		static public string getStringOrNull<KT, VT>(this IDictionary<KT, VT> dic, KT key) {
			VT val;
			dic.TryGetValue(key, out val);
			if (val == null) {
				return null;
			}
			return Convert.ToString(val);
		}
		/// <summary>
		/// 分割字符串成两个
		/// </summary>
		/// <param name="str"></param>
		/// <param name="p"></param>
		/// <returns></returns>
		static public string[] splitTow(this string str, string p) {
			string[] val = new string[2];
			int len = str.Length;
			if (str == null || len == 0) {
				val[0] = "";
				val[1] = "";
			} else {
				int idx = str.IndexOf(p);
				if (idx == -1) {
					val[0] = str;
					val[1] = "";
				} else {
					val[0] = str.Substring(0, idx);
					idx++;
					if (idx < len) {
						val[1] = str.Substring(idx, len - idx);
					} else {
						val[1] = "";
					}
				}
			}
			return val;
		}
		/// <summary>
		/// 分割字符串
		/// </summary>
		/// <param name="str"></param>
		/// <param name="p"></param>
		/// <returns></returns>
		static public List<string> split(this string str, string p) {
			List<string> val = new List<string>();
			int len = str.Length;
			if (str != null && len > 0) {
				int plen = p.Length;
				if (plen == 0) {
					val.Add(str);
				} else {
					int idx = 0, i = 0;
					while (true) {
						i = str.IndexOf(p, idx);
						if (i == -1) {
							val.Add(str.Substring(idx, len - idx));
							break;
						}
						val.Add(str.Substring(idx, i - idx));
						idx = i + plen;
					}
				}
			} else {
				val.Add("");
			}
			return val;
		}
	}
	public class Hash {
		static public Hash MD5 {
			get {
				return new Hash("MD5");
			}
		}
		static public Hash SHA1 {
			get {
				return new Hash("SHA1");
			}
		}
		static public Hash SHA256 {
			get {
				return new Hash("SHA256");
			}
		}
		static public Hash SHA384 {
			get {
				return new Hash("SHA384");
			}
		}
		static public Hash SHA512 {
			get {
				return new Hash("SHA512");
			}
		}



		public string HashName { get; private set; }
		public byte[] HashKey { get; private set; }
		/// <summary>
		/// 通过hashName构造出一个hash算法，支持：MD5,SHA1,SHA256,SHA384,SHA512
		/// </summary>
		public Hash(string hashName) {
			HashName = hashName;
		}


		private HashAlgorithm __Hash;
		/// <summary>
		/// 获取当前hashName代表的hash实现对象，如果hashName无效，返回null
		/// </summary>
		public HashAlgorithm HashObjectOrNull {
			get {
				if (__Hash == null) {
					HashAlgorithm val;
					if (HashKey != null) {
						var val2 = KeyedHashAlgorithm.Create(HashName);
						if (val2 != null) {
							val2.Key = HashKey;
						}
						val = val2;
					} else {
						val = HashAlgorithm.Create(HashName);
					}
					__Hash = val;
				}
				return __Hash;
			}
		}


		/// <summary>
		/// Hash Base64
		/// </summary>
		public string Base64(string str) {
			return Base64(Encoding.UTF8.GetBytes(str));
		}
		/// <summary>
		/// Hash Base64
		/// </summary>
		public string Base64(byte[] byts) {
			return EMail_Unit.Base64EncodeBytes(Bytes(byts));
		}
		/// <summary>
		/// Hash
		/// </summary>
		public byte[] Bytes(byte[] byts) {
			return HashObjectOrNull.ComputeHash(byts);
		}
	}












	public class Result : IResult<object> {

	}
	public class Result<T> : IResult<T> {

	}
	public abstract class IResult<T> {
		public Dictionary<string, object> Json { get { return json; } }
		protected Dictionary<string, object> json;
		public IResult() {
			json = new Dictionary<string, object>();

			ErrorMessage = "";
			ServerErrorMessage = "";
		}

		protected bool isErr = false;
		protected bool isSevErr = false;
		public string ErrorMessage { get; set; }
		public string ServerErrorMessage { get; set; }
		public T Value {
			get {
				object val;
				json.TryGetValue("v", out val);
				return val == null ? default(T) : (T)val;
			}
			set {
				json["v"] = value;
			}
		}

		public IResult<T> buildResult() {
			json["c"] = isErr ? 1 : 0;
			json["m"] = ErrorMessage;
			if (isSevErr) {
				json["m_sev"] = ServerErrorMessage;
			}

			return this;
		}
		/// <summary>
		/// 运行过程中是否出现错误，如果出错后续业务不应该被执行
		/// </summary>
		public bool IsError {
			get { return isErr; }
		}
		/// <summary>
		/// 运行异常，比如无法处理的捕获异常
		/// </summary>
		/// <param name="message">用户提示</param>
		/// <param name="serverMessage">服务器错误详细信息</param>
		public void fail(string message, string serverMessage) {
			isSevErr = true;
			ServerErrorMessage = serverMessage;
			error(message);
		}
		/// <summary>
		/// 出现错误，给用户友好提示
		/// </summary>
		/// <param name="message">用户提示</param>
		public void error(string message) {
			isErr = true;
			ErrorMessage = message;
		}

		/// <summary>
		/// 把错误信息设置到另外一个对象，包括服务器错误，如果result已经有错将不会复制，新的错误会添加到现有错误前面
		/// </summary>
		public void errorTo<X>(IResult<X> result, string newErr = "", string newSrvErr = "") {
			if (result.isErr || !isErr) {
				return;
			}
			var err = String.IsNullOrEmpty(newErr) ? "" : newErr + "\nby\n";
			err += ErrorMessage;
			var srvErr = String.IsNullOrEmpty(newErr) ? "" : newSrvErr + "\nby\n";

			if (isSevErr || srvErr != "") {
				srvErr += ServerErrorMessage;

				result.fail(err, srvErr);
			} else {
				result.error(err);
			}
		}
	}
}
