using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Runtime.Types.corelib
{
	public struct Native__LPSTR
	{
		public string Value;
		// ReSharper disable once UnusedMember.Local
		private void ReadFields(Il2CsRuntimeContext context, long address)
		{
			address = context.ReadPointer(address);
			ReadOnlyMemory<byte> stringData = context.ReadMemory(address, 512);
			Value = Encoding.UTF8.GetString(stringData.Span).Split('\0', 2)[0];
		}
	}
}
