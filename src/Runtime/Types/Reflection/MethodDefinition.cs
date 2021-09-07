using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Runtime.Types.Reflection
{
	public class MethodDefinition
	{
		private readonly long m_address;
		private readonly string m_moduleName;
		public MethodDefinition(long address, string moduleName)
		{
			m_address = address;
			m_moduleName = moduleName;
		}

		// ReSharper disable once UnusedMember.Global
		public static MethodDefinition Lookup(Type type, Dictionary<Type, MethodDefinition> table)
		{
			return table.TryGetValue(type, out MethodDefinition method) ? method : null;
		}

		public NativeMethodInfo Get(Il2CsRuntimeContext context)
		{
			long address = m_address + context.GetModuleAddress(m_moduleName);
			return context.ReadValue<NativeMethodInfo>(address, 2);
		}
	}
}
