using System;
using System.Collections.Generic;
using System.Linq;
using IL2CS.Generator;
using static Il2CppDumper.Il2CppConstants;

namespace Il2CppDumper
{
	public class UniqueTypeDefNameMap
	{
		private readonly HashSet<string> _uniqueNames = new HashSet<string>();
		private readonly Dictionary<Il2CppType, string> _typeDefToName = new Dictionary<Il2CppType, string>();
		private readonly WeakReference<Il2CppExecutor2> _executor;
		public UniqueTypeDefNameMap(WeakReference<Il2CppExecutor2> executor)
		{
			_executor = executor;
		}

		public string GetName(Il2CppTypeDefinition typeDef, string originalName)
		{
			if (!_executor.TryGetTarget(out Il2CppExecutor2 executor))
			{
				throw new InvalidOperationException("Cannot be used with disposed executor!");
			}
			Il2CppType il2CppType = executor.GetIl2CppTypeFromTypeDefinition(typeDef);

			return GetName(executor, typeDef, il2CppType, originalName);
		}

		public string GetName(Il2CppType il2CppType, string originalName)
		{
			if (_typeDefToName.ContainsKey(il2CppType))
			{
				return _typeDefToName[il2CppType].Split('|')[1];
			}

			if (!_executor.TryGetTarget(out Il2CppExecutor2 executor))
			{
				throw new InvalidOperationException("Cannot be used with disposed executor!");
			}
			Il2CppTypeDefinition typeDef = executor.GetTypeDefinitionFromIl2CppType(il2CppType);

			return GetName(executor, typeDef, il2CppType, originalName);
		}

		private string GetName(Il2CppExecutor2 executor, Il2CppTypeDefinition typeDef, Il2CppType il2CppType, string originalName)
		{
			if (_typeDefToName.ContainsKey(il2CppType))
			{
				return _typeDefToName[il2CppType].Split('|')[1];
			}

			string uniqueTypeNamePair = $"{typeDef.namespaceIndex}|{originalName}";
			int n = 0;
			while (_uniqueNames.Contains(uniqueTypeNamePair))
			{
				uniqueTypeNamePair = $"{typeDef.namespaceIndex}|_{++n}_{originalName}";
			}
			_uniqueNames.Add(uniqueTypeNamePair);
			_typeDefToName[il2CppType] = uniqueTypeNamePair;
			return uniqueTypeNamePair.Split('|')[1];
		}
	}
	public class Il2CppExecutor2
	{
		public Metadata metadata;
		public Il2Cpp il2Cpp;
		private static readonly Dictionary<int, string> TypeString = new Dictionary<int, string>
		{
			{1,typeof(void).FullName},
			{2,typeof(bool).FullName},
			{3,typeof(char).FullName},
			{4,typeof(sbyte).FullName},
			{5,typeof(byte).FullName},
			{6,typeof(short).FullName},
			{7,typeof(ushort).FullName},
			{8,typeof(int).FullName},
			{9,typeof(uint).FullName},
			{10,typeof(long).FullName},
			{11,typeof(ulong).FullName},
			{12,typeof(float).FullName},
			{13,typeof(double).FullName},
			{14,typeof(string).FullName},
			{22,typeof(IntPtr).FullName},
			{24,typeof(IntPtr).FullName},
			{25,typeof(UIntPtr).FullName},
			{28,typeof(object).FullName},
		};
		public ulong[] customAttributeGenerators;
		private readonly Dictionary<Il2CppTypeDefinition, int> TypeDefToIndex = new Dictionary<Il2CppTypeDefinition, int>();
		private readonly Dictionary<Il2CppType, long> TypeToIndex = new Dictionary<Il2CppType, long>();
		private readonly UniqueTypeDefNameMap TypeDefToName;

		public Il2CppExecutor2(Metadata metadata, Il2Cpp il2Cpp)
		{
			this.metadata = metadata;
			this.il2Cpp = il2Cpp;
			TypeDefToName = new UniqueTypeDefNameMap(new WeakReference<Il2CppExecutor2>(this));

			if (il2Cpp.Version >= 27)
			{
				customAttributeGenerators = new ulong[metadata.imageDefs.Sum(x => x.customAttributeCount)];
				foreach (Il2CppImageDefinition imageDef in metadata.imageDefs)
				{
					string imageDefName = metadata.GetStringFromIndex(imageDef.nameIndex);
					Il2CppCodeGenModule codeGenModule = il2Cpp.codeGenModules[imageDefName];
					ulong[] pointers = il2Cpp.ReadClassArray<ulong>(il2Cpp.MapVATR(codeGenModule.customAttributeCacheGenerator), imageDef.customAttributeCount);
					pointers.CopyTo(customAttributeGenerators, imageDef.customAttributeStart);
				}
			}
			else
			{
				customAttributeGenerators = il2Cpp.customAttributeGenerators;
			}

			for (int index = 0; index < metadata.typeDefs.Length; ++index)
			{
				TypeDefToIndex[metadata.typeDefs[index]] = index;
			}
			for (long index = 0; index < il2Cpp.types.Length; ++index)
			{
				TypeToIndex[il2Cpp.types[index]] = index;
			}
		}

		/**/
		public Il2CppTypeInfo GetTypeInfo(Il2CppType il2CppType, bool is_nested = false)
		{
			Il2CppTypeInfo result = GetTypeInfoInternal(il2CppType, is_nested);
			return result;
		}

		public Il2CppTypeInfo GetTypeInfo(Il2CppTypeDefinition typeDef, Il2CppGenericClass genericClass = null, bool is_nested = false)
		{
			Il2CppType il2CppType = GetIl2CppTypeFromTypeDefinition(typeDef);
			Il2CppTypeInfo result = GetTypeInfoInternal(typeDef, il2CppType, genericClass, is_nested);
			return result;
		}


		public Il2CppTypeInfo GetTypeInfoInternal(Il2CppTypeDefinition typeDef, Il2CppType il2CppType, Il2CppGenericClass genericClass = null, bool is_nested = false)
		{
			Il2CppTypeInfo typeInfo = new Il2CppTypeInfo(il2CppType)
			{
				TypeIndex = TypeToIndex[il2CppType]
			};

			if (typeDef.parentIndex != -1 && !is_nested && !typeDef.IsValueType && !typeDef.IsEnum)
			{
				Il2CppTypeInfo baseType = GetTypeInfo(il2Cpp.types[typeDef.parentIndex], is_nested);
				if (!baseType.IsPrimitive && baseType.TypeIndex != typeInfo.TypeIndex)
				{
					typeInfo.BaseType = baseType;
				}
			}
			if (typeDef.declaringTypeIndex != -1)
			{
				typeInfo.DeclaringType = GetTypeInfo(il2Cpp.types[typeDef.declaringTypeIndex], true);
			}
			else
			{
				typeInfo.Namespace = metadata.GetStringFromIndex(typeDef.namespaceIndex);
			}

			// trim MyGenericType`1 to just MyGenericType
			string typeName = metadata.GetStringFromIndex(typeDef.nameIndex);
			int index = typeName.IndexOf("`");
			if (index != -1)
			{
				typeName = typeName.Substring(0, index);
			}
			typeInfo.TypeName = TypeDefToName.GetName(typeDef, typeName);

			// only recurse if we're using templates!
			if (is_nested && genericClass == null)
			{
				return typeInfo;
			}

			if (typeDef.genericContainerIndex >= 0)
			{
				Il2CppGenericContainer genericContainer = metadata.genericContainers[typeDef.genericContainerIndex];
				string[] paramNames = GetGenericContainerParamNames(genericContainer);
				typeInfo.TemplateArgumentNames.AddRange(paramNames);
			}
			if (genericClass != null)
			{
				Il2CppGenericInst genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(genericClass.context.class_inst);
				Il2CppType[] args = GetGenericInstParamList(genericInst);
				foreach (Il2CppType arg in args)
				{
					// generic argument
					if (arg.type == Il2CppTypeEnum.IL2CPP_TYPE_VAR || arg.type == Il2CppTypeEnum.IL2CPP_TYPE_MVAR)
					{
						// Are these used??
						Il2CppGenericParameter param = GetGenericParameteFromIl2CppType(arg);
						string tName = metadata.GetStringFromIndex(param.nameIndex);
						typeInfo.TemplateArgumentNames.Add(tName);
					}
					else
					{
						Il2CppTypeInfo tArg = GetTypeInfoInternal(arg);
						typeInfo.TypeArguments.Add(tArg);
					}
				}
			}
			return typeInfo;
		}


		public Il2CppTypeInfo GetTypeInfoInternal(Il2CppType il2CppType, bool is_nested = false)
		{
			Il2CppTypeInfo typeInfo = new Il2CppTypeInfo(il2CppType)
			{
				TypeIndex = TypeToIndex[il2CppType]
			};
			switch (il2CppType.type)
			{
				case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
					{
						Il2CppArrayType arrayType = il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
						Il2CppType elementType = il2Cpp.GetIl2CppType(arrayType.etype);
						Il2CppTypeInfo elementTypeInfo = GetTypeInfo(elementType, is_nested);

						// hack: too lazy to special case consumption of array types
						elementTypeInfo.IsArray = true;
						return elementTypeInfo;
						// typeInfo.ElementType = GetTypeInfo(elementType);
						// return typeInfo;
						// return $"{GetTypeName(elementType, addNamespace, false)}[{new string(',', arrayType.rank - 1)}]";
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
					{
						Il2CppType elementType = il2Cpp.GetIl2CppType(il2CppType.data.type);
						Il2CppTypeInfo elementTypeInfo = GetTypeInfoInternal(elementType, is_nested);
						elementTypeInfo.IsArray = true;
						return elementTypeInfo;
						// typeInfo.ElementType = GetTypeInfo(elementType);
						// return typeInfo;
						// return $"{GetTypeName(elementType, addNamespace, false)}[]";
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
					{
						Il2CppType oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
						Il2CppTypeInfo ptrType = GetTypeInfoInternal(oriType, is_nested);
						ptrType.TypeIndex = typeInfo.TypeIndex;
						++ptrType.Indirection;
						return ptrType;
						// return $"{GetTypeName(oriType, addNamespace, false)}*";
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
				case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
					{
						Il2CppGenericParameter param = GetGenericParameteFromIl2CppType(il2CppType);
						// just a string name, no unique constraints
						typeInfo.TypeName = metadata.GetStringFromIndex(param.nameIndex);
						return typeInfo;
						//return metadata.GetStringFromIndex(param.nameIndex);
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_STRING:
					{
						typeInfo.TypeName = "String";
						typeInfo.Namespace = "System";
						++typeInfo.Indirection;
						return typeInfo;
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
				case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
				case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
					{
						Il2CppTypeDefinition typeDef;
						Il2CppGenericClass genericClass = null;
						if (il2CppType.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
						{
							genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
							typeDef = GetGenericClassTypeDefinition(genericClass);
						}
						else
						{
							typeDef = GetTypeDefinitionFromIl2CppType(il2CppType);
							if (il2CppType.type == Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE && typeDef.IsEnum)
							{
								return GetTypeInfo(il2Cpp.types[typeDef.elementTypeIndex]);
							}

						}
						Il2CppTypeInfo concreteType = GetTypeInfo(typeDef, genericClass, true);
						++concreteType.Indirection;
						return concreteType;
					}
				default:
					{
						typeInfo.IsPrimitive = true;
						typeInfo.TypeName = TypeString[(int)il2CppType.type];
						return typeInfo;
					}
			}
		}

		public Il2CppTypeDefinitionInfo GetTypeDefInfo(Il2CppTypeDefinition typeDef, Il2CppGenericClass genericClass = null, bool is_nested = false)
		{
			Il2CppTypeInfo typeInfo = GetTypeInfo(typeDef, genericClass);
			Il2CppTypeDefinitionInfo typeDefInfo = new Il2CppTypeDefinitionInfo(typeInfo);
			AddFields(typeDef, typeDefInfo);

			return typeDefInfo;
		}

		private void AddFields(Il2CppTypeDefinition typeDef, Il2CppTypeDefinitionInfo typeDefInfo)
		{
			UniqueName names = new();
			int typeIndex = TypeDefToIndex[typeDef];
			if (typeDef.field_count > 0)
			{
				int fieldEnd = typeDef.fieldStart + typeDef.field_count;
				for (int i = typeDef.fieldStart; i < fieldEnd; ++i)
				{
					Il2CppFieldDefinition fieldDef = metadata.fieldDefs[i];
					Il2CppType fieldType = il2Cpp.types[fieldDef.typeIndex];
					if ((fieldType.attrs & FIELD_ATTRIBUTE_LITERAL) != 0)
					{
						continue;
					}
					Il2CppFieldInfo structFieldInfo = new()
					{
						Type = GetTypeInfoInternal(fieldType)
					};
					string fieldName = metadata.GetStringFromIndex(fieldDef.nameIndex);
					structFieldInfo.Name = names.Get(fieldName);
					bool isStatic = (fieldType.attrs & FIELD_ATTRIBUTE_STATIC) != 0;
					if (typeIndex < 0)
					{
						throw new Exception("type is not in typeDef list!");
					}

					structFieldInfo.Offset = il2Cpp.GetFieldOffsetFromIndex(typeIndex, i - typeDef.fieldStart, i, typeDef.IsValueType, isStatic);
					if (isStatic)
					{
						typeDefInfo.StaticFields.Add(structFieldInfo);
					}
					else
					{
						typeDefInfo.Fields.Add(structFieldInfo);
					}
				}
			}
		}

		/**/

		public string GetTypeName(Il2CppType il2CppType, bool addNamespace, bool is_nested)
		{
			switch (il2CppType.type)
			{
				case Il2CppTypeEnum.IL2CPP_TYPE_ARRAY:
					{
						Il2CppArrayType arrayType = il2Cpp.MapVATR<Il2CppArrayType>(il2CppType.data.array);
						Il2CppType elementType = il2Cpp.GetIl2CppType(arrayType.etype);
						return $"{GetTypeName(elementType, addNamespace, false)}[{new string(',', arrayType.rank - 1)}]";
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_SZARRAY:
					{
						Il2CppType elementType = il2Cpp.GetIl2CppType(il2CppType.data.type);
						return $"{GetTypeName(elementType, addNamespace, false)}[]";
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_PTR:
					{
						Il2CppType oriType = il2Cpp.GetIl2CppType(il2CppType.data.type);
						return $"{GetTypeName(oriType, addNamespace, false)}*";
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_VAR:
				case Il2CppTypeEnum.IL2CPP_TYPE_MVAR:
					{
						Il2CppGenericParameter param = GetGenericParameteFromIl2CppType(il2CppType);
						return metadata.GetStringFromIndex(param.nameIndex);
					}
				case Il2CppTypeEnum.IL2CPP_TYPE_CLASS:
				case Il2CppTypeEnum.IL2CPP_TYPE_VALUETYPE:
				case Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST:
					{
						string str = string.Empty;
						Il2CppTypeDefinition typeDef;
						Il2CppGenericClass genericClass = null;
						if (il2CppType.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST)
						{
							genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
							typeDef = GetGenericClassTypeDefinition(genericClass);
						}
						else
						{
							typeDef = GetTypeDefinitionFromIl2CppType(il2CppType);
						}
						if (typeDef.declaringTypeIndex != -1)
						{
							str += GetTypeName(il2Cpp.types[typeDef.declaringTypeIndex], addNamespace, true);
							str += '.';
						}
						else if (addNamespace)
						{
							string @namespace = metadata.GetStringFromIndex(typeDef.namespaceIndex);
							if (@namespace != "")
							{
								str += @namespace + ".";
							}
						}

						string typeName = metadata.GetStringFromIndex(typeDef.nameIndex);
						int index = typeName.IndexOf("`");
						if (index != -1)
						{
							str += typeName.Substring(0, index);
						}
						else
						{
							str += typeName;
						}

						if (is_nested)
						{
							return str;
						}

						if (genericClass != null)
						{
							Il2CppGenericInst genericInst = il2Cpp.MapVATR<Il2CppGenericInst>(genericClass.context.class_inst);
							str += GetGenericInstParams(genericInst);
						}
						else if (typeDef.genericContainerIndex >= 0)
						{
							Il2CppGenericContainer genericContainer = metadata.genericContainers[typeDef.genericContainerIndex];
							str += GetGenericContainerParams(genericContainer);
						}

						return str;
					}
				default:
					return TypeString[(int)il2CppType.type];
			}
		}

		public string GetTypeDefName(Il2CppTypeDefinition typeDef, bool addNamespace, bool genericParameter)
		{
			string prefix = string.Empty;
			if (typeDef.declaringTypeIndex != -1)
			{
				prefix = GetTypeName(il2Cpp.types[typeDef.declaringTypeIndex], addNamespace, true) + ".";
			}
			else if (addNamespace)
			{
				string @namespace = metadata.GetStringFromIndex(typeDef.namespaceIndex);
				if (@namespace != "")
				{
					prefix = @namespace + ".";
				}
			}
			string typeName = metadata.GetStringFromIndex(typeDef.nameIndex);
			if (typeDef.genericContainerIndex >= 0)
			{
				int index = typeName.IndexOf("`");
				if (index != -1)
				{
					typeName = typeName.Substring(0, index);
				}
				if (genericParameter)
				{
					Il2CppGenericContainer genericContainer = metadata.genericContainers[typeDef.genericContainerIndex];
					typeName += GetGenericContainerParams(genericContainer);
				}
			}
			return prefix + typeName;
		}

		public string GetGenericInstParams(Il2CppGenericInst genericInst)
		{
			List<string> genericParameterNames = new List<string>();
			ulong[] pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
			for (int i = 0; i < genericInst.type_argc; i++)
			{
				Il2CppType il2CppType = il2Cpp.GetIl2CppType(pointers[i]);
				genericParameterNames.Add(GetTypeName(il2CppType, false, false));
			}
			return $"<{string.Join(", ", genericParameterNames)}>";
		}

		public Il2CppType[] GetGenericInstParamList(Il2CppGenericInst genericInst)
		{
			Il2CppType[] genericParameterTypes = new Il2CppType[genericInst.type_argc];
			ulong[] pointers = il2Cpp.MapVATR<ulong>(genericInst.type_argv, genericInst.type_argc);
			for (int i = 0; i < genericInst.type_argc; i++)
			{
				Il2CppType il2CppType = il2Cpp.GetIl2CppType(pointers[i]);
				genericParameterTypes[i] = il2CppType;
			}
			return genericParameterTypes;
		}

		public string[] GetGenericContainerParamNames(Il2CppGenericContainer genericContainer)
		{
			string[] genericParameterNames = new string[genericContainer.type_argc];
			for (int i = 0; i < genericContainer.type_argc; i++)
			{
				int genericParameterIndex = genericContainer.genericParameterStart + i;
				Il2CppGenericParameter genericParameter = metadata.genericParameters[genericParameterIndex];
				genericParameterNames[i] = metadata.GetStringFromIndex(genericParameter.nameIndex);
			}
			return genericParameterNames;
		}

		public string GetGenericContainerParams(Il2CppGenericContainer genericContainer)
		{
			List<string> genericParameterNames = new List<string>();
			for (int i = 0; i < genericContainer.type_argc; i++)
			{
				int genericParameterIndex = genericContainer.genericParameterStart + i;
				Il2CppGenericParameter genericParameter = metadata.genericParameters[genericParameterIndex];
				genericParameterNames.Add(metadata.GetStringFromIndex(genericParameter.nameIndex));
			}
			return $"<{string.Join(", ", genericParameterNames)}>";
		}

		public (string, string, string) GetMethodSpecName(Il2CppMethodSpec methodSpec, bool addNamespace = false)
		{
			Il2CppMethodDefinition methodDef = metadata.methodDefs[methodSpec.methodDefinitionIndex];
			Il2CppTypeDefinition typeDef = metadata.typeDefs[methodDef.declaringType];
			string typeName = GetTypeDefName(typeDef, addNamespace, false);
			string typeParameters = null;
			if (methodSpec.classIndexIndex != -1)
			{
				Il2CppGenericInst classInst = il2Cpp.genericInsts[methodSpec.classIndexIndex];
				typeParameters = GetGenericInstParams(classInst);
			}
			string methodName = metadata.GetStringFromIndex(methodDef.nameIndex);
			if (methodSpec.methodIndexIndex != -1)
			{
				Il2CppGenericInst methodInst = il2Cpp.genericInsts[methodSpec.methodIndexIndex];
				methodName += GetGenericInstParams(methodInst);
			}
			return (typeName, methodName, typeParameters);
		}

		public Il2CppGenericContext GetMethodSpecGenericContext(Il2CppMethodSpec methodSpec)
		{
			ulong classInstPointer = 0ul;
			ulong methodInstPointer = 0ul;
			if (methodSpec.classIndexIndex != -1)
			{
				classInstPointer = il2Cpp.genericInstPointers[methodSpec.classIndexIndex];
			}
			if (methodSpec.methodIndexIndex != -1)
			{
				methodInstPointer = il2Cpp.genericInstPointers[methodSpec.methodIndexIndex];
			}
			return new Il2CppGenericContext { class_inst = classInstPointer, method_inst = methodInstPointer };
		}

		public Il2CppRGCTXDefinition[] GetRGCTXDefinition(string imageName, Il2CppTypeDefinition typeDef)
		{
			Il2CppRGCTXDefinition[] collection = null;
			if (il2Cpp.Version >= 24.2f)
			{
				il2Cpp.rgctxsDictionary[imageName].TryGetValue(typeDef.token, out collection);
			}
			else
			{
				if (typeDef.rgctxCount > 0)
				{
					collection = new Il2CppRGCTXDefinition[typeDef.rgctxCount];
					Array.Copy(metadata.rgctxEntries, typeDef.rgctxStartIndex, collection, 0, typeDef.rgctxCount);
				}
			}
			return collection;
		}

		public Il2CppRGCTXDefinition[] GetRGCTXDefinition(string imageName, Il2CppMethodDefinition methodDef)
		{
			Il2CppRGCTXDefinition[] collection = null;
			if (il2Cpp.Version >= 24.2f)
			{
				il2Cpp.rgctxsDictionary[imageName].TryGetValue(methodDef.token, out collection);
			}
			else
			{
				if (methodDef.rgctxCount > 0)
				{
					collection = new Il2CppRGCTXDefinition[methodDef.rgctxCount];
					Array.Copy(metadata.rgctxEntries, methodDef.rgctxStartIndex, collection, 0, methodDef.rgctxCount);
				}
			}
			return collection;
		}

		public Il2CppTypeDefinition GetGenericClassTypeDefinition(Il2CppGenericClass genericClass)
		{
			if (il2Cpp.Version >= 27)
			{
				Il2CppType il2CppType = il2Cpp.GetIl2CppType(genericClass.type);
				return GetTypeDefinitionFromIl2CppType(il2CppType);
			}
			if (genericClass.typeDefinitionIndex == 4294967295 || genericClass.typeDefinitionIndex == -1)
			{
				return null;
			}
			return metadata.typeDefs[genericClass.typeDefinitionIndex];
		}

		public Il2CppTypeDefinition GetTypeDefinitionFromIl2CppType(Il2CppType il2CppType, bool resolveGeneric = true)
		{
			if (il2Cpp.Version >= 27 && il2Cpp is ElfBase elf && elf.IsDumped)
			{
				ulong offset = il2CppType.data.typeHandle - metadata.Address - metadata.header.typeDefinitionsOffset;
				ulong index = offset / (ulong)metadata.SizeOf(typeof(Il2CppTypeDefinition));
				return metadata.typeDefs[index];
			}
			else
			{
				if (il2CppType.type == Il2CppTypeEnum.IL2CPP_TYPE_GENERICINST && resolveGeneric)
				{
					Il2CppGenericClass genericClass = il2Cpp.MapVATR<Il2CppGenericClass>(il2CppType.data.generic_class);
					return GetGenericClassTypeDefinition(genericClass);
				}
				if (il2CppType.data.klassIndex < metadata.typeDefs.Length)
				{
					return metadata.typeDefs[il2CppType.data.klassIndex];
				}
				return null;
			}
		}

		public Il2CppType GetIl2CppTypeFromTypeDefinition(Il2CppTypeDefinition typeDef)
		{
			int typeDefIndex = TypeDefToIndex[typeDef];
			if (typeDefIndex == -1)
			{
				throw new KeyNotFoundException("typedef not found");
			}
			return il2Cpp.types[typeDef.byrefTypeIndex];
		}

		public Il2CppGenericParameter GetGenericParameteFromIl2CppType(Il2CppType il2CppType)
		{
			if (il2Cpp.Version >= 27 && il2Cpp is ElfBase elf && elf.IsDumped)
			{
				ulong offset = il2CppType.data.genericParameterHandle - metadata.Address - metadata.header.genericParametersOffset;
				ulong index = offset / (ulong)metadata.SizeOf(typeof(Il2CppGenericParameter));
				return metadata.genericParameters[index];
			}
			else
			{
				return metadata.genericParameters[il2CppType.data.genericParameterIndex];
			}
		}
	}
}
