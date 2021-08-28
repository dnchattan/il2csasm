using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using Il2CppDumper;
using Microsoft.Extensions.Logging;

namespace IL2CS.Generator
{
	public class TypeCollector
	{
		public TypeCollector(AssemblyGeneratorContext context)
		{
			m_context = context;
			m_logger = context.Options.LogFactory.CreateLogger("type-collector");
			m_buildTypeCallback = (descriptor) => BuildType(descriptor);
			m_buildTypeReferenceCallback = (reference) => BuildTypeReference(reference);
		}

		public void CollectTypeDefinition(Il2CppTypeDefinition typeDef)
		{
			GetTypeDescriptor(typeDef);
		}

		public TypeDescriptor GetTypeDescriptor(Il2CppTypeDefinition typeDef)
		{
			string typeName = m_context.Executor.GetTypeDefName(typeDef, addNamespace: true, genericParameter: false);
			if (m_typeCache.TryGetValue(typeDef, out TypeDescriptor td))
			{
				return td;
			}
			return MakeTypeDescriptor(typeName, typeDef);
		}

		public TypeDescriptor MakeTypeDescriptor(string typeName, Il2CppTypeDefinition typeDef)
		{
			Il2CppType typeInfo = m_context.Executor.GetIl2CppTypeFromTypeDefinition(typeDef);
			TypeDescriptor td = new(typeName, typeDef, m_buildTypeCallback);
			m_typesToEmit.Enqueue(td);
			m_typeCache.Add(typeDef, td);
			return td;
		}

		private TypeDescriptor GetTypeDescriptorOrThrow(int typeIndex)
		{
			Il2CppType cppType = m_context.Il2Cpp.types[typeIndex];
			Il2CppTypeDefinition typeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(cppType);
			if (m_typeCache.TryGetValue(typeDef, out TypeDescriptor value))
			{
				return value;
			}

			if (typeDef == null)
			{
				string typeDefName = m_context.Executor.GetTypeName(cppType, true, false);
				m_logger.LogWarning($"Could not find type definition '${typeDefName}'");
				throw new ApplicationException($"Could not find type definition '${typeDefName}'");
			}

			return GetTypeDescriptor(typeDef);
		}

		private TypeReference GetTypeReference(int typeIndex)
		{
			Il2CppType cppType = m_context.Il2Cpp.types[typeIndex];
			string name = m_context.Executor.GetTypeName(cppType, true, false);
			return new(name, cppType, m_buildTypeReferenceCallback);
		}

		private string GetTypeName(int typeIndex)
		{
			Il2CppType declaringType = m_context.Il2Cpp.types[typeIndex];
			Il2CppTypeDefinition declaringTypeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(declaringType);
			string declaringTypeName = m_context.Executor.GetTypeDefName(declaringTypeDef, addNamespace: true, genericParameter: false);
			return declaringTypeName;
		}

		public void ProcessTypes()
		{
			List<TypeDescriptor> descriptors = new();
			while (m_typesToEmit.TryDequeue(out TypeDescriptor td))
			{
				ProcessTypeDescriptor(td);
				descriptors.Add(td);
			}
			foreach (TypeDescriptor td in descriptors)
			{
				td.GetTypeBuilder();
			}
		}

		private void ProcessTypeDescriptor(TypeDescriptor td)
		{
			TypeAttributes attribs = Helpers.GetTypeAttributes(td.TypeDef);

			// nested type
			if (td.TypeDef.declaringTypeIndex != -1)
			{
				td.DeclaringParent = GetTypeDescriptorOrThrow(td.TypeDef.declaringTypeIndex);
			}

			// base class
			if (!attribs.HasFlag(TypeAttributes.Interface) && td.TypeDef.parentIndex >= 0 && !td.TypeDef.IsValueType && !td.TypeDef.IsEnum)
			{
				string baseTypeName = GetTypeName(td.TypeDef.parentIndex);
				if (baseTypeName != "System.Object")
				{
					td.Base = GetTypeDescriptorOrThrow(td.TypeDef.parentIndex);
				}
			}

			// interfaces
			if (td.TypeDef.interfaces_count > 0)
			{
				for (int i = 0; i < td.TypeDef.interfaces_count; i++)
				{
					int typeIndex = m_context.Metadata.interfaceIndices[td.TypeDef.interfacesStart + i];
					td.Implements.Add(GetTypeDescriptorOrThrow(typeIndex));
				}
			}

			// generic parameters
			if (td.TypeDef.genericContainerIndex != -1)
			{
				Il2CppGenericContainer genericContainer = m_context.Metadata.genericContainers[td.TypeDef.genericContainerIndex];
				td.GenericParameterNames = m_context.Executor.GetGenericContainerParamNames(genericContainer);
			}
		}

		//public TypeDescriptor GetTypeDescriptor(Il2CppType typeInfo)
		//{
		//    string typeName = m_context.Executor.GetTypeName(typeInfo, true, false);

		//    if (m_typeCache.TryGetValue(typeName, out TypeDescriptor cachedType))
		//    {
		//        return cachedType;
		//    }
		//    TypeDescriptor td = CreateTypeDescriptor(typeName, typeInfo);
		//    return td;
		//}

		//private TypeDescriptor CreateTypeDescriptor(string typeName, Il2CppType typeInfo)
		//{
		//    TypeDescriptor td = new(typeName, typeInfo, m_buildTypeCallback);
		//    if (typeInfo.type != Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
		//    {
		//        m_typesToEmit.Add(td);
		//    }
		//    m_typeCache.Add(typeName, td);

		//    if (typeInfo.type == Il2CppTypeEnum.IL2CPP_TYPE_ARRAY || typeInfo.type == Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY)
		//    {
		//        Il2CppType elementType;
		//        if (typeInfo.type == Il2CppTypeEnum.IL2CPP_TYPE_ARRAY)
		//        {
		//            Il2CppArrayType arrayType = m_context.Il2Cpp.MapVATR<Il2CppArrayType>(typeInfo.data.array);
		//            elementType = m_context.Il2Cpp.GetIl2CppType(arrayType.etype);
		//            td.ArrayRank = arrayType.rank;
		//        }
		//        else
		//        {
		//            elementType = m_context.Il2Cpp.GetIl2CppType(typeInfo.data.type);
		//        }
		//        td.ElementType = GetTypeDescriptor(elementType);
		//        return td;
		//    }

		//    // NB: Will get generic type definition if this is a generic instance type
		//    Il2CppTypeDefinition typeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(typeInfo);
		//    TypeAttributes attribs = Helpers.GetTypeAttributes(typeDef);

		//    // base class?
		//    if (!attribs.HasFlag(TypeAttributes.Interface) && typeDef.parentIndex >= 0 && !typeDef.IsValueType && !typeDef.IsEnum)
		//    {
		//        Il2CppType parent = m_context.Il2Cpp.types[typeDef.parentIndex];
		//        string parentName = m_context.Executor.GetTypeName(parent, true, false);
		//        if (parentName != "System.Object")
		//        {
		//            td.Base = GetTypeDescriptor(parent);
		//        }
		//    }

		//    // nested type?
		//    if (typeDef.declaringTypeIndex != -1)
		//    {
		//        Il2CppType declaringType = m_context.Il2Cpp.types[typeDef.declaringTypeIndex];
		//        td.DeclaringParent = GetTypeDescriptor(declaringType);
		//    }

		//    // interfaces
		//    if (typeDef.interfaces_count > 0)
		//    {
		//        for (int i = 0; i < typeDef.interfaces_count; i++)
		//        {
		//            Il2CppType interfaceType = m_context.Il2Cpp.types[m_context.Metadata.interfaceIndices[typeDef.interfacesStart + i]];
		//            td.Implements.Add(GetTypeDescriptor(interfaceType));
		//        }
		//    }

		//    // generic
		//    if (typeInfo.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
		//    {
		//        Il2CppGenericClass genericClass = m_context.Il2Cpp.MapVATR<Il2CppGenericClass>(typeInfo.data.generic_class);
		//        Il2CppTypeDefinition genericTypeDef = m_context.Metadata.typeDefs[genericClass.typeDefinitionIndex];
		//        Il2CppType genericTypeInfo = m_context.Il2Cpp.types[genericTypeDef.byrefTypeIndex];
		//        //Il2CppGenericContainer genericContainer = m_context.Metadata.genericContainers[genericTypeDef.genericContainerIndex];
		//        //Il2CppType genericTypeInfo = m_context.Il2Cpp.types[genericContainer.ownerIndex];
		//        if (genericTypeInfo == typeInfo)
		//        {
		//            m_logger.LogError($"Invalid type: {typeName} (self-referencing-generic)");
		//            throw new ApplicationException($"Invalid type: {typeName} (self-referencing-generic)");
		//        }
		//        td.GenericParent = GetTypeDescriptor(genericTypeInfo);

		//        // Il2CppTypeDefinition genericTypeDef = m_context.Executor.GetGenericClassTypeDefinition(genericClass);
		//        Il2CppGenericInst genericInst = m_context.Il2Cpp.MapVATR<Il2CppGenericInst>(genericClass.context.class_inst);
		//        Il2CppType[] typeParams = m_context.Executor.GetGenericInstParamList(genericInst);
		//        td.GenericTypeParams = typeParams.Select(tp => GetTypeDescriptor(tp)).ToArray();
		//    }

		//    return td;
		//}

		private Type BuildType(TypeDescriptor descriptor)
		{
			ResolveTypeBuilderEventArgs args = new(descriptor);
			OnResolveType?.Invoke(this, args);
			return args.Result;
		}

		private Type BuildTypeReference(TypeReference reference)
		{
			ResolveTypeReferenceEventArgs args = new(reference);
			OnResolveTypeReference?.Invoke(this, args);
			return args.Result;
		}

		public interface ITypeReference
		{
			public Type Type { get; }
		}

		public class TypeReference : ITypeReference
		{
			public TypeReference(string name, Il2CppType cppType, Func<TypeReference, Type> buildTypeReference)
			{
				m_name = name;
				CppType = cppType;
				m_buildTypeReference = buildTypeReference;
			}

			public string Name => m_name;

			public Type Type
			{
				get { EnsureType(); return m_type; }
			}

			private void EnsureType()
			{
				if (m_hasType)
				{
					return;
				}
				m_type = m_buildTypeReference(this);
				m_hasType = true;
			}

			private bool m_hasType;
			private Type m_type;
			private string m_name;
			private Func<TypeReference, Type> m_buildTypeReference;
			public readonly Il2CppType CppType;
		}

		[DebuggerDisplay("{DebuggerDisplay,nq}")]
		public class TypeDescriptor : ITypeReference
		{
			public TypeDescriptor(string name, Il2CppTypeDefinition typeDef, Func<TypeDescriptor, Type> buildTypeDelegate)
			{
				m_name = name;
				TypeDef = typeDef;
				m_buildTypeDelegate = buildTypeDelegate;
			}

			public Type Type
			{
				get { EnsureType(); return m_type; }
			}

			public TypeBuilder GetTypeBuilder()
			{
				EnsureType();
				return m_type as TypeBuilder;
			}

			public Type GetGeneratedType()
			{
				EnsureType();
				return m_type;
			}

			private void EnsureType()
			{
				if (m_hasType)
				{
					return;
				}
				m_type = Type.GetType(Name) ?? m_buildTypeDelegate(this);
				m_hasType = true;
			}

			private string DebuggerDisplay => string.Join(" : ", Name, Base?.Name).TrimEnd(new char[] { ' ', ':' });
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

			public readonly string m_name;
			private Type m_type;
			private bool m_hasType;
			private readonly Func<TypeDescriptor, Type> m_buildTypeDelegate;
			public readonly Il2CppTypeDefinition TypeDef;
			public readonly List<TypeDescriptor> Implements = new();
			public TypeDescriptor DeclaringParent;
			public TypeDescriptor GenericParent;
			public TypeDescriptor ElementType;
			public int? ArrayRank;
			public TypeDescriptor[] GenericTypeParams;
			public TypeDescriptor Base;
			public string[] GenericParameterNames = Array.Empty<string>();
		}

		public class ResolveTypeBuilderEventArgs : EventArgs
		{
			public ResolveTypeBuilderEventArgs(TypeDescriptor request)
			{
				Request = request;
			}

			public TypeDescriptor Request { get; private set; }
			public Type Result { get; set; }
		}

		public class ResolveTypeReferenceEventArgs : EventArgs
		{
			public ResolveTypeReferenceEventArgs(TypeReference request)
			{
				Request = request;
			}

			public TypeReference Request { get; private set; }
			public Type Result { get; set; }
		}

		public event EventHandler<ResolveTypeBuilderEventArgs> OnResolveType;
		public event EventHandler<ResolveTypeReferenceEventArgs> OnResolveTypeReference;
		private readonly Func<TypeDescriptor, Type> m_buildTypeCallback;
		private readonly Func<TypeReference, Type> m_buildTypeReferenceCallback;
		private readonly AssemblyGeneratorContext m_context;
		private readonly ILogger m_logger;
		private readonly Dictionary<Il2CppTypeDefinition, TypeDescriptor> m_typeCache = new();
		private readonly Queue<TypeDescriptor> m_typesToEmit = new();
	}
}
