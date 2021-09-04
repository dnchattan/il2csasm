using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Runtime.Types.corelib
{
	[TypeMapping(typeof(Nullable<>))]
	public class Native__Nullable<T> : StructBase where T : struct
	{
		protected uint Native__ValueSize
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

				// TODO: Check if there are other cases we don't know about
				return 8;
			}
		}

		protected override uint? Native__ObjectSize => Native__ValueSize + 1;

#pragma warning disable IDE0044 // Add readonly modifier
		private T m_value;
#pragma warning restore IDE0044 // Add readonly modifier
		private bool m_hasValue = false;

		public T Value => m_value;
		public bool HasValuie => m_hasValue;

		protected override void ReadFields()
		{
			var hasValue = Context.ReadMemory(Address + Native__ValueSize, 1);
			m_hasValue = BitConverter.ToBoolean(hasValue.Span);
			if (!m_hasValue)
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

			long valueOffset = Address;
			for (; indirection > 1; --indirection)
			{
				valueOffset = Context.ReadPointer(valueOffset);
				if (valueOffset == 0)
				{
					return;
				}
			}

			FieldInfo field = GetType().GetField("m_value");
			if (valueType.IsAssignableTo(typeof(StructBase)))
			{
				StructBase result = (StructBase)Activator.CreateInstance(valueType);
				result.Load(Context, valueOffset);
				field.SetValue(this, result);
			}
			else
			{
				var memory = Context.ReadMemory(valueOffset, Native__ValueSize);
				switch(valueType.Name)
				{
					case "Int64":
						{
							field.SetValue(this, BitConverter.ToInt64(memory.Span));
							break;
						}
					case "UInt64":
						{
							field.SetValue(this, BitConverter.ToUInt64(memory.Span));
							break;
						}
					case "Int32":
						{
							field.SetValue(this, BitConverter.ToInt32(memory.Span));
							break;
						}
					case "UInt32":
						{
							field.SetValue(this, BitConverter.ToUInt32(memory.Span));
							break;
						}
					case "Int16":
						{
							field.SetValue(this, BitConverter.ToInt16(memory.Span));
							break;
						}
					case "UInt16":
						{
							field.SetValue(this, BitConverter.ToUInt16(memory.Span));
							break;
						}
					case "Char":
						{
							field.SetValue(this, BitConverter.ToChar(memory.Span));
							break;
						}
					case "Double":
						{
							field.SetValue(this, BitConverter.ToDouble(memory.Span));
							break;
						}
					case "Single":
						{
							field.SetValue(this, BitConverter.ToSingle(memory.Span));
							break;
						}
					case "Boolean":
						{
							field.SetValue(this, BitConverter.ToBoolean(memory.Span));
							break;
						}
				}
			}
		}
	}
}
