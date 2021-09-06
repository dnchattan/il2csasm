using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Runtime
{
	public static class BitReader
	{
		private static class BitReaderImpl<T>
		{
			public static Func<Il2CsRuntimeContext, long, T> Read;
		}
		static BitReader()
		{
			// ReSharper disable BuiltInTypeReferenceStyle
			BitReaderImpl<Char>.Read = (context, address) => BitConverter.ToChar(context.ReadMemory(address, sizeof(Char)).Span);
			BitReaderImpl<Boolean>.Read = (context, address) => BitConverter.ToBoolean(context.ReadMemory(address, sizeof(Boolean)).Span);
			BitReaderImpl<Double>.Read = (context, address) => BitConverter.ToDouble(context.ReadMemory(address, sizeof(Double)).Span);
			BitReaderImpl<Single>.Read = (context, address) => BitConverter.ToSingle(context.ReadMemory(address, sizeof(Single)).Span);
			BitReaderImpl<Int16>.Read = (context, address) => BitConverter.ToInt16(context.ReadMemory(address, sizeof(Int16)).Span);
			BitReaderImpl<Int32>.Read = (context, address) => BitConverter.ToInt32(context.ReadMemory(address, sizeof(Int32)).Span);
			BitReaderImpl<Int64>.Read = (context, address) => BitConverter.ToInt64(context.ReadMemory(address, sizeof(Int64)).Span);
			BitReaderImpl<UInt16>.Read = (context, address) => BitConverter.ToUInt16(context.ReadMemory(address, sizeof(UInt16)).Span);
			BitReaderImpl<UInt32>.Read = (context, address) => BitConverter.ToUInt32(context.ReadMemory(address, sizeof(UInt32)).Span);
			BitReaderImpl<UInt64>.Read = (context, address) => BitConverter.ToUInt64(context.ReadMemory(address, sizeof(UInt64)).Span);
			// ReSharper restore BuiltInTypeReferenceStyle
		}
		public static T ReadPrimitive<T>(this Il2CsRuntimeContext context, long address)
		{
			if (BitReaderImpl<T>.Read != null)
			{
				return BitReaderImpl<T>.Read(context, address);
			}
			throw new ArgumentException($"Type '{typeof(T).FullName}' is not a valid primitive type");
		}
	}
}
