using IL2CS.Core;
using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using System;
using System.Reflection;

namespace IL2CS.Runtime
{
	public abstract class StructBase
	{
		public Il2CsRuntimeContext Context { get; set; }
		public long Address { get; set; }
		public bool Static { get; set; }
		private MemoryCacheEntry Cache { get; set; }

		public void Load(Il2CsRuntimeContext context, long address)
		{
			Context = context;
			Address = address;
			Static = GetType().GetCustomAttribute<StaticAttribute>(inherit: true) != null;
			EnsureCache();
			ReadFields();
		}

		public T As<T>() where T : StructBase,new()
		{
			T cast = new();
			cast.Load(Context, Address);
			return cast;
		}

		private void ReadFields()
		{
			FieldInfo[] fields = GetType().GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);

			foreach (FieldInfo field in fields)
			{
				ReadField(field);
			}
		}

		private void ReadField(FieldInfo field)
		{
			long offset = GetFieldAddress(field);
			byte indirection = 1;
			IndirectionAttribute indirectionAttr = field.GetCustomAttribute<IndirectionAttribute>(inherit: true);
			if (indirectionAttr != null)
			{
				indirection = indirectionAttr.Indirection;
			}
			for (; indirection > 1; --indirection)
			{
				offset = Context.ReadPointer(offset);
			}
			if (field.FieldType.IsAssignableTo(typeof(StructBase)))
			{
				StructBase result = (StructBase)Activator.CreateInstance(field.FieldType);
				result.Load(Context, offset);
				field.SetValue(this, result);
			}
			else if (field.FieldType.IsPrimitive)
			{
				if (field.FieldType.Name == "Int64")
				{
					var memory = Context.ReadMemory(offset, 8);
					field.SetValue(this, BitConverter.ToInt64(memory.Span));
				}
			}
		}

		private long GetFieldAddress(FieldInfo field)
		{
			if (Static)
			{
				AddressAttribute addressAttr = field.GetCustomAttribute<AddressAttribute>(inherit: true);
				if (addressAttr == null)
				{
					throw new ApplicationException($"Field {field.Name} requires an AddressAttribute");
				}
				long address = addressAttr.Address;
				if (!string.IsNullOrEmpty(addressAttr.RelativeToModule))
				{
					address = address + Context.GetModuleAddress(addressAttr.RelativeToModule);
				}
				return address;
			}
			else
			{
				OffsetAttribute offsetAttr = field.GetCustomAttribute<OffsetAttribute>(inherit: true);
				if (offsetAttr == null)
				{
					throw new ApplicationException($"Field {field.Name} requires an OffsetAttribute");
				}
				return Address + offsetAttr.OffsetBytes;
			}
		}

		private void EnsureCache()
		{
			if (Cache != null)
			{
				return;
			}
			SizeAttribute sizeAttr = GetType().GetCustomAttribute<SizeAttribute>(inherit: true);
			if (sizeAttr == null)
			{
				return;
			}
			IntPtr handle = NativeWrapper.OpenProcess(ProcessAccessFlags.Read, inheritHandle: true, Context.TargetProcess.Id);
			byte[] buffer = new byte[sizeAttr.Size];
			if (!NativeWrapper.ReadProcessMemoryArray(handle, (IntPtr)Address, buffer))
			{
				throw new ApplicationException("Failed to read memory location");
			}
			Cache = Context.CacheMemory(Address, sizeAttr.Size);
		}

	}

	public abstract class StaticStructBase : StructBase
	{
	}
}
