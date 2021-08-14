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
		public IntPtr Address { get; set; }
		public bool Static { get; set; }
		private byte[] Buffer { get; set; }

		public void Load(Il2CsRuntimeContext context, IntPtr address)
		{
			Context = context;
			Address = address;
			Static = GetType().GetCustomAttribute<StaticAttribute>(inherit: true) != null;
			ReadBuffer();
			ReadFields();
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
			IntPtr offset = GetFieldAddress(field);
			byte indirection = 1;
			IndirectionAttribute indirectionAttr = field.GetCustomAttribute<IndirectionAttribute>(inherit: true);
			if (indirectionAttr != null)
			{
				indirection = indirectionAttr.Indirection;
			}
		}

		private IntPtr GetFieldAddress(FieldInfo field)
		{
			if (Static)
			{
				AddressAttribute addressAttr = field.GetCustomAttribute<AddressAttribute>(inherit: true);
				if (addressAttr == null)
				{
					throw new ApplicationException($"Field {field.Name} requires an AddressAttribute");
				}
				return addressAttr.Address;
			}
			else
			{
				OffsetAttribute offsetAttr = field.GetCustomAttribute<OffsetAttribute>(inherit: true);
				if (offsetAttr == null)
				{
					throw new ApplicationException($"Field {field.Name} requires an OffsetAttribute");
				}
				return IntPtr.Add(Address, offsetAttr.OffsetBytes.ToInt32());
			}
		}

		private void ReadBuffer()
		{
			SizeAttribute sizeAttr = GetType().GetCustomAttribute<SizeAttribute>(inherit: true);
			if (sizeAttr == null)
			{
				return;
			}
			IntPtr handle = NativeWrapper.OpenProcess(ProcessAccessFlags.Read, inheritHandle: true, Context.TargetProcess.Id);
			Buffer = new byte[sizeAttr.Size];
			if (!NativeWrapper.ReadProcessMemoryArray(handle, Address, Buffer))
			{
				throw new ApplicationException("Failed to read memory location");
			}
		}

		public T GetStaticFields<T>() where T : StructBase, new()
		{
			return Context.ReadStruct<T>(Address + 184);
		}
	}
}
