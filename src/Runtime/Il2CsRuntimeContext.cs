using IL2CS.Core;
using System;
using System.Diagnostics;
using System.Reflection;

namespace IL2CS.Runtime
{
	public class Il2CsRuntimeContext
	{
		public Process TargetProcess { get; private set; }
		public Il2CsRuntimeContext(Process target)
		{
			TargetProcess = target;
		}

		public T ReadStruct<T>() where T : StructBase, new()
		{
			AddressAttribute addyAttr = typeof(T).GetCustomAttribute<AddressAttribute>(true);
			if (addyAttr == null)
			{
				StaticAttribute staticAttr = typeof(T).GetCustomAttribute<StaticAttribute>(true);
				if (staticAttr == null)
				{
					throw new ApplicationException("Struct type does not have a static address!");
				}
				return ReadStruct<T>(IntPtr.Zero);
			}
			return ReadStruct<T>(addyAttr.Address);
		}

		public T ReadStruct<T>(IntPtr address) where T : StructBase, new()
		{
			T result = new T();
			result.Load(this, address);
			return result;
		}
	}
}
