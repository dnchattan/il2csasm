using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Il2CppDumper;
using IL2CS.Runtime;
using Microsoft.Extensions.Logging;
using static Il2CppDumper.Il2CppConstants;

namespace IL2CS.Generator
{
	public class TypeCollector
	{
		public TypeCollector(AssemblyGeneratorContext context)
		{
			m_context = context;
			m_logger = context.Options.LogFactory.CreateLogger("type-collector");
		}

		public void VisitTypeDefinition(Il2CppTypeDefinition typeDef)
		{
			Il2CppType typeInfo = m_context.Executor.GetIl2CppTypeFromTypeDefinition(typeDef);
			VisitTypeUsage(typeInfo);
		}

		public void VisitTypeUsage(Il2CppType typeInfo)
		{
			GetTypeDescriptor(typeInfo);
		}

		public TypeDescriptor GetTypeDescriptor(Il2CppType typeInfo)
		{
			string typeName = m_context.Executor.GetTypeName(typeInfo, true, false);

			if (m_typeCache.TryGetValue(typeName, out TypeDescriptor cachedType))
			{
				return cachedType;
			}
			TypeDescriptor td = CreateTypeDescriptor(typeName, typeInfo);
			return td;
		}

		private TypeDescriptor CreateTypeDescriptor(string typeName, Il2CppType typeInfo)
		{
			TypeDescriptor td = new(typeName, typeInfo);
			m_typeCache.Add(typeName, td);
			if (typeInfo.type != Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
			{
				m_typesToEmit.Add(td);
			}

			// NB: Will get generic type definition if this is a generic instance type
			Il2CppTypeDefinition typeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(typeInfo);
			TypeAttributes attribs = GetTypeAttributes(typeDef);
			
			// base class?
			if (!attribs.HasFlag(TypeAttributes.Interface) && typeDef.parentIndex >= 0 && !typeDef.IsValueType && !typeDef.IsEnum)
			{
				Il2CppType parent = m_context.Il2Cpp.types[typeDef.parentIndex];
				string parentName = m_context.Executor.GetTypeName(parent, true, false);
				if (parentName != "System.Object")
				{
					td.Base = GetTypeDescriptor(parent);
				}
			}

			// nested type?
			if (typeDef.declaringTypeIndex != -1)
			{
				Il2CppType declaringType = m_context.Il2Cpp.types[typeDef.declaringTypeIndex];
				td.DeclaringParent = GetTypeDescriptor(declaringType);
			}

			// interfaces
			if (typeDef.interfaces_count > 0)
			{
				for (int i = 0; i < typeDef.interfaces_count; i++)
				{
					Il2CppType interfaceType = m_context.Il2Cpp.types[m_context.Metadata.interfaceIndices[typeDef.interfacesStart + i]];
					td.Implements.Add(GetTypeDescriptor(interfaceType));
				}
			}

			// generic
			if (typeInfo.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
			{
				Il2CppType genericTypeInfo = m_context.Executor.GetIl2CppTypeFromTypeDefinition(typeDef);
				if (genericTypeInfo == typeInfo)
				{
					m_logger.LogError($"Invalid type: {typeName} (self-referencing-generic)");
					throw new ApplicationException($"Invalid type: {typeName} (self-referencing-generic)");
				}
				td.GenericParent = GetTypeDescriptor(genericTypeInfo);
			}

			return td;
		}

		private static TypeAttributes GetTypeAttributes(Il2CppTypeDefinition typeDef)
		{
			TypeAttributes attrs = default(TypeAttributes);
			var visibility = typeDef.flags & TYPE_ATTRIBUTE_VISIBILITY_MASK;
			switch (visibility)
			{
				case TYPE_ATTRIBUTE_PUBLIC:
					attrs |= TypeAttributes.Public;
					break;
				case TYPE_ATTRIBUTE_NESTED_PUBLIC:
					attrs |= TypeAttributes.NestedPublic;
					break;
				case TYPE_ATTRIBUTE_NOT_PUBLIC:
					attrs |= TypeAttributes.NotPublic;
					break;
				case TYPE_ATTRIBUTE_NESTED_FAM_AND_ASSEM:
					attrs |= TypeAttributes.NestedFamANDAssem;
					break;
				case TYPE_ATTRIBUTE_NESTED_ASSEMBLY:
					attrs |= TypeAttributes.NestedAssembly;
					break;
				case TYPE_ATTRIBUTE_NESTED_PRIVATE:
					attrs |= TypeAttributes.NestedPrivate;
					break;
				case TYPE_ATTRIBUTE_NESTED_FAMILY:
					attrs |= TypeAttributes.NestedFamily;
					break;
				case TYPE_ATTRIBUTE_NESTED_FAM_OR_ASSEM:
					attrs |= TypeAttributes.NestedFamORAssem;
					break;
			}
			if ((typeDef.flags & TYPE_ATTRIBUTE_ABSTRACT) != 0 && (typeDef.flags & TYPE_ATTRIBUTE_SEALED) != 0)
				attrs |= TypeAttributes.NotPublic;
			else if ((typeDef.flags & TYPE_ATTRIBUTE_INTERFACE) == 0 && (typeDef.flags & TYPE_ATTRIBUTE_ABSTRACT) != 0)
				attrs |= TypeAttributes.Abstract;
			else if (!typeDef.IsValueType && !typeDef.IsEnum && (typeDef.flags & TYPE_ATTRIBUTE_SEALED) != 0)
				attrs |= TypeAttributes.Sealed;
			if ((typeDef.flags & TYPE_ATTRIBUTE_INTERFACE) != 0)
				attrs |= TypeAttributes.Interface | TypeAttributes.Abstract;
			return attrs;
		}

		[DebuggerDisplay("{DebuggerDisplay,nq}")]
		public class TypeDescriptor
		{
			public TypeDescriptor(string name, Il2CppType cppType)
			{
				Name = name;
				CppType = cppType;
				m_hashCode = Name.GetHashCode();
			}

			public override int GetHashCode()
			{
				return m_hashCode;
			}

			private string DebuggerDisplay => string.Join(" : ", Name, Base?.Name).TrimEnd(new char[] { ' ', ':' });

			private readonly int m_hashCode;
			public readonly string Name;
			public readonly Il2CppType CppType;
			public readonly List<TypeDescriptor> Implements = new();
			public TypeDescriptor DeclaringParent;
			public TypeDescriptor GenericParent;
			public TypeDescriptor Base;
		}

		private readonly AssemblyGeneratorContext m_context;
		private readonly ILogger m_logger;
		private readonly Dictionary<string, TypeDescriptor> m_typeCache = new();
		private readonly List<TypeDescriptor> m_typesToEmit = new();
	}
}
