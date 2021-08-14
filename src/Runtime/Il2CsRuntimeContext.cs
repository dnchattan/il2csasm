using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using System;
using System.Diagnostics;
using il2cs.Assembly;
using System.Reflection;

namespace Runtime
{
    public class Il2CsRuntimeContext
    {
        public Process TargetProcess {get; private set;}
        public Il2CsRuntimeContext(Process target)
        {
            TargetProcess = target;
        }

        public T ReadStruct<T>() where T : StructBase, new()
        {
            AddressAttribute addyAttr = typeof(T).GetCustomAttribute<AddressAttribute>(true);
            if (addyAttr == null)
            {
                throw new ApplicationException("Struct type does not have a static address!");
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
