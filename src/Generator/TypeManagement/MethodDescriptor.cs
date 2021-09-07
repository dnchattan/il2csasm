using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Generator.TypeManagement
{
	public class MethodDescriptor
	{
		public MethodDescriptor(string name, ulong address)
		{
			Name = name;
			Address = address;
		}

		public readonly string Name;
		public readonly ulong Address;
		public readonly List<TypeReference> DeclaringTypeArgs = new();
	}
}
