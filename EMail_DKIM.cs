using DotNetDetour;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Mail;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DKIMSmtp {
	/// <summary>
	/// 邮件DKIM签名处理类
	/// </summary>
	public class EMail_DKIM {
		public const string DKIMKey = "DKIM-Signature";
		public const string DKIMTimeKeyT = "DKIM-Sign-Date-T";
		public const string DKIMTimeKeyTS = "DKIM-Sign-Date-T-S";

		/// <summary>
		/// 需要签名的邮件标头列表，已默认提供最佳的列表，如需调整可以修改
		/// </summary>
		public List<string> SignHeaderNames { get; set; }
		private RSA.RSA _RSA;
		private string _EmailDomain;
		private string _EmailSelector;
		/// <summary>
		/// 使用RSA私钥构造出DKIM签名对象，这个私钥为发送邮件的邮箱域名配置的秘钥对
		/// </summary>
		public EMail_DKIM(string emailDomain, string emailSelector, RSA.RSA rsa) {
			_EmailDomain = emailDomain;
			_EmailSelector = emailSelector;
			_RSA = rsa;

			SignHeaderNames = new List<string>(new string[]{
				"Date"
				, "From", "Reply-To", "Subject", "To", "Cc"
				, "Resent-Date", "Resent-From", "Resent-To", "Resent-Cc"
				, "In-Reply-To", "References"
				, "List-Id", "List-Help", "List-Unsubscribe", "List-Subscribe", "List-Post", "List-Owner", "List-Archive"
			});
		}

		/// <summary>
		/// 对邮件内容进行签名，如果出错将返回错误
		/// </summary>
		public Result Sign(MailMessage message) {
			var rtv = new Result();

			message.Headers.Set(DKIMTimeKeyT, DateTime.Now.ToUniversalTime().ToString("r").Replace("GMT", "+000"));
			message.Headers.Remove(EMail_DKIM.DKIMTimeKeyTS);

			var rawRes = EMail_DKIM_MailMessageText.ToRAW(message);
			if (rawRes.IsError) {
				rawRes.errorTo(rtv);
				return rtv;
			}
			var signRes = Sign(rawRes.Value);
			if (signRes.IsError) {
				rawRes.errorTo(rtv);
				return rtv;
			}

			message.Headers.Set(DKIMKey, signRes.Value);
			return rtv;
		}
		/// <summary>
		/// 对邮件内容进行签名，如果出错将返回错误
		/// </summary>
		public Result<string> Sign(EMail_DKIM_RAW_EML eml) {
			var rtv = new Result<string>();
			rtv.Value = "";

			var body = eml.GetSignBody(true);
			var signHeadNames = eml.SelectSignHeaderNames(SignHeaderNames);


			var param = new List<string>();
			param.Add("v=1");
			param.Add("a=rsa-sha256");
			param.Add("c=relaxed/relaxed");
			param.Add("d=" + _EmailDomain);
			param.Add("s=" + _EmailSelector);
			param.Add("q=dns/txt");
			param.Add("t=" + EMail_Unit.GetMS() / 1000);
			param.Add("h=" + signHeadNames.join(":"));
			param.Add("bh=" + Hash.SHA256.Base64(body));
			param.Add("b=");
			var paramStr = param.join("; ");

			var signParams = eml.GetSignHeader(true, paramStr, null, signHeadNames);
			if (signParams.IsError) {
				signParams.errorTo(rtv);
				return rtv;
			}
			rtv.Value = paramStr + _RSA.Sign("SHA256", signParams.Value);

			return rtv;
		}
		/// <summary>
		/// 验证邮件eml文件内容DKIM签名是否正确，没有签名或签名错误返回false
		/// </summary>
		public bool Verify(string eml) {
			return Verify(EMail_DKIM_RAW_EML.ParseOrNull(eml));
		}
		/// <summary>
		/// 验证邮件eml文件内容DKIM签名是否正确，没有签名或签名错误返回false
		/// </summary>
		public bool Verify(EMail_DKIM_RAW_EML eml) {
			if (eml == null) {
				return false;
			}
			var sign = eml.GetHeader(DKIMKey);
			if (sign == null) {
				return false;
			}

			var kvs = sign.split(";");
			var kvDic = new Dictionary<string, string>();
			var exp = new Regex(@"\s");
			foreach (var kv in kvs) {
				var val = kv.splitTow("=");
				if (val[0] != "") {
					kvDic[exp.Replace(val[0], "")] = exp.Replace(val[1], "");
				}
			}
			var hash256 = kvDic.getString("a") == "rsa-sha256";
			Hash hash = hash256 ? Hash.SHA256 : Hash.SHA1;
			var cv = kvDic.getString("c").splitTow("/");
			bool headRelaxed = true;
			bool bodyRelaxed = true;
			if (cv[1] == "") {
				headRelaxed = cv[0] == "relaxed";
				bodyRelaxed = false;
			} else {
				headRelaxed = cv[0] == "relaxed";
				bodyRelaxed = cv[1] == "relaxed";
			}

			//验证body
			var body = eml.GetSignBody(bodyRelaxed);
			if (kvDic.getString("bh") != hash.Base64(body)) {
				return false;
			}

			//验证head
			var heads = kvDic.getString("h").split(":");
			var exp2 = new Regex(@"^([\S\s]+;\s+b=)([\S\s]+)$");
			var m = exp2.Match(sign);
			if (!m.Success) {
				return false;
			}
			var paramStr = m.Groups[1].Value;
			var signCode = exp.Replace(m.Groups[2].Value, "");
			var signParams = eml.GetSignHeader(headRelaxed, paramStr, eml.GetHeaderMPKey(DKIMKey), heads);
			if (signParams.IsError) {
				return false;
			}
			if (!_RSA.Verify(hash256 ? "SHA256" : "SHA1", signCode, signParams.Value)) {
				return false;
			}

			return true;
		}
	}














	public class EMail_HOOK_Message_PrepareHeaders : IMethodMonitor {
		/*干掉Date head，用稳定的时间源，不然签名和输出可能不一致*/

		[Monitor("System.Net.Mail", "Message")]
		private void PrepareHeaders(bool sendEnvelope, bool allowUnicode) {
			Original(sendEnvelope, allowUnicode);

			var attr = Method.ReflectedType.GetProperty("Headers", Monitor.AllFlag);
			var headers = (NameValueCollection)attr.GetValue(this);
			if (headers.AllKeys.Contains(EMail_DKIM.DKIMTimeKeyT)) {
				headers.Set("Date", headers[EMail_DKIM.DKIMTimeKeyT]);
				var val = headers[EMail_DKIM.DKIMTimeKeyTS];
				if (val == null) {
					headers[EMail_DKIM.DKIMTimeKeyTS] = "1";
				} else {
					//一次签名，是空的，二次发送，清除数据
					headers.Remove(EMail_DKIM.DKIMTimeKeyTS);
					headers.Remove(EMail_DKIM.DKIMTimeKeyT);
				}
			}
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[Original]
		private void Original(bool sendEnvelope, bool allowUnicode) {
			return;
		}

		static private MethodInfo Method;
		public void SetMethod(MethodInfo method) {
			Method = method;
		}
	}
	public class EMail_HOOK_MimeMultiPart_GetNextBoundary : IMethodMonitor {
		/*手动设置分隔符，不然带附件将每次输出邮件都不一样*/
		static private long UseTime;
		static private string Rnd;
		static private object Lock = new object();
		static private string GetRnd() {
			var now = EMail_Unit.GetMS();
			if (now - UseTime > 30000) {//30秒空闲更新一次
				lock (Lock) {
					now = EMail_Unit.GetMS();
					if (now - UseTime > 30000) {
						Rnd = EMail_Unit.RandomStringMix(20);
						UseTime = now;
					}
				}
			}
			UseTime = now;
			return Rnd;
		}

		[Monitor("System.Net.Mime", "MimeMultiPart")]
		private string GetNextBoundary() {
			return "--boundary" + GetRnd();
		}
		[MethodImpl(MethodImplOptions.NoInlining)]
		[Original]
		private string Original() {
			return null;
		}

		static private MethodInfo Method;
		public void SetMethod(MethodInfo method) {
			Method = method;
		}
	}
	public class EMail_DKIM_RAW_EML {
		public string Body { get; private set; }
		public string Raw { get; private set; }
		private NameValueCollection _Headers { get; set; }
		private EMail_DKIM_RAW_EML() { }

		/// <summary>
		/// 解析一个eml文件
		/// </summary>
		static public EMail_DKIM_RAW_EML ParseOrNull(string eml) {
			var headers = new NameValueCollection();
			using (var reader = new StringReader(eml)) {
				string line;
				string key = null;
				string value = null;

				while ((line = reader.ReadLine()) != null) {
					if (line == "") {
						if (key != null) {
							headers.Add(key, value);
						}
						return new EMail_DKIM_RAW_EML { _Headers = headers, Body = reader.ReadToEnd(), Raw = eml };
					}

					if (key != null && line.Length > 0 && IsWhiteSpace(line[0])) {
						value += "\r\n" + line;
						continue;
					}

					// parse key : value 
					int idx = line.IndexOf(':');
					if (idx == -1) {
						return null;
					}
					if (key != null) {
						headers.Add(key, value);
					}

					key = line.Substring(0, idx);
					value = line.Substring(idx + 1);
				}
			}

			// email must have no body
			return new EMail_DKIM_RAW_EML { _Headers = headers, Body = "", Raw = eml };
		}








		/// <summary>
		/// 获取待签名的header
		/// 
		/// signParams="...; b="。
		/// 
		/// dkimKeyOrNull为null时会格式化signParams用于对发送邮件进行签名，如果是验证签名必须提供这个key，因为不知道源格式是什么样的字符串，而且不会对signParams做任何处理。
		/// 
		/// signHeaderNames要一起签名的header，From一定会有。
		/// 
		///	返回内容(relaxed/simple https://service.mail.qq.com/cgi-bin/help?subtype=1&id=16&no=1001507)：
		///		k:v
		///		k:v
		///		DKIM-Signature:...; b=
		/// </summary>
		public Result<string> GetSignHeader(bool relaxed, string signParams, string dkimKeyOrNull, List<string> signHeaderNames) {
			var rtv = new Result<string>();


			var headVals = new List<string>();
			Action<string, string> addHead = (key, val) => {
				if (relaxed) {
					val = ReduceWitespace(val.Trim());
					headVals.Add(key.Trim().ToLower() + ":" + val);
				} else {
					headVals.Add(key + ":" + val);
				}
			};
			foreach (var key in signHeaderNames) {
				var val = GetHeader(key);

				addHead(key, val);
			}

			if (dkimKeyOrNull != null) {
				addHead(dkimKeyOrNull, signParams);
			} else {
				//生成空白签名内部格式，签名专用
				var msg = new MailMessage("a@q.com", "b@q.com");
				msg.HeadersEncoding = Encoding.UTF8;
				msg.Subject = new string('a', 1024);
				msg.Body = new string('a', 1024);
				msg.Headers.Add(EMail_DKIM.DKIMKey, signParams + new string('0', 70));
				var rawRes = EMail_DKIM_MailMessageText.ToRAW(msg);
				if (rawRes.IsError) {
					rawRes.errorTo(rtv);
					return rtv;
				}
				var wDkim = rawRes.Value.GetHeader(EMail_DKIM.DKIMKey);
				wDkim = wDkim.Substring(0, wDkim.Length - 70);
				addHead(EMail_DKIM.DKIMKey, wDkim);
			}

			rtv.Value = headVals.join("\r\n");
			return rtv;
		}
		/// <summary>
		/// 把一个手写的key映射成请求头中的原生key字符串，因为源里面不知道大小写和有没有空白。
		/// </summary>
		public string GetHeaderMPKey(string key) {
			var keysMp = FixedHeaderLowerKeys;
			return keysMp.getString(key.ToLower());
		}
		/// <summary>
		/// 用一个手写的key获取header里面的内容，因为header里面的key是什么样的完全无法知晓。
		/// </summary>
		public string GetHeader(string key) {
			var keysMp = FixedHeaderLowerKeys;
			return EMail_Unit.STR(_Headers[keysMp.getString(key.ToLower())]);
		}
		private Dictionary<string, string> _KeysMp;
		/// <summary>
		/// 获取一个header的标准化的key->源key映射：key格式化成了没有空白+纯小写。
		/// </summary>
		public Dictionary<string, string> FixedHeaderLowerKeys {
			get {
				if (_KeysMp != null) {
					return _KeysMp;
				}
				var keysRaw = _Headers.AllKeys;
				var keysMp = new Dictionary<string, string>();
				foreach (var k in keysRaw) {
					keysMp[k.Trim().ToLower()] = k;
				}
				_KeysMp = keysMp;
				return keysMp;
			}
		}
		/// <summary>
		/// 从待选的key names列表里面获得header里面存在的key列表。
		/// </summary>
		public List<string> SelectSignHeaderNames(List<string> signHeaderNames) {
			//不支持多个相同的头
			var keysMp = FixedHeaderLowerKeys;
			var keys = keysMp.keys();

			var signHeads = new List<string>();
			foreach (var k in signHeaderNames) {
				if (keys.Contains(k.ToLower())) {
					signHeads.Add(keysMp[k.ToLower()]);
				}
			}
			return signHeads;
		}
		/// <summary>
		/// 获取待签名的body，根据relaxed/simple进行标准化处理。
		/// </summary>
		public string GetSignBody(bool relaxed) {
			var bodySB = new StringBuilder(Body.Length + 99);
			using (var reader = new StringReader(Body)) {
				string line;
				int emptyLineCount = 0;

				while ((line = reader.ReadLine()) != null) {
					if (line == "") {
						emptyLineCount++;
						continue;
					}

					while (emptyLineCount > 0) {
						bodySB.AppendLine();
						emptyLineCount--;
					}

					if (relaxed) {
						bodySB.AppendLine(ReduceWitespace(line.TrimEnd()));
					} else {
						bodySB.Append(line);
					}
				}
				if (!relaxed && bodySB.Length == 0) {
					bodySB.AppendLine();
				}
			}
			return bodySB.ToString();
		}
		private static bool IsWhiteSpace(char c) {
			return c == ' ' || c == '\t' || c == '\r' || c == '\n';
		}
		private static string ReduceWitespace(string text) {
			if (text.IndexOfAny(new char[] { ' ', '\t', '\r', '\n' }) == -1) {
				return text;
			}

			var sb = new StringBuilder(text.Length);
			bool hasWhiteSpace = false;
			foreach (var c in text) {
				if (IsWhiteSpace(c)) {
					hasWhiteSpace = true;
				} else {
					if (hasWhiteSpace) {
						sb.Append(' ');
					}
					sb.Append(c);
					hasWhiteSpace = false;
				}
			}

			return sb.ToString();
		}
	}












	public class EMail_DKIM_MailMessageText {
		/// <summary>
		/// 参考邮件内容的获取
		/// https://github.com/dmcgiv/DKIM.Net/blob/master/src/DKIM.Net/MailMessage/MailMessageText.cs
		/// </summary>

		private static readonly Func<Stream, object> MailWriterFactory;
		private static readonly Action<MailMessage, object, bool, bool> Send3;
		private static readonly Action<MailMessage, object, bool> Send2;
		private static readonly Action<object> Close;

		static EMail_DKIM_MailMessageText() {
			var messageType = typeof(MailMessage);
			var mailWriterType = messageType.Assembly.GetType("System.Net.Mail.MailWriter");

			// mail writer constructor
			try {
				var constructorInfo = mailWriterType.GetConstructor(BindingFlags.Instance | BindingFlags.NonPublic, null, new[] { typeof(Stream) }, null);
				var argument = Expression.Parameter(typeof(Stream), "arg");
				var conExp = Expression.New(constructorInfo, argument);
				MailWriterFactory = Expression.Lambda<Func<Stream, object>>(conExp, argument).Compile();
			} catch { }


			// mail message Send method
			// void Send(BaseWriter writer, Boolean sendEnvelope)
			// void Send(BaseWriter writer, bool sendEnvelope, bool allowUnicode)
			try {
				var sendMethod = messageType.GetMethod("Send", BindingFlags.Instance | BindingFlags.NonPublic);
				var mailWriter = Expression.Parameter(typeof(object), "mailWriter");
				var sendEnvelope = Expression.Parameter(typeof(bool), "sendEnvelope");
				var allowUnicode = Expression.Parameter(typeof(bool), "allowUnicode");
				var instance = Expression.Parameter(messageType, "instance");

				var pars = sendMethod.GetParameters();
				if (pars.Length == 3) {
					var call = Expression.Call(instance, sendMethod, Expression.Convert(mailWriter, mailWriterType),
											   sendEnvelope, allowUnicode);

					Send3 =
						Expression.Lambda<Action<MailMessage, object, bool, bool>>(call, instance, mailWriter,
																				   sendEnvelope, allowUnicode).Compile();
				} else if (pars.Length == 2) {
					var call = Expression.Call(instance, sendMethod, Expression.Convert(mailWriter, mailWriterType),
											  sendEnvelope);

					Send2 =
						Expression.Lambda<Action<MailMessage, object, bool>>(call, instance, mailWriter, sendEnvelope).Compile();
				}
			} catch { }


			// mail writer Close method
			try {
				var closeMethod = mailWriterType.GetMethod("Close", BindingFlags.Instance | BindingFlags.NonPublic);
				var instance = Expression.Parameter(typeof(object), "instance");
				var call = Expression.Call(Expression.Convert(instance, mailWriterType), closeMethod);

				Close = Expression.Lambda<Action<object>>(call, instance).Compile();
			} catch { }
		}


		/// <summary>
		/// 获取邮件的内容
		/// </summary>
		public static Result<EMail_DKIM_RAW_EML> ToRAW(MailMessage message) {
			var rtv = new Result<EMail_DKIM_RAW_EML>();
			if (MailWriterFactory == null || Close == null || Send2 == null && Send3 == null) {
				rtv.error("email内容获取器未初始化成功");
				return rtv;
			}

			var headers = message.Headers;
			//备份hook head
			var hT = headers[EMail_DKIM.DKIMTimeKeyT];
			var hTS = headers[EMail_DKIM.DKIMTimeKeyTS];

			try {
				using (var internalStream = new ClosableMemoryStream()) {
					object mailWriter = MailWriterFactory(internalStream);

					if (Send2 != null) {
						Send2(message, mailWriter, false);
					} else if (Send3 != null) {
						//由smtp.DeliveryFormat决定的allowUnicode为false
						Send3(message, mailWriter, false, false);
					}

					Close(mailWriter);

					internalStream.Position = 0;
					string text;
					using (var reader = new StreamReader(internalStream, Encoding.UTF8)) {
						text = reader.ReadToEnd();
					}

					internalStream.ReallyClose();

					var val = EMail_DKIM_RAW_EML.ParseOrNull(text);
					if (val == null) {
						rtv.error("整个email内容获取后解析失败");
						return rtv;
					}
					rtv.Value = val;
					return rtv;
				}
			} catch (Exception e) {
				rtv.fail("无法获取整个email内容：" + e.Message, e.ToString());
				return rtv;
			} finally {
				//还原hook head
				if (hTS != null) {
					headers.Set(EMail_DKIM.DKIMTimeKeyT, hT);
					headers.Set(EMail_DKIM.DKIMTimeKeyTS, hTS);
				}
			}
		}


		/// <summary>
		/// Use memory stream with dummy Close method as MailWriter writes final CRLF when closing the stream. This allows us to read the stream and close it manually.
		/// </summary>
		private class ClosableMemoryStream : MemoryStream {
			public override void Close() {
				// do not close just yet
			}

			public void ReallyClose() {
				base.Close();
			}
		}
	}
}
