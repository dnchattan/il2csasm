using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Il2CppDumper;
using IL2CS.Runtime;
using Microsoft.Extensions.Logging;

namespace IL2CS.Generator
{
	public class TypeCollector
	{
		public TypeCollector(AssemblyGeneratorContext context)
		{
			m_context = context;
			m_logger = context.Options.LogFactory.CreateLogger("type-collector");
			m_buildTypeCallback = BuildType;
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
						return genericDescriptor.Type?.MakeGenericType(genericParameterTypes.ToArray());
					}
				default:
					return TypeMap[(int)il2CppType.type];
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
				td.EnsureType();
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
			if (attribs.HasFlag(TypeAttributes.Interface))
			{
				td.Base = null;
			}
			else if (td.TypeDef.IsEnum)
			{
				td.Base= typeof(Enum);
			}
			else if (td.TypeDef.parentIndex >= 0 && !td.TypeDef.IsValueType && !td.TypeDef.IsEnum)
			{
				string baseTypeName = GetTypeName(td.TypeDef.parentIndex);
				if (baseTypeName != "System.Object")
				{
					td.Base = GetTypeReference(td.TypeDef.parentIndex, td);
				}
			}
			else
			{
				td.Base = typeof(StructBase);
			}

			// interfaces
			foreach (int interfaceTypeIndex in EnumerateMetadata(td.TypeDef.interfacesStart, td.TypeDef.interfaces_count, m_context.Metadata.interfaceIndices))
			{
				td.Implements.Add(GetTypeReference(interfaceTypeIndex, td));
			}

			// fields
			if (td.TypeDef.field_count > 0)
			{
				foreach (int fieldIndex in Enumerable.Range(td.TypeDef.fieldStart, td.TypeDef.field_count))
				{
					Il2CppFieldDefinition fieldDef = m_context.Metadata.fieldDefs[fieldIndex];
					Il2CppType fieldCppType = m_context.Il2Cpp.types[fieldDef.typeIndex];
					Type fieldType = GetTypeReference(fieldCppType, td);
					string fieldName = m_context.Metadata.GetStringFromIndex(fieldDef.nameIndex);
					FieldAttributes attrs = (FieldAttributes)fieldCppType.attrs & ~FieldAttributes.InitOnly;
					FieldDescriptor fieldDescriptor = new(fieldName, fieldType, attrs);
					if (m_context.Metadata.GetFieldDefaultValueFromIndex(fieldIndex, out Il2CppFieldDefaultValue fieldDefaultValue) && fieldDefaultValue.dataIndex != -1)
					{
						if (TryGetDefaultValue(fieldDefaultValue.typeIndex, fieldDefaultValue.dataIndex, out object value))
						{
							fieldDescriptor.DefaultValue = value;
						}
					}
					td.Fields.Add(fieldDescriptor);
				}
			}
		}

		private static IEnumerable<T> EnumerateMetadata<T>(int start, int count, T[] definitionArray)
		{
			if (count <= 0 || start < 0)
			{
				return Array.Empty<T>();
			}
			return definitionArray.AsSpan().Slice(start, count).ToArray();
		}

		private bool TryGetDefaultValue(int typeIndex, int dataIndex, out object value)
		{
			var pointer = m_context.Metadata.GetDefaultValueFromIndex(dataIndex);
			var defaultValueType = m_context.Il2Cpp.types[typeIndex];
			m_context.Metadata.Position = pointer;
			switch (defaultValueType.type)
			{
				case Il2CppTypeEnum.IL2CPP_TYPE_BOOLEAN:
					value = m_context.Metadata.ReadBoolean();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_U1:
					value = m_context.Metadata.ReadByte();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_I1:
					value = m_context.Metadata.ReadSByte();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_CHAR:
					value = BitConverter.ToChar(m_context.Metadata.ReadBytes(2), 0);
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_U2:
					value = m_context.Metadata.ReadUInt16();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_I2:
					value = m_context.Metadata.ReadInt16();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_U4:
					value = m_context.Metadata.ReadUInt32();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_I4:
					value = m_context.Metadata.ReadInt32();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_U8:
					value = m_context.Metadata.ReadUInt64();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_I8:
					value = m_context.Metadata.ReadInt64();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_R4:
					value = m_context.Metadata.ReadSingle();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_R8:
					value = m_context.Metadata.ReadDouble();
					return true;
				case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
					var len = m_context.Metadata.ReadInt32();
					value = m_context.Metadata.ReadString(len);
					return true;
				default:
					value = pointer;
					return false;
			}
		}

		[DebuggerStepThrough]
		private Type BuildType(TypeDescriptor descriptor)
		{
			ResolveTypeBuilderEventArgs args = new(descriptor);
			OnResolveType?.Invoke(this, args);
			return args.Result;
		}

		public interface ITypeReference
		{
			public Type Type { get; }
		}

		public class FieldDescriptor
		{
			private static readonly Regex BackingFieldRegex = new("<(.+)>k__BackingField", RegexOptions.Compiled);

			public FieldDescriptor(string name, Type type, FieldAttributes attrs)
			{
				Name = BackingFieldRegex.Replace(name, match => match.Groups[1].Value);
				// this is kinda evil, but it will make them consistent in name at least =)
				StorageName = $"_{Name}_BackingField";
				Type = type;
				Attributes = attrs;
			}

			public readonly string StorageName;
			public readonly string Name;
			public readonly Type Type;
			public FieldAttributes Attributes;
			public object DefaultValue;
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

			public Type GetGeneratedType()
			{
				EnsureType();
				return m_type;
			}

			internal void EnsureType()
			{
				if (m_hasType)
				{
					return;
				}
				// TODO: I think we can stop testing for system types here since this is for typedefs only
				//m_type = Type.GetType(Name) ?? m_buildTypeDelegate(this);
				m_type = m_buildTypeDelegate(this);
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
			public readonly List<FieldDescriptor> Fields = new();
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
		private readonly Func<TypeDescriptor, Type> m_buildTypeCallback;
		private readonly AssemblyGeneratorContext m_context;
		private readonly ILogger m_logger;
		private readonly Dictionary<Il2CppTypeDefinition, TypeDescriptor> m_typeCache = new();
		private readonly Queue<TypeDescriptor> m_typesToEmit = new();
	}
}
