using DKIMSmtp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace DNS {
	public class DNS_A : DNSBase {
		[StructLayout(LayoutKind.Sequential)]
		private class A : Record {
			//https://docs.microsoft.com/zh-cn/windows/desktop/api/windns/ns-windns-__unnamed_struct_2
			public uint IpAddress;
		}

		protected override Type RecordType { get { return typeof(A); } }
		protected override string GetVal(object obj) {
			return new IPAddress(((A)obj).IpAddress).ToString();
		}
	}
	public class DNS_MX : DNSBase {
		[StructLayout(LayoutKind.Sequential)]
		private class MX : Record {
			public IntPtr pNameExchange;
			public short wPreference;
			public short Pad;
		}

		protected override Type RecordType { get { return typeof(MX); } }
		protected override string GetVal(object obj) {
			return Marshal.PtrToStringUni(((MX)obj).pNameExchange);
		}
	}
	public class DNS_CNAME : DNSBase {
		[StructLayout(LayoutKind.Sequential)]
		private class CNAME : Record {
			public IntPtr pNameHost;
		}

		protected override Type RecordType { get { return typeof(CNAME); } }
		protected override string GetVal(object obj) {
			return Marshal.PtrToStringUni(((CNAME)obj).pNameHost);
		}
	}
	public class DNS_NS : DNSBase {
		[StructLayout(LayoutKind.Sequential)]
		private class NS : Record {
			public IntPtr pNameHost;
		}

		protected override Type RecordType { get { return typeof(NS); } }
		protected override string GetVal(object obj) {
			return Marshal.PtrToStringUni(((NS)obj).pNameHost);
		}
	}
	public class DNS_PTR : DNSBase {
		[StructLayout(LayoutKind.Sequential)]
		private class PTR : Record {
			public IntPtr pNameHost;
		}

		protected override Type RecordType { get { return typeof(PTR); } }
		protected override string GetVal(object obj) {
			return Marshal.PtrToStringUni(((PTR)obj).pNameHost);
		}
	}
	public class DNS_TXT : DNSBase {
		[StructLayout(LayoutKind.Sequential)]
		private class TXT : Record {
			public uint dwStringCount;
			public IntPtr pStringArray;
		}

		protected override Type RecordType { get { return typeof(TXT); } }
		protected override string GetVal(object obj) {
			return Marshal.PtrToStringUni(((TXT)obj).pStringArray);
		}
	}


	[StructLayout(LayoutKind.Sequential)]
	public abstract class DNSBase {
		abstract protected Type RecordType { get; }
		abstract protected string GetVal(object obj);

		static private Dictionary<string, int> Types = new Dictionary<string, int>();
		/// <summary>
		/// 注册一个类型，比如A,0x1，类型参考https://docs.microsoft.com/zh-cn/windows/desktop/DNS/dns-constants中的DNS Record Types
		/// </summary>
		static public void RegisterType(string type, int value) {
			Types[type] = value;
		}
		static DNSBase() {
			RegisterType("A", 0x0001);
			RegisterType("MX", 0x000f);
			RegisterType("CNAME", 0x0005);
			RegisterType("NS", 0x0002);
			RegisterType("PTR", 0x000c);
			RegisterType("TXT", 0x0010);
		}


		/// <summary>
		/// 查询一条记录，如果没有记录或查询失败会返回原因
		/// </summary>
		public Result<string> QueryOne(string domain, DNSQueryOptions options = DNSQueryOptions.STANDARD) {
			var rtv = new Result<string>();
			var res = QueryAll(domain, options);
			if (res.IsError) {
				res.errorTo(rtv);
				return rtv;
			}
			var val = res.Value;
			if (val.Count == 0) {
				rtv.error("未查询到" + RecordType.Name + "记录");
				return rtv;
			}

			rtv.Value = val[0];
			return rtv;
		}
		/// <summary>
		/// 查询此类型的记录列表，如果查询失败会返回原因
		/// </summary>
		public Result<List<string>> QueryAll(string domain, DNSQueryOptions options = DNSQueryOptions.STANDARD) {
			var rtv = new Result<List<string>>();
			List<string> list = new List<string>();
			rtv.Value = list;

			if (String.IsNullOrEmpty(domain)) {
				rtv.error("查询" + RecordType.Name + "记录域名不能为空");
				return rtv;
			}
			try {
				IntPtr ptr1 = IntPtr.Zero;
				IntPtr ptr2 = IntPtr.Zero;
				Record rec;

				var type = Types[RecordType.Name];
				int num = DnsQuery(ref domain, type, options, 0, ref ptr1, 0);
				if (num != 0) {
					rtv.error("查询出错[" + num + "]");
					return rtv;
				}
				for (ptr2 = ptr1; !ptr2.Equals(IntPtr.Zero); ptr2 = rec.pNext) {
					rec = (Record)Marshal.PtrToStructure(ptr2, RecordType);
					if (rec.wType == type) {
						list.Add(GetVal(rec));
					}
				}
				DnsRecordListFree(ptr2, 0);

				return rtv;
			} catch (Exception e) {
				rtv.fail("查询" + RecordType.Name + "记录出错：" + e.Message, e.ToString());
				return rtv;
			}
		}






		[DllImport("dnsapi", EntryPoint = "DnsQuery_W", CharSet = CharSet.Unicode, SetLastError = true, ExactSpelling = true)]
		private static extern int DnsQuery([MarshalAs(UnmanagedType.VBByRefStr)]ref string pszName, int wType, DNSQueryOptions options, int aipServers, ref IntPtr ppQueryResults, int pReserved);

		[DllImport("dnsapi", CharSet = CharSet.Auto, SetLastError = true)]
		private static extern void DnsRecordListFree(IntPtr pRecordList, int FreeType);


		[StructLayout(LayoutKind.Sequential)]
		protected abstract class Record {
			//https://docs.microsoft.com/zh-cn/windows/desktop/api/windns/ns-windns-_dnsrecorda 固定数据
			public IntPtr pNext;
			public IntPtr pName;
			public short wType;
			public short wDataLength;
			public int flags;
			public int dwTtl;
			public int dwReserved;
		}
	}


	//https://docs.microsoft.com/zh-cn/windows/desktop/DNS/dns-constants
	public enum DNSQueryOptions {
		STANDARD = 0x00000000,
		ACCEPT_TRUNCATED_RESPONSE = 0x00000001,
		USE_TCP_ONLY = 0x00000002,
		NO_RECURSION = 0x00000004,
		BYPASS_CACHE = 0x00000008,
		NO_WIRE_QUERY = 0x00000010,
		NO_LOCAL_NAME = 0x00000020,
		NO_HOSTS_FILE = 0x00000040,
		NO_NETBT = 0x00000080,
		WIRE_ONLY = 0x00000100,
		RETURN_MESSAGE = 0x00000200,
		MULTICAST_ONLY = 0x00000400,
		NO_MULTICAST = 0x00000800,
		TREAT_AS_FQDN = 0x00001000,
		ADDRCONFIG = 0x00002000,
		DUAL_ADDR = 0x00004000,
		MULTICAST_WAIT = 0x00020000,
		MULTICAST_VERIFY = 0x00040000,
		DONT_RESET_TTL_VALUES = 0x00100000,
		DISABLE_IDN_ENCODING = 0x00200000,
		APPEND_MULTILABEL = 0x00800000
		//RESERVED = 0xf0000000
	}
}
