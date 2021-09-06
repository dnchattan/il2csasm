using IL2CS.Core;
using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using System;
using System.Reflection;

namespace IL2CS.Runtime
{
	public abstract class StructBase
	{
		private bool m_isLoaded = false;
		private MemoryCacheEntry m_cache;

		public Il2CsRuntimeContext Context { get; set; }
		public long Address { get; set; }

		protected virtual uint? Native__ObjectSize
		{
			get
			{
				SizeAttribute sizeAttr = GetType().GetCustomAttribute<SizeAttribute>(inherit: true);
				return sizeAttr?.Size;
			}
		}

		protected StructBase(Il2CsRuntimeContext context, long address)
		{
			Context = context;
			Address = address;
		}

		public T As<T>() where T : StructBase
		{
			T cast = Context.ReadValue<T>(Address, 1);
			return cast;
		}

		protected internal virtual void Load()
		{
			if (m_isLoaded)
			{
				return;
			}
			m_isLoaded = true;
			EnsureCache();
			Context.ReadFieldsT(this, Address);
		}

		protected virtual void EnsureCache()
		{
			if (m_cache != null || !Native__ObjectSize.HasValue || Address == 0)
			{
				return;
			}
			uint? size = Native__ObjectSize;
			IntPtr handle = NativeWrapper.OpenProcess(ProcessAccessFlags.Read, inheritHandle: true, Context.TargetProcess.Id);
			byte[] buffer = new byte[size.Value];
			if (!NativeWrapper.ReadProcessMemoryArray(handle, (IntPtr)Address, buffer))
			{
				throw new ApplicationException("Failed to read memory location");
			}
			m_cache = Context.CacheMemory(Address, size.Value);
		}
	}
}
