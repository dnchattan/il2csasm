using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Il2CppDumper;

namespace IL2CS.Generator.TypeManagement
{
	public class TypeReference
	{
		public TypeReference(Type type)
		{
			Name = type.FullName;
			Type = type;
		}

		public TypeReference(string typeName, Il2CppType cppType, TypeDescriptor typeContext)
		{
			Name = typeName;
			CppType = cppType;
			TypeContext = typeContext;
		}

		public readonly string Name;
		public readonly Type Type;
		public readonly Il2CppType CppType;
		public readonly TypeDescriptor TypeContext;
	}

}
