using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Il2CppDumper;
using IL2CS.Runtime;
using Microsoft.Extensions.Logging;

namespace IL2CS.Generator.TypeManagement
{
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class TypeDescriptor
	{
		public TypeDescriptor(string name, Il2CppTypeDefinition typeDef, int typeIndex)
		{
			m_name = name;
			TypeDef = typeDef;
			TypeIndex = typeIndex;
		}

		public string Name
		{
			get
			{
				if (GenericParameterNames.Length == 0)
				{
					return m_name;
				}
				return $"{m_name}`{GenericParameterNames.Length }";
			}
		}
		public string FullName
		{
			get
			{
				if (DeclaringParent != null)
				{
					return $"{DeclaringParent.FullName}+{m_name.Split('.').Last()}";
				}
				return Name;
			}
		}

		private readonly string m_name;

		public readonly Il2CppTypeDefinition TypeDef;
		public readonly int TypeIndex;
		public readonly List<TypeReference> Implements = new();
		public readonly List<TypeDescriptor> NestedTypes = new();
		public TypeDescriptor DeclaringParent;
		public TypeDescriptor GenericParent;
		public TypeReference[] GenericTypeParams;
		public TypeReference Base;
		public TypeAttributes Attributes;
		public string[] GenericParameterNames = Array.Empty<string>();
		public readonly List<FieldDescriptor> Fields = new();

		private string DebuggerDisplay => string.Join(" : ", Name, Base?.Name).TrimEnd(new char[] { ' ', ':' });
	}
}
