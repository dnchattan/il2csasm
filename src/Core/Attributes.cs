using System;

namespace il2cs.Assembly
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
        public UIntPtr OffsetBytes { get; private set; }
        public OffsetAttribute(uint offset)
        {
            OffsetBytes = (UIntPtr)offset;
        }
    }

    [AttributeUsage(AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class IndirectionAttribute : Attribute
    {
        public UIntPtr Indirection { get; private set; }
        public IndirectionAttribute(uint indirection)
        {
            Indirection = (UIntPtr)indirection;
        }
    }

    [AttributeUsage(AttributeTargets.Class| AttributeTargets.Field, Inherited = true, AllowMultiple = false)]
    public class AddressAttribute : Attribute
    {
        public IntPtr Address { get; private set; }
        public AddressAttribute(int address)
        {
            Address = (IntPtr)address;
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