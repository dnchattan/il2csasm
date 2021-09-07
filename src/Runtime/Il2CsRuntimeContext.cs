using IL2CS.Core;
using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using IL2CS.Runtime.Types.corelib;

namespace IL2CS.Runtime
{
	public class Il2CsRuntimeContext
	{
		private readonly Dictionary<string, ulong> moduleAddresses = new();
		private readonly ReadProcessMemoryCache rpmCache = new();
		private readonly IntPtr processHandle;
		public Process TargetProcess { get; }
		public Il2CsRuntimeContext(Process target)
		{
			TargetProcess = target;
			processHandle = NativeWrapper.OpenProcess(ProcessAccessFlags.Read, inheritHandle: true, TargetProcess.Id);
		}

		public ulong GetMemberFieldOffset(FieldInfo field)
		{
			AddressAttribute addressAttr = field.GetCustomAttribute<AddressAttribute>(inherit: true);
			if (addressAttr != null)
			{
				ulong address = addressAttr.Address;
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
		
		public virtual void ReadFields(Type type, object target, ulong targetAddress)
		{
			MethodInfo readFieldsOverride = type.GetMethod(
				"ReadFields",
				BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public,
				null,
				CallingConventions.HasThis,
				new []
				{
					typeof(Il2CsRuntimeContext),
					typeof(ulong),
				},
				null);

			if (readFieldsOverride != null)
			{
				readFieldsOverride.Invoke(target, new object[]{ this, targetAddress });
				return;
			}

			FieldInfo[] fields = type.GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			foreach (FieldInfo field in fields)
			{
				ReadField(target, targetAddress, field);
			}
		}

		public void ReadField(object target, ulong targetAddress, FieldInfo field)
		{
			ulong offset = targetAddress + GetMemberFieldOffset(field);
			byte indirection = 1;
			IndirectionAttribute indirectionAttr = field.GetCustomAttribute<IndirectionAttribute>(inherit: true);
			if (indirectionAttr != null)
			{
				indirection = indirectionAttr.Indirection;
			}

			object result = ReadValue(field.FieldType, offset, indirection);
			field.SetValue(target, result);
		}

		public T ReadValue<T>(ulong address, byte indirection)
		{
			return (T)ReadValue(typeof(T), address, indirection);
		}
		
		public object ReadValue(Type type, ulong address, byte indirection)
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
			if (type == typeof(string))
			{
				return ReadString(address);
			}
			if (type.IsEnum)
			{
				return this.ReadPrimitive(type.GetEnumUnderlyingType(), address);
			}
			if (type.IsPrimitive)
			{
				return this.ReadPrimitive(type, address);
			}
			return ReadStruct(type, address);
		}

		private object ReadString(ulong address)
		{
			Native__String? str = ReadValue<Native__String>(address, 2);
			return str?.Value;
		}

		public T ReadStruct<T>()
		{
			return (T)ReadStruct(typeof(T));
		}

		public object ReadStruct(Type type)
		{
			bool isStatic = type.GetCustomAttribute<StaticAttribute>(inherit: true) != null;
			if (!isStatic)
			{
				throw new ApplicationException($"Type '{type.FullName}' does not have the required [Static]");
			}
			if (!type.IsAssignableTo(typeof(StructBase)))
			{
				throw new ArgumentException($"Type '{type.FullName}' is not a supported type");
			}
			return Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new object[] { this, (ulong)0 }, null);
		}

		public object ReadStruct(Type type, ulong address)
		{
			if (type.IsInterface)
			{
				// TODO
				return null;
			}
			if (type.IsAssignableTo(typeof(StructBase)))
			{
				return Activator.CreateInstance(type, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new object[] { this, address }, null);
			}
			// value type
			object result = Activator.CreateInstance(type);
			ReadFields(type, result, address);
			return result;
		}

		internal ulong GetModuleAddress(string moduleName)
		{
			if (moduleAddresses.ContainsKey(moduleName)) 
				return moduleAddresses[moduleName];

			ProcessModule module = TargetProcess.Modules.OfType<ProcessModule>().FirstOrDefault(m => m.ModuleName == moduleName);
			
			if (module == null)
				throw new Exception("Unable to locate GameAssembly.dll in memory");

			moduleAddresses.Add(moduleName, (ulong)module.BaseAddress);
			return moduleAddresses[moduleName];
		}

		internal ulong ReadPointer(ulong address)
		{
			ReadOnlyMemory<byte> memory = ReadMemory(address, 8);
			return BitConverter.ToUInt64(memory.Span);
		}

		internal ReadOnlyMemory<byte> ReadMemory(ulong address, ulong size)
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

		internal MemoryCacheEntry CacheMemory(ulong address, ulong size)
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
