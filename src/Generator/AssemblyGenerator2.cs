using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Il2CppDumper;
using IL2CS.Runtime;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using static Il2CppDumper.Il2CppConstants;

namespace IL2CS.Generator
{
	public class AssemblyGenerator2
	{
		private Dictionary<string, Type> m_typeCache = new();
		private readonly List<TypeBuilder> m_typesToBuild = new();

		private readonly string m_assemblyName;
		private readonly AssemblyGeneratorOptions m_options;
		private readonly AssemblyName m_asmName;
		private readonly AssemblyBuilder m_asm;
		private readonly ModuleBuilder m_module;
		private readonly AssemblyGeneratorContext m_context;
		private readonly TypeCollector m_collector;
		private readonly ILogger m_logger;
		public AssemblyGenerator2(AssemblyGeneratorOptions options)
		{
			m_options = options;
			m_assemblyName = System.IO.Path.GetFileName(m_options.GameAssemblyPath);
			m_context = new AssemblyGeneratorContext(options);
			m_asmName = new AssemblyName(options.AssembyName);
			m_asm = AssemblyBuilder.DefineDynamicAssembly(m_asmName, AssemblyBuilderAccess.RunAndCollect);
			m_module = m_asm.DefineDynamicModule(m_asmName.Name);
			m_collector = new TypeCollector(m_context);
			m_logger = options.LogFactory.CreateLogger("asmgen");
		}

		private static string FieldName(Il2CppFieldInfo fieldInfo)
		{
			return System.Text.RegularExpressions.Regex.Replace(fieldInfo.Name, "<(.+)>k__BackingField", match => match.Groups[1].Value);
		}


		private void ProcessImage(Il2CppImageDefinition imageDef)
		{
			string imageName = m_context.Metadata.GetStringFromIndex(imageDef.nameIndex);
			long typeEnd = imageDef.typeStart + imageDef.typeCount;
			for (int typeDefIndex = imageDef.typeStart; typeDefIndex < typeEnd; typeDefIndex++)
			{
				if (!m_options.IncludeImages.Contains(imageName))
				{
					continue;
				}
				Il2CppTypeDefinition typeDef = m_context.Metadata.typeDefs[typeDefIndex];
				m_collector.VisitTypeDefinition(typeDef);
				//GetType(typeDef);
			}
		}

		private Type GetType(Il2CppType typeInfo)
		{
			string typeName = m_context.Executor.GetTypeName(typeInfo, true, false);

			if (m_typeCache.TryGetValue(typeName, out Type cachedType))
			{
				return cachedType;
			}
			try
			{
				Type builtIn = Type.GetType(typeName);
				if (builtIn != null)
				{
					m_typeCache.Add(typeName, builtIn);
					return builtIn;
				}
			}
			catch { }
			return BuildType(typeInfo);
		}

		private Type GetType(Il2CppTypeDefinition typeDef)
		{
			string typeName = m_context.Executor.GetTypeDefName(typeDef, true, true);

			if (m_typeCache.TryGetValue(typeName, out Type cachedType))
			{
				return cachedType;
			}
			try
			{
				Type builtIn = Type.GetType(typeName);
				if (builtIn != null)
				{
					m_typeCache.Add(typeName, builtIn);
					return builtIn;
				}
			}
			catch { }
			return BuildType(typeDef);
		}

		private Type BuildType(Il2CppType typeInfo)
		{
			Il2CppTypeDefinition typeDef = m_context.Executor.GetTypeDefinitionFromIl2CppType(typeInfo);
			return BuildType(typeDef);
		}

		private Type BuildType(Il2CppTypeDefinition typeDef)
		{
			string typeName = m_context.Executor.GetTypeDefName(typeDef, true, false);
			using (m_logger.BeginScope($"BuildType: {typeName}"))
			{
				if (typeDef.IsEnum)
				{
					return BuildEnum(typeDef);
				}
				return BuildClass(typeDef);
			}
		}

		private Type BuildEnum(Il2CppTypeDefinition typeDef)
		{
			// TODO
			return null;
		}

		private Type BuildClass(Il2CppTypeDefinition typeDef)
		{
			string typeName = m_context.Executor.GetTypeDefName(typeDef, true, false);
			TypeAttributes attribs = GetTypeAttributes(typeDef);
			Type baseType = typeof(StructBase);
			if (attribs.HasFlag(TypeAttributes.Interface))
			{
				baseType = null;
			}
			if (typeDef.parentIndex >= 0)
			{
				Il2CppType parent = m_context.Il2Cpp.types[typeDef.parentIndex];
				string parentName = m_context.Executor.GetTypeName(parent, true, false);
				if (!typeDef.IsValueType && !typeDef.IsEnum && parentName != "System.Object")
				{
					baseType = GetType(parent);
				}
			}

			if (typeName.IndexOfAny(new char[] { '<', '>', '_', '(', ')', '`' }) > -1)
			{
				m_logger.LogDebug($"Skipping '{typeName}'");
				return null;
			}

			Type type;
			if (typeDef.declaringTypeIndex != -1)
			{
				if (GetType(m_context.Il2Cpp.types[typeDef.declaringTypeIndex]) is TypeBuilder declaringTypeBuilder)
				{
					type = DefineType(typeName, attribs, baseType, declaringTypeBuilder);
				}
				else
				{
					m_logger.LogWarning($"Dropping type with missing declaring type '{typeName}'");
					return null;
				}
			}
			else
			{
				type = DefineType(typeName, attribs, baseType);
			}

			//if (typeDef.interfaces_count > 0)
			//{
			//	for (int i = 0; i < typeDef.interfaces_count; i++)
			//	{
			//		Il2CppType interfaceType = m_context.Il2Cpp.types[m_context.Metadata.interfaceIndices[typeDef.interfacesStart + i]];
			//		Type interfaceBuilder = GetType(interfaceType);
			//		if (interfaceBuilder == null)
			//		{
			//			m_logger.LogWarning($"Could not find interface type [{i}] for '{typeName}'");
			//			continue;
			//		}
			//		tb.AddInterfaceImplementation(interfaceBuilder);
			//	}
			//}

			return type;
		}

		private static TypeAttributes GetTypeAttributes(Il2CppTypeDefinition typeDef)
		{
			//return (TypeAttributes)typeDef.flags;
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
			else if (typeDef.IsEnum)
			{ }
			else if (typeDef.IsValueType)
			{ }
			else
			{ }
			return attrs;
		}

		private Type DefineType(string typeName, TypeAttributes attribs, Type baseType, TypeBuilder declaringType = null)
		{
			if (m_typeCache.TryGetValue(typeName, out var type))
			{
				m_logger.LogDebug($"Using cached type '{typeName}'");
				if (type != null)
				{
					return type;
				}
			}
			m_logger.LogDebug($"Defining type '{typeName}'");
			TypeBuilder tb;
			if (declaringType != null)
			{
				tb = declaringType.DefineNestedType(typeName, attribs, baseType);
			}
			else
			{
				tb = m_module.DefineType(typeName, attribs, baseType);
			}
			m_typesToBuild.Add(tb);
			m_typeCache.Add(typeName, tb);
			return tb;
		}

		public void Generate(string outputPath)
		{
			foreach (Il2CppImageDefinition imageDef in m_context.Metadata.imageDefs)
			{
				ProcessImage(imageDef);
			}

			foreach (TypeBuilder type in m_typesToBuild)
			{
				m_logger.LogInformation($"Building type '{type.FullName}'");
				// TODO:
				// Create ctors based on .ctor
				// Add fields
				// Add statics
				// Add methods
				type.CreateType();
			}
			Lokad.ILPack.AssemblyGenerator generator = new();
			string outputFile = outputPath;
			if (System.IO.Path.GetExtension(outputFile) != ".dll")
			{
				outputFile = System.IO.Path.Join(outputPath, $"{m_asmName.Name}.dll");
			}
			generator.GenerateAssembly(m_asm, outputFile);
		}
	}
}
