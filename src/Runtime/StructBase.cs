using il2cs.Assembly;
using ProcessMemoryUtilities.Managed;
using ProcessMemoryUtilities.Native;
using System;
using System.Reflection;

namespace Runtime
{
	public abstract class StructBase
	{
		public Il2CsRuntimeContext Context { get; set; }
		public IntPtr Address {get; set;}
		private byte[] Buffer { get; set; }

		public void Load(Il2CsRuntimeContext context,  IntPtr address)
		{
			Context = context;
			Address = address;
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
			AddressAttribute addressAttr = field.GetCustomAttribute<AddressAttribute>(inherit: true);
			if (addressAttr != null)
			{
				
			}
			OffsetAttribute offsetAttr = field.GetCustomAttribute<OffsetAttribute>(inherit: true);
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
