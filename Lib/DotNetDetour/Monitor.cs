using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotNetDetour
{
    class DestAndOri
    {
		public IMethodMonitor Obj;
        public MethodInfo Dest;
        public MethodInfo Ori;
    }

	/// <summary>
	/// 初始版本github: https://github.com/bigbaldy1128/DotNetDetour/tree/39e22ea4112fb03e0164fa09cc22f701946e7bd2
	/// </summary>
    public class Monitor {
		static public BindingFlags AllFlag = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static;

        static bool installed = false;
        static List<DestAndOri> destAndOris = new List<DestAndOri>();
        /// <summary>
        /// 安装监视器
        /// </summary>
        public static void Install(string dir = null)
        {
            if (installed)
                return;
            installed = true;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            IEnumerable<IMethodMonitor> monitors;
            if (string.IsNullOrEmpty(dir))
            {
                monitors = assemblies.SelectMany(t => t.GetImplementedObjectsByInterface<IMethodMonitor>());
            }
            else
            {
                monitors = Directory
                            .GetFiles(dir)
                            .SelectMany(d => Assembly.LoadFrom(d).GetImplementedObjectsByInterface<IMethodMonitor>());
            }
            foreach (var monitor in monitors)
            {
                DestAndOri destAndOri = new DestAndOri();
				destAndOri.Obj = monitor;
                destAndOri.Dest= monitor
                            .GetType()
							.GetMethods(AllFlag)
                            .FirstOrDefault(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(MonitorAttribute)));
                destAndOri.Ori = monitor
                            .GetType()
							.GetMethods(AllFlag)
                            .FirstOrDefault(t => t.CustomAttributes.Any(a => a.AttributeType == typeof(OriginalAttribute)));
                if (destAndOri.Dest != null)
                {
                    destAndOris.Add(destAndOri);
                }
            }
            InstallInternal(assemblies);
            AppDomain.CurrentDomain.AssemblyLoad += CurrentDomain_AssemblyLoad;
        }

		//查找最优函数，忽略参数匹配
		private static MethodInfo getFn(Type type, string name, Type[] paramTypes) {
			var all = type.GetMethods(AllFlag);
			MethodInfo rtv = null;
			var find = 0;
			foreach (var item in all) {
				if (item.Name == name) {
					rtv = item;
					find++;
				}
			}
			if (find == 0) {
				return null;
			}
			if (find == 1) {
				return rtv;
			}

			//获取最佳的
			var list = new List<fnItem>();
			foreach (var item in all) {
				if (item.Name == name) {
					int score = 0;
					var ps = item.GetParameters();
					score += 100 - Math.Abs(paramTypes.Length - ps.Length);
					//参数数量不一样基本忽略
					if (score == 100) {
						for (int i = 0, len = paramTypes.Length; i < len; i++) {
							if (paramTypes[i] == ps[i].ParameterType) {
								score += 100;
							}
						}
					}
					list.Add(new fnItem() { fn = item, score = score });
				}
			}
			list.Sort((a, b) => {
				return b.score - a.score;
			});
			return list[0].fn;
		}
		private class fnItem {
			public MethodInfo fn;
			public int score;
		}
        private static void InstallInternal(Assembly[] assemblies)
        {
            foreach (var destAndOri in destAndOris)
            {
                MethodInfo src = null;
                var dest = destAndOri.Dest;
                var monitorAttribute = dest.GetCustomAttribute(typeof(MonitorAttribute)) as MonitorAttribute;
                var methodName = dest.Name;
                var paramTypes = dest.GetParameters().Select(t => t.ParameterType).ToArray();
                if (monitorAttribute.Type != null)
                {
					src = getFn(monitorAttribute.Type, methodName, paramTypes);
                }
                else
                {
                    var srcNamespaceAndClass = monitorAttribute.NamespaceName + "." + monitorAttribute.ClassName;
                    foreach (var asm in assemblies)
                    {
                        var type= asm.GetTypes().FirstOrDefault(t => t.FullName == srcNamespaceAndClass);
                        if (type != null)
                        {
							src = getFn(type, methodName, paramTypes);
                            break;
                        }
                    }
                }
                if (src == null)
					continue;
				destAndOri.Obj.SetMethod(src);
                var ori = destAndOri.Ori;
                var engine = DetourFactory.CreateDetourEngine();
                engine.Patch(src, dest, ori);
            }
        }

        private static void CurrentDomain_AssemblyLoad(object sender, AssemblyLoadEventArgs args)
        {
            InstallInternal(new[] { args.LoadedAssembly });
        }
    }
}
