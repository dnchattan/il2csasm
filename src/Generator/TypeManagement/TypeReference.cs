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

		public TypeReference(Type genericType, params TypeReference[] typeArguments)
		{
			Name = genericType.FullName;
			GenericType = genericType;
			TypeArguments = typeArguments;
		}

		public TypeReference(string typeName, Il2CppType cppType, TypeDescriptor typeContext)
		{
			Name = typeName;
			CppType = cppType;
			TypeContext = typeContext;
		}

		public TypeReference(TypeDescriptor typeDescriptor)
		{
			Name = typeDescriptor.FullName;
			TypeDescriptor = typeDescriptor;
		}

		public readonly string Name;
		public readonly Type Type;

		public readonly TypeDescriptor TypeDescriptor;

		public readonly Type GenericType;
		public readonly TypeReference[] TypeArguments;
		
		public readonly Il2CppType CppType;
		public readonly TypeDescriptor TypeContext;
	}

}
