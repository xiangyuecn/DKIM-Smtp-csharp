# DKIM-Smtp-csharp 帮助文档


## 跑起来

clone下来用vs应该能够直接打开，经目测看起来没什么卵用的文件都svn:ignore掉了（svn滑稽。


## 主要支持

- 对邮件进行DKIM签名
- 对整个邮件内容（.eml文件）的DKIM签名进行验证
- 对`MailMessage`、`SmtpClient`进行了一次封装，发送邮件简单易用


## DKIM签名、验证规则

对于DKIM的签名和验证规则，QQ邮箱的《[DKIM指引](https://service.mail.qq.com/cgi-bin/help?subtype=1&id=16&no=1001507)》这个文章已经写的足够详细，就不搬运了。

还不行还可以去参考[DKIM.Net](https://github.com/dmcgiv/DKIM.Net)库对签名的实现。


## 举个例子

``` C#
//创建DKIM签名对象
var dkim = new EMail_DKIM("domain.com", "dkimSelector", new RSA.RSA(/*"-----BEGIN RSA PRIVATE KEY-----....", true*/ 1024));

//通过EMail类来操作发邮件
using (var email = new EMail("mx1.qq.com", 25)) {
	//使用签名
	email.TryUseDKIM(dkim);

	email.FromEmail = "test@test.test";
	email.ToEmail("11111111@qq.com");//改成有效的邮箱地址

	//发送邮件出去，去垃圾箱找，如果私钥是域名设置的话正常点
	var res = email.Send("标题", "内容");
	Console.WriteLine(res.IsError ? "发送失败:" + res.ErrorMessage : "发送成功");
}

//直接给MailMessage签名
var msg = new MailMessage("test@test.test", "11111111@qq.com");
msg.SubjectEncoding = msg.BodyEncoding = msg.HeadersEncoding = Encoding.UTF8;
msg.Subject = "标题";
msg.Body = "内容";
msg.Attachments.Add(new Attachment(new MemoryStream(Encoding.UTF8.GetBytes("abc文本内容123")), "文本.txt"));

//签名
Console.WriteLine(dkim.Sign(msg).IsError ? "签名失败" : "签名完成");

//获取邮件内容
var eml = EMail_DKIM_MailMessageText.ToRAW(msg).Value;

//验证eml文件签名
Console.WriteLine(dkim.Verify(eml) ? "验证通过" : "验证未通过");

//邮件整体内容
Console.WriteLine(eml.Raw);
```

输出：
```
发送失败:邮件发送出错：邮箱不可用。 服务器响应为:Mailbox not found. http://servi
ce.mail.qq.com/cgi-bin/help?subtype=1&&id=20022&&no=1000728
签名完成
验证通过
MIME-Version: 1.0
From: test@test.test
To: 11111111@qq.com
Date: Sun, 11 Nov 2018 05:31:55 +000
Subject: =?utf-8?B?5qCH6aKY?=
DKIM-Signature: v=1; a=rsa-sha256; c=relaxed/relaxed; d=domain.com;
 s=dkimSelector; q=dns/txt; t=1541914315; h=Date:From:Subject:To;
 bh=iKgtfjx6cvO8YCUPyjjnbHU9jziQ+q1c/Hrz0aRDb98=;
 b=CidpxecyNHkZGsIQGnUD8eQwrEGS+Nx09RUOff6hU/7H1DV50m/h0xqRLFlgskiqm1r0exDTPf/zS
CKui1WWNO5iKXSZt9/3s0YN9fhliP72c0GRIJ8DM3tQilVYgFnayK61jmvCW0gtrPd3biDdMp/s+Arq8
lWD6CbQfBMIPmQ=
Content-Type: multipart/mixed; boundary=--boundaryhRN0aXVHKzDLi76qUZTq


----boundaryhRN0aXVHKzDLi76qUZTq
Content-Type: text/plain; charset=utf-8
Content-Transfer-Encoding: base64

5YaF5a65
----boundaryhRN0aXVHKzDLi76qUZTq
Content-Type: application/octet-stream; name="=?utf-8?B?5paH5pysLnR4dA==?="
Content-Transfer-Encoding: base64
Content-Disposition: attachment

YWJj5paH5pys5YaF5a65MTIz
----boundaryhRN0aXVHKzDLi76qUZTq--
```


## 方法文档

### EMail_DKIM.cs
邮件进行DKIM签名和验证的所有代码都在里面。

`EMail_DKIM`类：提供签名`Sign`和验证`Verify`。

`EMail_DKIM_RAW_EML`类：提供`ParseOrNull`用来解析一封.eml文件内容。

`EMail_DKIM_MailMessageText`类：提供`ToRAW`用来获取`MailMessage`的全部内容，并转成`EMail_DKIM_RAW_EML`格式。

### EMail.cs
封装的一个发送邮件的功能。

主要提供`TimeoutMillisecond`,`ClientName`设置，一堆添加附件的方法`AddAttachment(x,x,x)`，最后`Send`发送邮件。

### EMail_Unit.cs
封装的一些通用方法，如：base64。都是比较周边的功能。

### /Lib/RSA-csharp目录
这个目录里面是我的[RSA-csharp](https://github.com/xiangyuecn/RSA-csharp)仓库代码，用来解析PEM秘钥对的。

### /Lib/DotNetDetour目录
这个目录里面是[DotNetDetour](https://github.com/bigbaldy1128/DotNetDetour)库，使用的[这个版本](https://github.com/bigbaldy1128/DotNetDetour/tree/39e22ea4112fb03e0164fa09cc22f701946e7bd2)代码。已经修改过，用来支持私有方法的hook。





## 前言、自述、还有啥

在实现邮件发送时发现就算不把邮件投递到自己的邮件服务器（由邮件服务服务器进行发送给对方），有些邮箱（`QQ邮箱`）不会拒绝，但有邮箱直接就拒绝了（`网易邮箱`）。对比由邮件服务器发送的和直接发送的邮件内容的区别，发现直接发送缺少了`DKIM-Signature`邮件头。

好了，缺少那就加上。但.Net的`MailMessage`、`SmtpClient`简陋到一份邮件发送到一个`Stream`的接口都不舍得暴露（任性写入到文件夹不给文件名却支持），直接就没有支持签名的头绪。那自行实现。

研究了一下[RFC 6376](http://www.ietf.org/rfc/rfc6376.txt)长篇大论看不懂（主要没给一个简单的实现步骤），然后[QQ给的](https://service.mail.qq.com/cgi-bin/help?subtype=1&id=16&no=1001507)简单易懂多了（流式.清晰）。签名和验证算法就清楚了。


#### 发现DKIM.Net

要签名先搞定`body`的`hash计算`，`body`怎么获取？一堆附件、一堆转码...... `MailMessage`、`SmtpClient`没给获取`body`的支持。然后找到一个库 [DKIM.Net](https://github.com/dmcgiv/DKIM.Net)，他里面实现了获取整个邮件内容的方法，简单调用一下`MailMessage`的私有方法搞定。

然后遇到了`DKIM.Net`也没有搞定的问题，对于带附件`Attachments`或`AlternateViews`的邮件，由于每次获取的邮件内容因为`boundary`分隔符（边界）不一致导致签名无效，`DKIM.Net`是直接粗暴的拒绝`multipart`格式邮件的签名的。然后翻阅[.NET Framework MimeMultiPart源码](https://referencesource.microsoft.com/#System/net/System/Net/mail/MimeMultiPart.cs,2615996f074ad959,references)找到了以下代码：
``` c#
internal string GetNextBoundary() {
	int b = Interlocked.Increment(ref boundary) - 1;
	string boundaryString = "--boundary_" + b.ToString(CultureInfo.InvariantCulture)+"_"+Guid.NewGuid().ToString(null, CultureInfo.InvariantCulture);

	return boundaryString;
}
```
这个方法只会在`MimeMultiPart`初始化时调用一次，`MimeMultiPart`的初始化时机在`MailMessage.Send`调用时，通过`MailMessage.SetContent`来初始化。而私有方法`MailMessage.Send`是发送邮件时才会调用到的，我们获取邮件内容也是通过这个方法。如果我们通过手段使`MimeMultiPart.GetNextBoundary`返回的`boundary`相同，那么每次获取的邮件内容也会相同了（Date相同的情况下）。


#### 发现DotNetDetour

然后就是寻找控制`MimeMultiPart.GetNextBoundary`函数的方法。研究了半天反射，没有找到头绪，反射能替换一个类实例的方法为另一个方法？然后顺着查找`C# hook`，找到多篇一样的内容，还是看原创吧《[自己写的一个可以hook .net方法的库](https://bbs.csdn.net/topics/391958344)》，内容本身并不感冒（没看懂），但结尾一句话`但hook一般都需要dll注入来配合，因为hook自身进程没什么意义，hook别人的进程才有意义`，咦，搞自己，有意思，然后仔细看了一下代码，没错！这就是我要的功能，修改类的一个方法为另外一个方法！[DotNetDetour](https://github.com/bigbaldy1128/DotNetDetour)库。


#### Date隐患

因为签名和发送是在不同时间内，就有可能导致签名时是8:05.999，而发送时是8:06.001，从而导致带Date header的签名失败，但签名时建议携带Date header一起签名。

so 这个问题hook `System.Net.Mail.Message.PrepareHeaders` 可以解决，每次原始函数处理完成后我们获取`System.Net.Mail.Message.Headers`，然后把Date header删掉，然后写入我们可以控制的值。


#### 对DotNetDetour的修改

测试`DotNetDetour`过程中发现他不能 hook 非public的方法，然后魔改了一下`Monitor.cs`，主要在反射获取类的方法的时候添加了flags参数，用来获取类的所有方法。

使用中给`IMethodMonitor`接口加了一个`void SetMethod(MethodInfo method)`方法，用来把原始方法信息传递给我们自己的方法，简化我们自己函数内的反射操作（获取.Net框架内的类型敲的字符串比较复杂，有了MethodInfo就是一个属性调用的事）。

#### 准备好了

有了`DKIM.Net`提供的思路来获取邮件内容，搬来`DotNetDetour` hook修改 .Net系统类的方法，邮件DKIM签名唾手可得~

参考文章：

《[C#发送DKIM签名的邮件](https://www.cnblogs.com/dudu/archive/2013/03/02/csharp-dkim-sendmail.html)》：发现DKIM.Net

《[RFC 6376](http://www.ietf.org/rfc/rfc6376.txt)》：DKIM签名规则

《[DKIM指引](https://service.mail.qq.com/cgi-bin/help?subtype=1&id=16&no=1001507)》：QQ提供的DKIM签名、验证规则文档

《[自己写的一个可以hook .net方法的库](https://bbs.csdn.net/topics/391958344)》：发现DotNetDetour

《[DKIM 测试](http://www.appmaildev.com/cn/dkim)》：测试签名，测试前提：需要有一个自己的域名，并配置邮箱域名的DKIM公钥


## 附

### 任意邮箱收件地址查询

比如qq邮箱，`smtp.qq.com`这种是发件用的地址，不是收件地址，接收邮件的地址需要进行mx查询。有了收件地址就可以发送任意邮件给他，他收不收是另外一回事，比如伪造发件人。

mx查询方法：

比如查询qq邮箱的收件地址
``` cmd
> nslookup
> set type=mx
> qq.com

非权威应答:
qq.com  MX preference = 30, mail exchanger = mx1.qq.com
qq.com  MX preference = 20, mail exchanger = mx2.qq.com
qq.com  MX preference = 10, mail exchanger = mx3.qq.com
```

然后响应的mail exchanger就是收件地址，随便挑一个给他发垃圾邮件。


### 邮箱域名DKIM公钥查询

要验证一份邮件的签名，需要先获取公钥（有私钥用私钥验证也可以）。给个邮箱然后查询公钥的方法（比如QQ邮箱）：

步骤1：打开邮件源码获取到`DKIM-Signature`中的s参数(`selector`)，QQ为`s201512`
步骤2：和QQ邮箱拼接出ns txt记录名称：`s201512._domainkey.qq.com`
``` cmd
> nslookup
> set type=txt
> s201512._domainkey.qq.com

非权威应答:
s201512._domainkey.qq.com       text =

        "v=DKIM1; k=rsa; p=MIGfMA0GCSqGSIb3DQEBAQUAA4GNADCBiQKBgQDPsFIOSteMStsN6
15gUWK2RpNJ/B/ekmm4jVlu2fNzXADFkjF8mCMgh0uYe8w46FVqxUS97habZq6P5jmCj/WvtPGZAX49j
mdaB38hzZ5cUmwYZkdue6dM17sWocPZO8e7HVdq7bQwfGuUjVuMKfeTB3iNeo6/hFhb9TmUgnwjpQIDA
QAB"
```

然后响应的text内的p参数就是公钥了，copy出来拼成PEM格式就可以拿来进行DKIM验证。


### 线上DKIM签名测试

测试需要有一个域名并且配置好相应ns DKIM的 txt记录。

本次测试实例代码：
``` c#
var rsa = new RSA.RSA(@"-----BEGIN RSA PRIVATE KEY-----
私钥内容
-----END RSA PRIVATE KEY-----
", true);

var mail = new EMail("mail.appmaildev.com", 25);
mail.TryUseDKIM(new EMail_DKIM("email.jiebian.life", "email", rsa));
mail.FromEmail = "test-7ea72484@email.jiebian.life";
mail.ToEmail("test-7ea72484@appmaildev.com");
var res=mail.Send("测试", "测试内容");
Console.WriteLine(res.IsError?"发送失败:"+res.ErrorMessage:"发送成功");
```

本次测试报告：见`images/report-7ea72484.txt`


## 相关截图

控制台运行：

![控制台运行](images/3.png)

开始线上测试：

![开始测试](images/1.png)

测试线上结果：

![测试结果](images/2.png)