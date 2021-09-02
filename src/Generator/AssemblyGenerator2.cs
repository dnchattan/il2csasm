using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Il2CppDumper;
using IL2CS.Runtime;
using Microsoft.Extensions.Logging;
using static IL2CS.Generator.TypeCollector;

namespace IL2CS.Generator
{
	public class AssemblyGenerator2
	{
		private readonly Dictionary<string, Type> m_typeCache = new();
		private readonly List<TypeBuilder> m_typesToBuild = new();

		private readonly string m_assemblyName;
		private readonly AssemblyGeneratorOptions m_options;
		private readonly AssemblyName m_asmName;
		private readonly AssemblyBuilder m_asm;
		private readonly ModuleBuilder m_module;
		private readonly AssemblyGeneratorContext m_context;
		private readonly TypeCollector m_collector;
		private readonly ILogger m_logger;
		private Dictionary<int, string> m_typeToImageName = new();
		public AssemblyGenerator2(AssemblyGeneratorOptions options)
		{
			m_options = options;
			m_assemblyName = System.IO.Path.GetFileName(m_options.GameAssemblyPath);
			m_context = new AssemblyGeneratorContext(options);
			m_asmName = new AssemblyName(options.AssembyName);
			m_asm = AssemblyBuilder.DefineDynamicAssembly(m_asmName, AssemblyBuilderAccess.RunAndCollect);
			m_module = m_asm.DefineDynamicModule(m_asmName.Name);
			m_logger = options.LogFactory.CreateLogger("asmgen");

			m_collector = new TypeCollector(m_context);
			m_collector.OnResolveType += ResolveTypeBuilder;
		}

		private static string FieldName(Il2CppFieldInfo fieldInfo)
		{
			return System.Text.RegularExpressions.Regex.Replace(fieldInfo.Name, "<(.+)>k__BackingField", match => match.Groups[1].Value);
		}


		private void ProcessImage(Il2CppImageDefinition imageDef)
		{
			long typeEnd = imageDef.typeStart + imageDef.typeCount;
			for (int typeDefIndex = imageDef.typeStart; typeDefIndex < typeEnd; typeDefIndex++)
			{
				Il2CppTypeDefinition typeDef = m_context.Metadata.typeDefs[typeDefIndex];
				string typeName = m_context.Executor.GetTypeDefName(typeDef, true, false);

				if (typeName.IndexOfAny(new char[] { '<', '>', '_', '(', ')', '`' }) > -1)
				{
					continue;
				}
				m_collector.CollectTypeDefinition(typeDef);
			}
		}

		public void GenerateType(TypeDescriptor descriptor)
		{
			if (descriptor.Type == null)
			{
				m_logger.LogWarning($"TypeDescriptor '{descriptor.Name}' did not generate a type");
			}
		}

		public void ResolveTypeBuilder(object sender, ResolveTypeBuilderEventArgs eventArgs)
		{
			TypeDescriptor descriptor = eventArgs.Request;
			//try
			//{
			//	int typeIndex = Array.IndexOf(m_context.Metadata.typeDefs, descriptor.TypeDef);
			//	string imageName = m_typeToImageName[typeIndex];
			//	Type builtInType = Type.GetType($"{descriptor.FullName}, System.Private.CoreLib") ?? Type.GetType($"{descriptor.FullName}, {imageName}");
			//	if (builtInType != null)
			//	{
			//		eventArgs.Result = builtInType;
			//		return;
			//	}
			//	if (descriptor.FullName.StartsWith("System."))
			//	{
			//		return;
			//	}
			//}
			//catch { }

			TypeBuilder genericParent = descriptor.GenericParent?.Type as TypeBuilder;
			if (genericParent != null)
			{
				eventArgs.Result = genericParent.MakeGenericType(descriptor.GenericTypeParams);
				return;
			}

			Il2CppTypeDefinition typeDef = descriptor.TypeDef;
			TypeAttributes attribs = Helpers.GetTypeAttributes(typeDef);

			TypeBuilder tb;
			TypeBuilder declaringType = descriptor.DeclaringParent?.Type as TypeBuilder;
			if (descriptor.DeclaringParent != null && declaringType == null)
			{
				Debugger.Break();
			}
			if (declaringType != null)
			{
				tb = declaringType.DefineNestedType(descriptor.Name, attribs, descriptor.Base);
			}
			else
			{
				if ((attribs & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPrivate || (attribs & TypeAttributes.VisibilityMask) == TypeAttributes.NestedPublic)
				{
					Debugger.Break();
				}
				tb = m_module.DefineType(descriptor.Name, attribs, descriptor.Base);
			}
			// TODO: Type Attributes

			if (descriptor.GenericParameterNames.Length > 0)
			{
				tb.DefineGenericParameters(descriptor.GenericParameterNames);
			}

			foreach(FieldDescriptor field in descriptor.Fields)
			{
				FieldBuilder fb = tb.DefineField(field.StorageName, field.Type, field.Attributes);
				if (field.DefaultValue != null)
				{
					fb.SetConstant(field.DefaultValue);
				}
				// TODO: Field Attributes
			}

			// TODO:
			// Create ctors based on .ctor
			// Add fields
			// Add statics
			// Add methods

			m_typesToBuild.Add(tb);
			eventArgs.Result = tb;
		}

		public void Generate(string outputPath)
		{
			foreach (Il2CppImageDefinition imageDef in m_context.Metadata.imageDefs)
			{
				string imageName = m_context.Metadata.GetStringFromIndex(imageDef.nameIndex);
				ProcessImage(imageDef);
			}
			m_collector.ProcessTypes();
			foreach (TypeBuilder tb in m_typesToBuild)
			{
				m_logger.LogInformation($"Creating type '{tb.FullName}'");
				tb.CreateType();
			}
			Lokad.ILPack.AssemblyGenerator generator = new();
			string outputFile = outputPath;
			if (System.IO.Path.GetExtension(outputFile) != ".dll")
			{
				outputFile = System.IO.Path.Join(outputPath, $"{m_asmName.Name}.dll");
			}
			generator.GenerateAssembly(m_asm, outputFile);
		}

		private void IndexTypeImageMapping()
		{
			foreach (Il2CppImageDefinition imageDef in m_context.Metadata.imageDefs)
			{
				string imageName = m_context.Metadata.GetStringFromIndex(imageDef.nameIndex).Replace(".dll", "");
				long typeEnd = imageDef.typeStart + imageDef.typeCount;
				for (int typeDefIndex = imageDef.typeStart; typeDefIndex < typeEnd; typeDefIndex++)
				{
					m_typeToImageName.Add(typeDefIndex, imageName);
				}
			}
		}
	}
}
