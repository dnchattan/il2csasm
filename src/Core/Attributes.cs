using System;

namespace IL2CS.Core
{
	[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
	public class StaticAttribute : Attribute
	{
		public StaticAttribute()
		{
		}
	}

	[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public class IgnoreAttribute : Attribute
	{
	}

	[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public class OffsetAttribute : Attribute
	{
		public int OffsetBytes { get; private set; }
		public OffsetAttribute(int offset)
		{
			OffsetBytes = offset;
		}
	}

	[AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public class IndirectionAttribute : Attribute
	{
		public byte Indirection { get; private set; }
		public IndirectionAttribute(byte indirection)
		{
			Indirection = indirection;
		}
	}

	[AttributeUsage(AttributeTargets.Class | AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
	public class AddressAttribute : Attribute
	{
		public long Address { get; private set; }
		public string RelativeToModule { get; private set; }
		public AddressAttribute(long address, string relativeToModule)
		{
			Address = address;
			RelativeToModule = relativeToModule;
		}
	}

	[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
	public class SizeAttribute : Attribute
	{
		public uint Size { get; private set; }
		public SizeAttribute(uint size)
		{
			Size = size;
		}
	}
}