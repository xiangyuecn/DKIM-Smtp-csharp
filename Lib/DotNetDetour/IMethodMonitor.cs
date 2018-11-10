using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace DotNetDetour
{
    public interface IMethodMonitor {
		void SetMethod(MethodInfo method);
    }
}
