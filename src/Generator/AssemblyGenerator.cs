using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using Il2CppDumper;
using IL2CS.Core;
using IL2CS.Generator.TypeManagement;
using IL2CS.Runtime;
using IL2CS.Runtime.Types;
using Microsoft.Extensions.Logging;

namespace IL2CS.Generator
{
	public class AssemblyGenerator
	{
		private enum State
		{
			Initialized = 0,
			IndexDescriptors,
			GenerateTypes,
			ProcessTypes,
			GenerateAssembly
		}
		private State m_state = State.Initialized;

		public void Generate(string outputPath)
		{
			AppDomain currentDomain = Thread.GetDomain();
			ResolveEventHandler resolveHandler = new(ResolveEvent);
			currentDomain.TypeResolve += resolveHandler;

			Dictionary<State, Action> states = new()
			{
				{ State.IndexDescriptors, IndexTypeDescriptors },
				{ State.GenerateTypes, GenerateTypes },
				{ State.ProcessTypes, ProcessTypes },
				{ State.GenerateAssembly, () => GenerateAssembly(outputPath) }
			};

			foreach ((State state, Action action) in states)
			{
				m_state = state;
				action();
			}
		}

		private static readonly Stack<Type> s_resolutionStack = new();
		public Assembly ResolveEvent(object _, ResolveEventArgs args)
		{
			using IDisposable scope = m_logger.BeginScope($"Resolve({args.Name})");

			if (s_resolutionStack.Count > 0 && m_generatedTypeByFullName.TryGetValue($"{s_resolutionStack.Peek().FullName}+{args.Name}", out Type type))
			{
				ResolveType(type);
				return type.Assembly;
			}
			else if (m_generatedTypeByFullName.TryGetValue(args.Name, out type))
			{
				ResolveType(type);
				return type.Assembly;
			}
			else
			{
				if (m_generatedTypeByClassName.TryGetValue(args.Name, out List<Type> types))
				{
					types.ForEach(ResolveType);
				}
				else
				{
					m_logger.LogTrace($"Failed to load type {args.Name}");
				}
			}

			// Complete the type.
			return m_module.Assembly;
		}

		private void ResolveType(Type type)
		{
			s_resolutionStack.Push(type);
			if (type is TypeBuilder tb)
			{
				try
				{
					tb.CreateType();
				}
				catch (InvalidOperationException)
				{
					// This is needed to throw away InvalidOperationException.
					// Loader might send the TypeResolve event more than once
					// and the type might be complete already.
				}
				catch (Exception ex)
				{
					m_logger.LogTrace(ex.ToString());
				}
			}
			s_resolutionStack.Pop();
		}

		private void GenerateAssembly(string outputPath)
		{
			Lokad.ILPack.AssemblyGenerator generator = new();
			string outputFile = outputPath;
			if (System.IO.Path.GetExtension(outputFile) != ".dll")
			{
				outputFile = System.IO.Path.Join(outputPath, $"{m_asmName.Name}.dll");
			}
			generator.GenerateAssembly(m_asm, outputFile);
		}

		private void ProcessTypes()
		{
			foreach ((TypeDescriptor td, Type type) in m_generatedTypes)
			{
				if (type is TypeBuilder tb)
				{
					if (td.Base != null)
					{
						tb.SetParent(ResolveTypeReference(td.Base));
					}

					if (!td.TypeDef.IsEnum)
					{
						foreach (FieldDescriptor field in td.Fields)
						{
							if (field.Attributes.HasFlag(FieldAttributes.Static))
							{
								// TODO
								continue;
							}

							Type fieldType = ResolveTypeReference(field.Type);
							if (fieldType == null)
							{
								m_logger.LogWarning($"Dropping field '{field.Name}' from '{tb.Name}'. Reason: unknown type");
								continue;
							}

							byte indirection = 1;
							while (fieldType.IsPointer)
							{
								++indirection;
								fieldType = fieldType.GetElementType();
							}

							FieldBuilder fb = tb.DefineField(field.StorageName, fieldType, field.Attributes & ~(FieldAttributes.InitOnly | FieldAttributes.Public) | FieldAttributes.Private);
							if (field.DefaultValue != null)
							{
								fb.SetConstant(field.DefaultValue);
							}

							fb.SetCustomAttribute(new CustomAttributeBuilder(typeof(OffsetAttribute).GetConstructor(new Type[] { typeof(int) }), new object[] { field.Offset }));
							if (indirection > 1)
							{
								fb.SetCustomAttribute(new CustomAttributeBuilder(typeof(IndirectionAttribute).GetConstructor(new Type[] { typeof(byte) }), new object[] { indirection }));
							}

							MethodBuilder mb = tb.DefineMethod($"get_{field.Name}", MethodAttributes.Public, fieldType, Type.EmptyTypes);
							ILGenerator mbil = mb.GetILGenerator();
							mbil.Emit(OpCodes.Ldarg_0);
							mbil.Emit(OpCodes.Call, typeof(StructBase).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance));
							mbil.Emit(OpCodes.Ldarg_0);
							mbil.Emit(OpCodes.Ldfld, fb);
							mbil.Emit(OpCodes.Ret);

							PropertyBuilder pb = tb.DefineProperty(field.Name, PropertyAttributes.None, fieldType, null);
							pb.SetGetMethod(mb);
						}
					}
				}
			}
		}

		private void GenerateTypes()
		{
			m_state = State.GenerateTypes;
			List<TypeDescriptor> typesToBuild = FilterTypes();
			GenerateTypesRecursive(typesToBuild);
		}

		private List<TypeDescriptor> FilterTypes()
		{
			List<TypeDescriptor> typesToBuild = new();
			if (m_options.TypeSelectors != null)
			{
				typesToBuild.AddRange(m_typeDescriptors.Where(descriptor =>
				{
					foreach (Func<TypeDescriptor, bool> selector in m_options.TypeSelectors)
					{
						if (selector(descriptor))
						{
							return true;
						}
					}
					return false;
				}));
			}
			else
			{
				typesToBuild = new(m_typeDescriptors);
			}

			return typesToBuild;
		}

		private void GenerateTypesRecursive(IEnumerable<TypeDescriptor> descriptors)
		{
			Queue<TypeDescriptor> queue = new();
			HashSet<TypeDescriptor> queuedSet = new();
			Queue<TypeDescriptor> reopenList = new(descriptors);
			do
			{
				Queue<TypeDescriptor> openList = reopenList;
				reopenList = new();
				while (openList.TryDequeue(out TypeDescriptor td))
				{
					if (queuedSet.Contains(td))
					{
						throw new ApplicationException("Internal error");
					}
					if (td.DeclaringParent != null && !queuedSet.Contains(td.DeclaringParent))
					{
						reopenList.Enqueue(td);
						continue;
					}
					if (td.GenericParent != null && !queuedSet.Contains(td.GenericParent))
					{
						reopenList.Enqueue(td);
						continue;
					}
					queue.Enqueue(td);
					queuedSet.Add(td);
				}
			}
			while (reopenList.Count > 0);

			while (queue.TryDequeue(out TypeDescriptor td))
			{
				EnsureType(td);
			}
		}

		private Type EnsureType(TypeDescriptor descriptor)
		{
			if (descriptor == null)
			{
				return null;
			}
			if (m_generatedTypes.TryGetValue(descriptor, out Type value))
			{
				return value;
			}
			return BuildType(descriptor);
		}

		private Type BuildType(TypeDescriptor descriptor)
		{
			if (m_state != State.GenerateTypes)
			{
				throw new ApplicationException("Invalid state");
			}

			if (Types.TryGetType(descriptor.Name, out Type type))
			{
				RegisterType(descriptor, type);
				return type;
			}

			if (descriptor.DeclaringParent != null)
			{
				TypeBuilder parentBuilder = EnsureType(descriptor.DeclaringParent) as TypeBuilder;
				if (parentBuilder == null)
				{
					throw new ApplicationException("Internal error: Parent is not of type TypeBuilder");
				}
				if (descriptor.TypeDef.IsEnum)
				{
					TypeBuilder eb = parentBuilder.DefineNestedType(descriptor.Name, descriptor.Attributes, typeof(Enum), null);
					m_generatedTypes.Add(descriptor, eb);
					foreach (FieldDescriptor field in descriptor.Fields)
					{
						FieldBuilder fb = eb.DefineField(field.StorageName, ResolveTypeReference(field.Type), field.Attributes);
						if (field.DefaultValue != null)
						{
							fb.SetConstant(field.DefaultValue);
						}
					}
					type = eb;
				}
				else
				{
					type = parentBuilder.DefineNestedType(descriptor.Name, descriptor.Attributes, descriptor.Base?.Type);
					m_generatedTypes.Add(descriptor, type);
				}
			}
			else
			{
				if (descriptor.TypeDef.IsEnum)
				{
					// NB: first field is always 'value__'
					TypeBuilder eb = m_module.DefineType(descriptor.Name, descriptor.Attributes, typeof(Enum), null);
					m_generatedTypes.Add(descriptor, eb);
					foreach (FieldDescriptor field in descriptor.Fields)
					{
						FieldBuilder fb = eb.DefineField(field.Name, ResolveTypeReference(field.Type), field.Attributes);
						if (field.DefaultValue != null)
						{
							fb.SetConstant(field.DefaultValue);
						}
					}
					type = eb;
				}
				else
				{
					type = m_module.DefineType(descriptor.Name, descriptor.Attributes, descriptor.Base?.Type);
					m_generatedTypes.Add(descriptor, type);
				}
			}
			if (!m_generatedTypes.ContainsKey(descriptor))
			{
				throw new ApplicationException("Internal error: type was not added to m_generatedTypes");
			}
			m_generatedTypeByFullName.Add(type.FullName, type);
			if (!m_generatedTypeByClassName.ContainsKey(type.Name))
			{
				m_generatedTypeByClassName.Add(type.Name, new List<Type>());
			}
			m_generatedTypeByClassName[type.Name].Add(type);
			if (type is TypeBuilder tb)
			{
				if (descriptor.GenericParameterNames.Length > 0)
				{
					tb.DefineGenericParameters(descriptor.GenericParameterNames);
				}
				if (!descriptor.TypeDef.IsEnum && !descriptor.Attributes.HasFlag(TypeAttributes.Interface))
				{
					tb.DefineDefaultConstructor(MethodAttributes.Public);
				}
			}

			// visit members, don't create them.
			if (!descriptor.TypeDef.IsEnum)
			{
				ResolveTypeReference(descriptor.Base);
				descriptor.Fields.ForEach(field => ResolveTypeReference(field.Type));
				EnsureType(descriptor.GenericParent);
				descriptor.Implements.ForEach(iface => ResolveTypeReference(iface));
			}

			return type;
		}

		private void RegisterType(TypeDescriptor descriptor, Type type)
		{
			m_generatedTypes.Add(descriptor, type);
			if (type != null && descriptor.Name != type.Name && !type.IsAssignableTo(typeof(StructBase)))
			{
				Debugger.Break();
			}
			m_generatedTypeByFullName.Add(descriptor.Name, type);
			if (!m_generatedTypeByClassName.ContainsKey(descriptor.Name))
			{
				m_generatedTypeByClassName.Add(descriptor.Name, new List<Type>());
			}
			m_generatedTypeByClassName[descriptor.Name].Add(type);
		}

		private void IndexTypeDescriptors()
		{
			// already indexed?
			if (m_typeDescriptors.Count > 0)
			{
				return;
			}

			// Declare all types before associating dependencies
			for (int typeIndex = 0; typeIndex < m_context.Metadata.typeDefs.Length; ++typeIndex)
			{
				Il2CppTypeDefinition typeDef = m_context.Metadata.typeDefs[typeIndex];
				m_typeDescriptors.Add(MakeTypeDescriptor(typeDef, typeIndex));
			}

			// Build dependencies
			foreach (TypeDescriptor td in m_typeDescriptors)
			{
				TypeAttributes attribs = Helpers.GetTypeAttributes(td.TypeDef);
				td.Attributes = attribs;

				// nested within type (parent)
				if (td.TypeDef.declaringTypeIndex != -1)
				{
					Helpers.Assert((attribs & TypeAttributes.VisibilityMask) > TypeAttributes.Public, "Nested attribute missing");
					Il2CppType cppType = m_context.Il2Cpp.types[td.TypeDef.declaringTypeIndex];
					td.DeclaringParent = m_typeCache[(int)cppType.data.klassIndex];
				}
				else
				{
					Helpers.Assert((attribs & TypeAttributes.VisibilityMask) <= TypeAttributes.Public, "Unexpected nested attribute");
				}

				// nested types (children)
				if (td.TypeDef.nested_type_count > 0)
				{
					foreach (int typeIndex in EnumerateMetadata(td.TypeDef.nestedTypesStart, td.TypeDef.nested_type_count, m_context.Metadata.nestedTypeIndices))
					{
						td.NestedTypes.Add(m_typeCache[typeIndex]);
					}
				}

				// generic parameters
				if (td.TypeDef.genericContainerIndex != -1)
				{
					Il2CppGenericContainer genericContainer = m_context.Metadata.genericContainers[td.TypeDef.genericContainerIndex];
					td.GenericParameterNames = m_context.Executor.GetGenericContainerParamNames(genericContainer);
					Helpers.Assert(td.GenericParameterNames.Length > 0, "Generic class must have template arguments");
				}

				// base class
				if (attribs.HasFlag(TypeAttributes.Interface))
				{
					td.Base = null;
				}
				else if (td.TypeDef.IsEnum)
				{
					td.Base = new TypeReference(typeof(Enum));
				}
				else if (td.TypeDef.parentIndex >= 0 && !td.TypeDef.IsValueType && !td.TypeDef.IsEnum)
				{
					TypeReference baseTypeReference = MakeTypeReferenceFromCppTypeIndex(td.TypeDef.parentIndex, td);
					if (baseTypeReference.Name != "System.Object")
					{
						if (Types.TryGetType(baseTypeReference.Name, out Type builtInType))
						{
							td.Base = new TypeReference(builtInType);
						}
						else
						{
							td.Base = baseTypeReference;
						}
					}
				}

				// default to ValueType or StructBase
				if (td.Base == null && !attribs.HasFlag(TypeAttributes.Interface))
				{
					td.Base = new TypeReference(td.TypeDef.IsValueType ? typeof(ValueType) : typeof(StructBase));
				}

				// interfaces
				foreach (int interfaceTypeIndex in EnumerateMetadata(td.TypeDef.interfacesStart, td.TypeDef.interfaces_count, m_context.Metadata.interfaceIndices))
				{
					td.Implements.Add(MakeTypeReferenceFromCppTypeIndex(interfaceTypeIndex, td));
				}

				// fields
				if (td.TypeDef.field_count > 0)
				{
					foreach (int fieldIndex in Enumerable.Range(td.TypeDef.fieldStart, td.TypeDef.field_count))
					{
						Il2CppFieldDefinition fieldDef = m_context.Metadata.fieldDefs[fieldIndex];
						Il2CppType fieldCppType = m_context.Il2Cpp.types[fieldDef.typeIndex];
						TypeReference fieldType = MakeTypeReferenceFromCppTypeIndex(fieldDef.typeIndex, td);
						string fieldName = m_context.Metadata.GetStringFromIndex(fieldDef.nameIndex);
						FieldAttributes attrs = (FieldAttributes)fieldCppType.attrs & ~FieldAttributes.InitOnly;
						int offset = m_context.Il2Cpp.GetFieldOffsetFromIndex(td.TypeIndex, fieldIndex - td.TypeDef.fieldStart, fieldIndex, td.TypeDef.IsValueType, attrs.HasFlag(FieldAttributes.Static));
						FieldDescriptor fieldDescriptor = new(fieldName, fieldType, attrs, offset);
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
		}

		private TypeReference MakeTypeReferenceFromCppTypeIndex(int typeIndex, TypeDescriptor descriptor)
		{
			Il2CppType cppType = m_context.Il2Cpp.types[typeIndex];
			string baseTypeName = m_context.Executor.GetTypeName(cppType, addNamespace: true, is_nested: false);
			return new(baseTypeName, cppType, descriptor);
		}

		private TypeDescriptor MakeTypeDescriptor(Il2CppTypeDefinition typeDef, int typeIndex)
		{
			string typeName = m_context.Metadata.GetStringFromIndex(typeDef.nameIndex);
			int index = typeName.IndexOf("`");
			if (index != -1)
			{
				typeName = typeName.Substring(0, index);
			}
			string ns = m_context.Metadata.GetStringFromIndex(typeDef.namespaceIndex);
			if (ns != "")
			{
				typeName = ns + "." + typeName;
			}
			TypeDescriptor td = new(typeName, typeDef, typeIndex);
			m_typeCache.Add(typeIndex, td);
			return td;
		}

		private Type ResolveTypeReference(TypeReference reference)
		{
			if (reference == null)
			{
				return null;
			}
			if (reference.Type != null)
			{
				return reference.Type;
			}

			Il2CppType il2CppType = reference.CppType;
			TypeDescriptor typeContext = reference.TypeContext;
			return ResolveTypeReference(il2CppType, typeContext);
		}

		private Type ResolveTypeReference(Il2CppType il2CppType, TypeDescriptor typeContext)
		{
			string typeName = m_context.Executor.GetTypeName(il2CppType, true, false);
			switch (il2CppType.type)
			{
				case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
					{
						Il2CppArrayType arrayType = m_context.Il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
						Il2CppType elementCppType = m_context.Il2Cpp.GetIl2CppType(arrayType.etype);
						Type elementType = ResolveTypeReference(elementCppType, typeContext);
						return elementType?.MakeArrayType(arrayType.rank);
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
					{
						Il2CppType elementCppType = m_context.Il2Cpp.GetIl2CppType(il2CppType.data.type);
						Type elementType = ResolveTypeReference(elementCppType, typeContext);
						return elementType?.MakeArrayType();
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
					{
						Il2CppType oriType = m_context.Il2Cpp.GetIl2CppType(il2CppType.data.type);
						Type ptrToType = ResolveTypeReference(oriType, typeContext);
						return ptrToType?.MakePointerType();
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
				case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
					{
						// TODO: Is this even remotely correct? :S
						Il2CppGenericParameter param = m_context.Executor.GetGenericParameteFromIl2CppType(il2CppType);
						Type type = m_generatedTypes[typeContext];
						return (type as TypeInfo)?.GenericTypeParameters[param.num];
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
				case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
					{
						Il2CppTypeDefinition typeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(il2CppType);
						int typeDefIndex = Array.IndexOf(m_context.Metadata.typeDefs, typeDef);
						return EnsureType(m_typeCache[typeDefIndex]);
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
							Type ptype = ResolveTypeReference(paramCppType, typeContext);
							if (ptype == null)
							{
								m_logger.LogWarning($"Dropping '{typeName}'. Reason: incomplete generic type");
								return null;
							}
							genericParameterTypes.Add(ptype);
						}

						int typeDefIndex = Array.IndexOf(m_context.Metadata.typeDefs, genericTypeDef);
						return EnsureType(m_typeCache[typeDefIndex])?.MakeGenericType(genericParameterTypes.ToArray());
					}
				default:
					return TypeMap[(int)il2CppType.type];
			}
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

		private bool TryGetDefaultValue(int typeIndex, int dataIndex, out object value)
		{
			uint pointer = m_context.Metadata.GetDefaultValueFromIndex(dataIndex);
			Il2CppType defaultValueType = m_context.Il2Cpp.types[typeIndex];
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
					int len = m_context.Metadata.ReadInt32();
					value = m_context.Metadata.ReadString(len);
					return true;
				default:
					value = pointer;
					return false;
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

		public AssemblyGenerator(AssemblyGeneratorOptions options)
		{
			m_options = options;
			m_context = new AssemblyGeneratorContext(options);
			m_asmName = new AssemblyName(options.AssembyName);
			m_asm = AssemblyBuilder.DefineDynamicAssembly(m_asmName, AssemblyBuilderAccess.RunAndCollect);
			m_module = m_asm.DefineDynamicModule(m_asmName.Name);
			m_logger = options.LogFactory.CreateLogger("generator");
		}

		private readonly AssemblyGeneratorOptions m_options;
		private readonly AssemblyName m_asmName;
		private readonly AssemblyBuilder m_asm;
		private readonly ModuleBuilder m_module;
		private readonly AssemblyGeneratorContext m_context;
		private readonly Dictionary<int, TypeDescriptor> m_typeCache = new();
		private readonly List<TypeDescriptor> m_typeDescriptors = new();
		private readonly Dictionary<TypeDescriptor, Type> m_generatedTypes = new();
		private readonly Dictionary<string, Type> m_generatedTypeByFullName = new();
		private readonly Dictionary<string, List<Type>> m_generatedTypeByClassName = new();
		private readonly ILogger m_logger;
	}
}
