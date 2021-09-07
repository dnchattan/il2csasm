using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Runtime
{
	public class UnknownClass : StructBase
	{
		public UnknownClass(Il2CsRuntimeContext context, ulong address) : base(context, address)
		{
		}
	}
}
