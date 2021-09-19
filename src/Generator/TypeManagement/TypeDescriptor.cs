using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using Il2CppDumper;
using IL2CS.Runtime;
using static Il2CppDumper.Il2CppConstants;

namespace IL2CS.Generator.TypeManagement
{
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class TypeDescriptor
	{
		public TypeDescriptor(string name, Il2CppTypeDefinition typeDef, int typeIndex, Il2CppImageDefinition imageDef)
		{
			m_name = name;
			TypeDef = typeDef;
			TypeIndex = typeIndex;
			ImageDef = imageDef;
		}

		public ulong Tag
		{
			get
			{
				return Utilities.GetTypeTag(ImageDef.typeStart, TypeDef.token);
			}
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

		public bool IsStatic
		{
			get
			{
				return (TypeDef.flags & TYPE_ATTRIBUTE_ABSTRACT) != 0 && (TypeDef.flags & TYPE_ATTRIBUTE_SEALED) != 0;
			}
		}
		public readonly Il2CppImageDefinition ImageDef;
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
		public readonly List<MethodDescriptor> Methods = new();

		private string DebuggerDisplay => string.Join(" : ", Name, Base?.Name).TrimEnd(new char[] { ' ', ':' });
	}
}
