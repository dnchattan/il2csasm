using IL2CS.Core;
using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace IL2CS.Runtime
{
	public class Il2CsRuntimeContext
	{
		private readonly Dictionary<string, long> moduleAddresses = new();
		private readonly ReadProcessMemoryCache rpmCache = new ReadProcessMemoryCache();
		private readonly IntPtr processHandle;
		public Process TargetProcess { get; private set; }
		public Il2CsRuntimeContext(Process target)
		{
			TargetProcess = target;
			processHandle = NativeWrapper.OpenProcess(ProcessAccessFlags.Read, inheritHandle: true, TargetProcess.Id);
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
				return ReadStruct<T>(0);
			}
			return ReadStruct<T>(addyAttr.Address);
		}

		public T ReadStruct<T>(long address) where T : StructBase, new()
		{
			T result = new T();
			result.Load(this, address);
			return result;
		}

		internal long GetModuleAddress(string moduleName)
		{
			if (!moduleAddresses.ContainsKey(moduleName))
			{
				var module = TargetProcess.Modules.OfType<ProcessModule>().FirstOrDefault(m => m.ModuleName == moduleName);
				if (module == null)
				{
					throw new Exception("Unable to locate GameAssembly.dll in memory");
				}

				moduleAddresses.Add(moduleName, (long)module.BaseAddress);
			}
			return moduleAddresses[moduleName];
		}

		internal long ReadPointer(long address)
		{
			ReadOnlyMemory<byte> memory = ReadMemory(address, 8);
			return BitConverter.ToInt64(memory.Span);
		}

		internal ReadOnlyMemory<byte> ReadMemory(long address, long size)
		{
			ReadOnlyMemory<byte>? result = rpmCache.Find(address, size);
			if (result != null)
			{
				return result.Value;
			}
			byte[] buffer = new byte[size];
			if (!NativeWrapper.ReadProcessMemoryArray(processHandle, (IntPtr)address, buffer))
			{
				throw new ApplicationException("Failed to read memory location");
			}
			return buffer;
		}

		internal MemoryCacheEntry CacheMemory(long address, long size)
		{
			MemoryCacheEntry result = rpmCache.FindEntry(address, size);
			if (result != null)
			{
				return result;
			}
			byte[] buffer = new byte[size];
			if (!NativeWrapper.ReadProcessMemoryArray(processHandle, (IntPtr)address, buffer))
			{
				throw new ApplicationException("Failed to read memory location");
			}
			return rpmCache.Store(address, buffer);
		}
	}
}
