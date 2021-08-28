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
			ProcessTypes();
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

		private Type GetTypeReference(int il2CppTypeIndex, TypeDescriptor typeContext)
		{
			Il2CppType il2CppType = m_context.Il2Cpp.types[il2CppTypeIndex];
			return GetTypeReference(il2CppType, typeContext);
		}

		private Type GetTypeReference(Il2CppType il2CppType, TypeDescriptor typeContext)
		{
			string name = m_context.Executor.GetTypeName(il2CppType, true, false);

			switch (il2CppType.type)
			{
				case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
					{
						Il2CppArrayType arrayType = m_context.Il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
						Il2CppType elementCppType = m_context.Il2Cpp.GetIl2CppType(arrayType.etype);
						Type elementType = GetTypeReference(elementCppType, typeContext);
						return elementType.MakeArrayType(arrayType.rank);
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
					{
						Il2CppType elementCppType = m_context.Il2Cpp.GetIl2CppType(il2CppType.data.type);
						Type elementType = GetTypeReference(elementCppType, typeContext);
						return elementType.MakeArrayType();
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
					{
						Il2CppType oriType = m_context.Il2Cpp.GetIl2CppType(il2CppType.data.type);
						Type ptrToType = GetTypeReference(oriType, typeContext);
						return ptrToType.MakePointerType();
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
				case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
					{
						// TODO: Is this even remotely correct? :S
						Il2CppGenericParameter param = m_context.Executor.GetGenericParameteFromIl2CppType(il2CppType);
						return (typeContext.Type as TypeInfo).GenericTypeParameters[param.num];
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
				case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
					{
						Il2CppTypeDefinition typeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(il2CppType);
						return GetTypeDescriptor(typeDef).Type;
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
					{
						Il2CppGenericClass genericClass = m_context.Il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
						Il2CppTypeDefinition genericTypeDef = m_context.Executor.GetGenericClassTypeDefinition(genericClass);
						Il2CppGenericInst genericInst = m_context.Il2Cpp.MapVATR<Il2CppGenericInst>(genericClass.context.class_inst);
						List<Type> genericParameterTypes = new();
						ulong[] pointers = m_context.Il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
						for (int i = 0; i < genericInst.type_argc; i++)
						{
							Il2CppType paramCppType = m_context.Il2Cpp.GetIl2CppType(pointers[i]);
							genericParameterTypes.Add(GetTypeReference(paramCppType, typeContext));
						}
						TypeDescriptor genericDescriptor = GetTypeDescriptor(genericTypeDef);
						return genericDescriptor.Type.MakeGenericType(genericParameterTypes.ToArray());
					}
				default:
					return TypeMap[(int)il2CppType.type];
			}
		}

		private TypeReference MakeTypeReference(int typeIndex)
		{
			Il2CppType il2CppType = m_context.Il2Cpp.types[typeIndex];
			return MakeTypeReference(il2CppType);
		}

		private TypeReference MakeTypeReference(Il2CppType il2CppType)
		{
			string name = m_context.Executor.GetTypeName(il2CppType, true, false);
			TypeReference typeRef = new(name, il2CppType, m_buildTypeReferenceCallback);

			switch (il2CppType.type)
			{
				case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
					{
						Il2CppArrayType arrayType = m_context.Il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
						Il2CppType elementType = m_context.Il2Cpp.GetIl2CppType(arrayType.etype);
						typeRef.ElementType = MakeTypeReference(elementType);
						typeRef.ArrayRank = arrayType.rank;
						return typeRef;
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
					{
						Il2CppType elementType = m_context.Il2Cpp.GetIl2CppType(il2CppType.data.type);
						typeRef.ElementType = MakeTypeReference(elementType);
						return typeRef;
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
					{
						Il2CppType oriType = m_context.Il2Cpp.GetIl2CppType(il2CppType.data.type);
						typeRef.PointerOf = MakeTypeReference(oriType);
						return typeRef;
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
				case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
					{
						Il2CppGenericParameter param = m_context.Executor.GetGenericParameteFromIl2CppType(il2CppType);
						typeRef.TypeArgumentSlot = param.num;
						return typeRef;
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
				case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
				case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
				// TODO
				default:
					typeRef.PrimitiveType = TypeMap[(int)il2CppType.type];
					return typeRef;
			}
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

			// generic parameters
			if (td.TypeDef.genericContainerIndex != -1)
			{
				Il2CppGenericContainer genericContainer = m_context.Metadata.genericContainers[td.TypeDef.genericContainerIndex];
				td.GenericParameterNames = m_context.Executor.GetGenericContainerParamNames(genericContainer);
			}

			// base class
			if (!attribs.HasFlag(TypeAttributes.Interface) && td.TypeDef.parentIndex >= 0 && !td.TypeDef.IsValueType && !td.TypeDef.IsEnum)
			{
				string baseTypeName = GetTypeName(td.TypeDef.parentIndex);
				if (baseTypeName != "System.Object")
				{
					td.Base = GetTypeReference(td.TypeDef.parentIndex, td);
				}
			}

			// interfaces
			if (td.TypeDef.interfaces_count > 0)
			{
				for (int i = 0; i < td.TypeDef.interfaces_count; i++)
				{
					int typeIndex = m_context.Metadata.interfaceIndices[td.TypeDef.interfacesStart + i];
					td.Implements.Add(GetTypeReference(typeIndex, td));
				}
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

			public TypeReference ElementType;
			public int? ArrayRank;
			public TypeReference PointerOf;
			public ushort? TypeArgumentSlot;
			public Type PrimitiveType;
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
			public readonly List<Type> Implements = new();
			public TypeDescriptor DeclaringParent;
			public TypeDescriptor GenericParent;
			public Type[] GenericTypeParams;
			public Type Base;
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

		private static readonly Dictionary<int, Type> TypeMap = new Dictionary<int, Type>
		{
			{1,typeof(void)},
			{2,typeof(bool)},
			{3,typeof(char)},
			{4,typeof(sbyte)},
			{5,typeof(byte)},
			{6,typeof(short)},
			{7,typeof(ushort)},
			{8,typeof(int)},
			{9,typeof(uint)},
			{10,typeof(long)},
			{11,typeof(ulong)},
			{12,typeof(float)},
			{13,typeof(double)},
			{14,typeof(string)},
			{22,typeof(IntPtr)},
			{24,typeof(IntPtr)},
			{25,typeof(UIntPtr)},
			{28,typeof(object)},
		};
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
