using DotNetDetour;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace DKIMSmtp {
	public class EMail : IDisposable {
		static EMail() {
			Monitor.Install();
		}
		static private readonly int defaultTimeout = 10509;

		/// <summary>
		/// 发送超时，默认普通邮件10秒，带附件的根据附件大小计算超时，最小10秒
		/// </summary>
		public int TimeoutMillisecond { get; set; }
		/// <summary>
		/// 客户端名称，默认EMail_SMTP，Hello时发出
		/// </summary>
		public string ClientName { get; set; }


		public string SmtpHost { get; set; }
		public int SmtpPort { get; set; }
		/// <summary>
		/// smtp端口对应的是否需要ssl
		/// </summary>
		public bool SmtpSSL { get; set; }
		/// <summary>
		/// 发送时要登录的账户，如果为空不登录
		/// </summary>
		public string User { get; set; }
		/// <summary>
		/// 发送时要登录的账户的密码
		/// </summary>
		public string Password { get; set; }
		/// <summary>
		/// 发送人地址
		/// </summary>
		public string FromEmail { get; set; }
		/// <summary>
		/// 发送人名称，默认不提供
		/// </summary>
		public string FromEmailName { get; set; }


		/// <summary>
		/// 用smtp地址(port一般25)和账号来创建email
		/// 如果pwd为空不使用账号密码来发邮件
		/// fromEmail不填写默认为userOrFromEmail
		/// </summary>
		public EMail(string smtpHost, int smtpPort, string userOrFromEmail = "", string pwd = "", string fromEmail = null) {
			TimeoutMillisecond = defaultTimeout;
			ClientName = "EMail_SMTP";
			SmtpSSL = false;

			SmtpHost = smtpHost;
			SmtpPort = smtpPort;
			User = "";
			Password = "";
			if (!String.IsNullOrEmpty(pwd)) {
				User = userOrFromEmail;
				Password = pwd;
			}
			FromEmail = String.IsNullOrEmpty(fromEmail) ? userOrFromEmail : fromEmail;
		}


		private List<Attachment> _Attachments = new List<Attachment>();
		private List<int> _AttachmentsLen = new List<int>();
		/// <summary>
		/// 添加一个附件，必须提供附件中文件内容的长度
		/// </summary>
		public void AddAttachment(Attachment item, int fileLen) {
			_Attachments.Add(item);
			_AttachmentsLen.Add(fileLen);
		}
		/// <summary>
		/// 添加一个附件，必须提供流的长度，指定文件名称，mime如果不提供会根据文件后缀来识别
		/// </summary>
		public void AddAttachment(Stream content, int streamLen, string fileName, string mime = "") {
			if (String.IsNullOrEmpty(fileName)) {
				fileName = "附件" + (_Attachments.Count + 1) + ".unknown";
			}
			if (String.IsNullOrEmpty(mime)) {
				mime = MimeMapping.GetMimeMapping(fileName);
			}
			var item = new Attachment(content, fileName, mime);
			item.NameEncoding = Encoding.UTF8;
			AddAttachment(item, streamLen);
		}
		/// <summary>
		/// 添加一个附件，指定文件路径，如果不提供文件名，默认从路径中获取，mime如果不提供会根据文件后缀来识别
		/// </summary>
		public Result AddAttachmentFileOrError(string path, string fileName = "", string mime = "") {
			var rtv = new Result();

			Stream file;
			try {
				if (String.IsNullOrEmpty(fileName)) {
					fileName = new FileInfo(path).Name;
				}

				file = File.OpenRead(path);
			} catch (Exception e) {
				rtv.fail("打开文件出错：" + e.Message, e.ToString());
				return rtv;
			}

			AddAttachment(file, (int)file.Length, fileName, mime);
			return rtv;
		}
		/// <summary>
		/// 添加一个附件，指定文件名称，mime如果不提供会根据文件后缀来识别
		/// </summary>
		public void AddAttachment(byte[] content, string fileName, string mime = "") {
			var stream = new MemoryStream(content);
			AddAttachment(stream, content.Length, fileName, mime);
		}
		/// <summary>
		/// 添加一个附件，指定文件名称，mime如果不提供会根据文件后缀来识别
		/// </summary>
		public void AddAttachment(string content, string fileName, string mime = "") {
			AddAttachment(Encoding.UTF8.GetBytes(content), fileName, mime);
		}



		private List<string> _ToEmails = new List<string>();
		/// <summary>
		/// 设置发送到指定的邮件地址
		/// </summary>
		public EMail ToEmail(params string[] email) {
			_ToEmails.AddRange(email);
			return this;
		}


		private EMail_DKIM _DKIM;
		public const int DKIM_MaxLen = 4 * 1024 * 1024;
		/// <summary>
		/// 尝试使用dkim签名，默认为null不进行签名
		/// 
		///	设置了也不签名条件：	
		///		附件总大小超过DKIM_MaxLen=4M
		/// </summary>
		public EMail TryUseDKIM(EMail_DKIM dkim) {
			_DKIM = dkim;
			return this;
		}



		/// <summary>
		/// 生成邮件内容（未进行DKIM签名），除非你要获得MailMessage否则无需调用此方法
		/// </summary>
		public Result<MailMessage> BuildMessage(string title, string content, out int attachmentLength, bool isHtml = true) {
			var rtv = new Result<MailMessage>();
			var msg = new MailMessage();
			rtv.Value = msg;

			attachmentLength = 0;
			try {
				MailAddress from;
				if (String.IsNullOrEmpty(FromEmailName)) {
					from = new MailAddress(FromEmail);
				} else {
					from = new MailAddress(FromEmail, FromEmailName, Encoding.UTF8);
				}


				msg.From = from;
				foreach (var to in _ToEmails) {
					msg.To.Add(new MailAddress(to));
				}

				msg.HeadersEncoding = Encoding.UTF8;
				msg.Subject = title;
				msg.SubjectEncoding = Encoding.UTF8;
				msg.Body = content;
				msg.BodyEncoding = Encoding.UTF8;
				msg.IsBodyHtml = isHtml;

				if (_Attachments.Count > 0) {
					foreach (var len in _AttachmentsLen) {
						attachmentLength += len;
					}

					foreach (var item in _Attachments) {
						msg.Attachments.Add(item);
					}
				}
			} catch (Exception e) {
				rtv.fail("无法生成邮件内容：" + e.Message, e.ToString());
				return rtv;
			}
			return rtv;
		}

		/// <summary>
		/// 发送之前处理方法，返回false取消发送
		/// </summary>
		public Func<SmtpClient, bool> OnSendBefore { get; set; }
		/// <summary>
		/// 发送邮件
		/// </summary>
		public Result Send(string title, string content, bool isHtml = true) {
			var rtv = new Result();
			if (String.IsNullOrEmpty(SmtpHost)) {
				rtv.error("必须提供发送邮件的smtp地址");
				return rtv;
			}
			if (_ToEmails.Count == 0) {
				rtv.error("需要设置发送到的邮件地址");
				return rtv;
			}

			MailMessage msg = null;
			var smtp = new SmtpClient();

			//SmtpConnection Hello 乱发名字
			try {
				var attr = smtp.GetType().GetField("clientDomain", Monitor.AllFlag);
				attr.SetValue(smtp, ClientName);
			} catch (Exception e) {
				rtv.fail("无法设置客户端名称：" + e.Message, e.ToString());
				return rtv;
			}

			try {
				int dataLen;
				var msgRes = BuildMessage(title, content, out dataLen, isHtml);
				if (msgRes.IsError) {
					msgRes.errorTo(rtv);
					return rtv;
				}
				msg = msgRes.Value;

				var timeout = TimeoutMillisecond;
				if (timeout == defaultTimeout) {
					//重新计算超时时间
					timeout = Math.Max((dataLen / (50 * 1024)) * 1000, defaultTimeout);
				}

				while (_DKIM != null) {
					if (dataLen > DKIM_MaxLen) {
						break;
					}

					var res = _DKIM.Sign(msg);
					if (res.IsError) {
						res.errorTo(rtv);
						return rtv;
					}
					break;
				}

				smtp.Host = SmtpHost;
				smtp.Port = SmtpPort;
				smtp.EnableSsl = SmtpSSL;
				if (!String.IsNullOrEmpty(User)) {
					smtp.Credentials = new NetworkCredential(User, Password);
				}
				smtp.Timeout = timeout;

				if (OnSendBefore != null) {
					if (!OnSendBefore(smtp)) {
						rtv.error("邮件发送前被取消");
						return rtv;
					}
				}

				var task = smtp.SendMailAsync(msg);
				var isout = !task.Wait(timeout);
				if (isout || task.Exception != null) {
					smtp.SendAsyncCancel();
					if (isout) {
						rtv.error("邮件发送超时");
						return rtv;
					}
					throw task.Exception;
				}

				return rtv;
			} catch (AggregateException ex) {
				var e = ex.InnerException;
				rtv.fail("邮件发送出错：" + e.Message, e.ToString());
				return rtv;
			} catch (Exception e) {
				rtv.fail("邮件发送异常：" + e.Message, e.ToString());
				return rtv;
			} finally {
				if (msg != null) {
					msg.Dispose();
				}
				if (smtp != null) {
					smtp.Dispose();
				}
			}
		}

		public void Dispose() {
			foreach (var item in _Attachments) {
				item.Dispose();
			}
		}
	}
}
