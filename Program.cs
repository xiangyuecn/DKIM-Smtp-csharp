using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;

namespace DKIMSmtp {
	class Program {
		static void EMailTest() {
			var domain = "test.localhost";
			var selector = "op";

			//DKIM签名私钥
			var rsa = new RSA.RSA(@"
-----BEGIN RSA PRIVATE KEY-----
MIIBOwIBAAJBALYyNggDBZ2SjrnZsOolGW1UWjf5Mt6P3zmitHctuOv8TvkdAAnH
knzM8soIWXFUjQ3yTSweJ54hvX2UmDpjEe0CAwEAAQJAM7vkLbg18v03e7w9iO7J
3opyJ6yh7iJqHyJ9Hc4k0RTT69q+rTWky2NNOQjDIb7dFiN8soXSttkgxJWHpvS1
XQIhANphUALecb2vyi9fVtZsLf+IPHQQSxmaRwpszmXqvD/jAiEA1ZUp/cLuPhvL
aVGoAOpMI3+tTrAS+rD4ynS9m+pQb+8CIQDWGgQ029wNyhRi/4kGrocmeW4zqGnI
zy4JNYXh/BLWWwIgMnLhUEdS7uixy1a2UEEHavslfIiqcvyKR4f7oXBfP5ECIQDK
B4JqZMGSBWkvkoGZOoTEr5UF7/EUeZIjux3wm2wYXA==
-----END RSA PRIVATE KEY-----
", true);
			Console.WriteLine("【DKIM测试公钥512位（PEM）】：");
			Console.WriteLine(rsa.ToPEM_PKCS1(true));
			Console.WriteLine();


			var dkim = new EMail_DKIM(domain, selector, rsa);

			Action<Action<EMail>> create = (call) => {
				//投递邮件给qq服务器
				using (var email = new EMail("mx1.qq.com", 25)) {
					//使用签名
					email.TryUseDKIM(dkim);

					email.FromEmail = "test@" + domain;
					email.ToEmail("11111111@qq.com");//改成有效的邮箱地址

					//添加附件
					email.AddAttachment("abc文本内容123", "文本.txt");
					email.AddAttachment("未命名文件内容，红红火火恍恍惚惚", "");

					call(email);
				}
			};




			create((email) => {
				//发送邮件出去，去垃圾箱找，如果私钥是域名设置的话正常点
				var res = email.Send("标题", "内容");

				Console.WriteLine("【邮件发送】");
				Console.WriteLine(res.IsError ? "失败：" + res.ErrorMessage : "成功");
				Console.WriteLine();
			});




			create((email) => {
				int len;
				var msg = email.BuildMessage("标题", "内容", out len).Value;

				//手动签名
				dkim.Sign(msg);

				//获取邮件内容
				var raw = EMail_DKIM_MailMessageText.ToRAW(msg);
				if (raw.IsError) {
					Console.WriteLine("获取邮件内容出错：" + raw.ErrorMessage);
					return;
				}

				Console.WriteLine("【验证签名】");
				Console.WriteLine(dkim.Verify(raw.Value) ? "合法" : "非法");
				Console.WriteLine();


				Console.WriteLine("【邮件(.eml)内容】");
				Console.WriteLine(raw.Value.Raw);
			});

			create((email) => {
				//设置发送到文件夹内，保存邮件，不投递
				var dir = "d:/.email测试保存邮件文件夹";
				Directory.CreateDirectory(dir);

				email.OnSendBefore = (smtp) => {
					smtp.DeliveryMethod = SmtpDeliveryMethod.SpecifiedPickupDirectory;
					smtp.PickupDirectoryLocation = dir;
					return true;
				};
				var res = email.Send("标题", "内容");
				Console.WriteLine("【另类保存】");
				Console.WriteLine("目录：" + dir);
				Console.WriteLine("结果：" + (res.IsError ? "失败，" + res.ErrorMessage : "已保存"));
			});
		}




		static void Main(string[] args) {
			Console.WriteLine("---------------------------------------------------------");
			Console.WriteLine("◆◆◆◆◆◆◆◆◆◆◆◆ EMail测试 ◆◆◆◆◆◆◆◆◆◆◆");
			Console.WriteLine("---------------------------------------------------------");

			EMailTest();

			Console.WriteLine("-------------------------------------------------------------");
			Console.WriteLine("◆◆◆◆◆◆◆◆◆◆◆◆ 回车退出... ◆◆◆◆◆◆◆◆◆◆◆◆");
			Console.WriteLine();
			Console.ReadLine();
		}
	}
}
