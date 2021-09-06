using System;
using System.Diagnostics;
using System.Reflection;
// ReSharper disable InconsistentNaming

namespace IL2CS.Runtime.Types.corelib
{
	[TypeMapping(typeof(Nullable<>))]
	public struct Native__Nullable<T>
	{
		private static uint Native__ValueSize
		{
			get
			{
				if (typeof(T).IsPointer)
				{
					return 8;
				}
				else if (Types.TypeSizes.TryGetValue(typeof(T), out int size))
				{
					return (uint)size;
				}

				if (!typeof(T).IsAssignableTo(typeof(StructBase)))
				{
					// TODO: Catch if there are other cases we didn't anticipate
					throw new NotSupportedException("Unexpected type === unknown size");
				}
				return 8;
			}
		}
		
		public T Value { get; private set; }
		public bool HasValue { get; private set; }

		// ReSharper disable once UnusedMember.Local
		private void ReadFields(long address, Il2CsRuntimeContext context)
		{
			ReadOnlyMemory<byte> hasValue = context.ReadMemory(address + Native__ValueSize, 1);
			HasValue = BitConverter.ToBoolean(hasValue.Span);
			if (!HasValue)
			{
				return;
			}

			byte indirection = 1;
			Type valueType = typeof(T);
			while (valueType.IsPointer)
			{
				++indirection;
				valueType = valueType.GetElementType();
			}

			long valueOffset = address;
			for (; indirection > 1; --indirection)
			{
				valueOffset = context.ReadPointer(valueOffset);
				if (valueOffset == 0)
				{
					return;
				}
			}

			Value = (T)context.ReadValue(typeof(T), valueOffset, indirection);
		}
	}
}
