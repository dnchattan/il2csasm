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
		private readonly ReadProcessMemoryCache rpmCache = new();
		private readonly IntPtr processHandle;
		public Process TargetProcess { get; private set; }
		public Il2CsRuntimeContext(Process target)
		{
			TargetProcess = target;
			processHandle = NativeWrapper.OpenProcess(ProcessAccessFlags.Read, inheritHandle: true, TargetProcess.Id);
		}

		public long GetMemberFieldOffset(FieldInfo field)
		{
			AddressAttribute addressAttr = field.GetCustomAttribute<AddressAttribute>(inherit: true);
			if (addressAttr != null)
			{
				long address = addressAttr.Address;
				if (!string.IsNullOrEmpty(addressAttr.RelativeToModule))
				{
					address += GetModuleAddress(addressAttr.RelativeToModule);
				}
				return address;
			}
			
			OffsetAttribute offsetAttr = field.GetCustomAttribute<OffsetAttribute>(inherit: true);
			if (offsetAttr == null)
			{
				throw new ApplicationException($"Field {field.Name} requires an OffsetAttribute");
			}
			return offsetAttr.OffsetBytes;
		}

		public void ReadFieldsT(object target, long targetAddress)
		{
			GetType().GetMethod("ReadFields").MakeGenericMethod(target.GetType()).Invoke(this, new [] {target, targetAddress});
		}

		public virtual void ReadFields<T>(T target, long targetAddress)
		{
			Type type = target.GetType();

			MethodInfo readFieldsOverride = type.GetMethod(
				"ReadFields",
				BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public,
				null,
				CallingConventions.HasThis,
				new Type[]
				{
					typeof(long),
					typeof(Il2CsRuntimeContext)
				},
				null);

			if (readFieldsOverride != null)
			{
				readFieldsOverride.Invoke(target, Array.Empty<object>());
				return;
			}

			FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			foreach (FieldInfo field in fields)
			{
				ReadField(target, targetAddress, field);
			}
		}

		public void ReadField<T>(T target, long targetAddress, FieldInfo field)
		{
			long offset = targetAddress + GetMemberFieldOffset(field);
			byte indirection = 1;
			IndirectionAttribute indirectionAttr = field.GetCustomAttribute<IndirectionAttribute>(inherit: true);
			if (indirectionAttr != null)
			{
				indirection = indirectionAttr.Indirection;
			}

			T result = ReadValue<T>(offset, indirection);
			field.SetValue(target, result);
		}

		public T ReadValue<T>(long address, byte indirection)
		{
			for (; indirection > 1; --indirection)
			{
				address = ReadPointer(address);
				if (address == 0)
				{
					return default;
				}
			}
			if (address == 0)
			{
				return default;
			}
			if (typeof(T).IsAssignableTo(typeof(StructBase)))
			{
				return ReadStruct<T>(address);
			}
			if (typeof(T).IsPrimitive)
			{
				return this.ReadPrimitive<T>(address);
			}

			if (typeof(T).IsValueType)
			{
				return ReadStruct<T>(address);
			}
			throw new ApplicationException("Unknown type");
		}

		public T ReadStruct<T>()
		{
			bool isStatic = typeof(T).GetCustomAttribute<StaticAttribute>(inherit: true) != null;
			if (!isStatic)
			{
				throw new ApplicationException($"Type '{typeof(T).FullName}' does not have the required [Static]");
			}
			if (!typeof(T).IsAssignableTo(typeof(StructBase)))
			{
				throw new ArgumentException($"Type '{typeof(T).FullName}' is not a supported type");
			}
			T result = (T)Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new object[] { this, (long)0 }, null);
			return result;
		}

		public T ReadStruct<T>(long address)
		{
			if (!typeof(T).IsAssignableTo(typeof(StructBase)))
			{
				throw new ArgumentException($"Type '{typeof(T).FullName}' is not a supported type");
			}
			T result = (T)Activator.CreateInstance(typeof(T), BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new object[] { this, address }, null);
			return result;
		}

		internal long GetModuleAddress(string moduleName)
		{
			if (moduleAddresses.ContainsKey(moduleName)) 
				return moduleAddresses[moduleName];

			ProcessModule module = TargetProcess.Modules.OfType<ProcessModule>().FirstOrDefault(m => m.ModuleName == moduleName);
			
			if (module == null)
				throw new Exception("Unable to locate GameAssembly.dll in memory");

			moduleAddresses.Add(moduleName, (long)module.BaseAddress);
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
