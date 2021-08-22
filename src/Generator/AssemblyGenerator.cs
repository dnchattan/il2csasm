using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using IL2CS.Runtime;

namespace IL2CS.Generator
{
	public class AssemblyGenerator
	{
		private readonly AssemblyGeneratorOptions m_options;
		private readonly AssemblyName m_asmName;
		private readonly AssemblyBuilder m_asm;
		private readonly Assembly m_coreAsm;
		private readonly Il2CppIndex m_index;
		private readonly ModuleBuilder m_module;
		private readonly AssemblyGeneratorContext m_context;
		public AssemblyGenerator(AssemblyGeneratorOptions options)
		{
			m_options = options;
			m_context = new AssemblyGeneratorContext(options);
			m_asmName = new AssemblyName(options.AssembyName);
			m_asm = AssemblyBuilder.DefineDynamicAssembly(m_asmName, AssemblyBuilderAccess.RunAndCollect);
			m_module = m_asm.DefineDynamicModule(m_asmName.Name);
			m_index = new Il2CppIndex(m_context);
		}

		private static string FullyQualifiedNameFor(Il2CppTypeInfo typeInfo)
		{
			if (!string.IsNullOrEmpty(typeInfo.Namespace))
			{
				return $"{typeInfo.Namespace}.{typeInfo.TypeName}";
			}
			return typeInfo.TypeName;
		}

		public void Generate()
		{
			Console.WriteLine(string.Join("\n", m_index.TypeInfoList.Select(t => t.ImageName).Distinct().ToArray()));
			Dictionary<TypeBuilder, Il2CppTypeDefinitionInfo> types = new();
			foreach (Il2CppTypeDefinitionInfo typeDef in m_index.TypeInfoList)
			{
				if (!m_options.IncludeImages.Contains(typeDef.ImageName))
				{
					continue;
				}
				// force type stubs to be generated first
				TypeBuilder tb = GetType(typeDef.Type.TypeIndex) as TypeBuilder;
				if (tb != null)
				{
					types.Add(tb, typeDef);
				}
			}
			foreach (var entry in types)
			{
				try
				{
					BuildType(entry.Key, entry.Value);
				}
				catch(Exception e)
				{
					Console.Error.WriteLine(e.ToString());
				}
			}
			foreach (var type in m_types.Values.OfType<TypeBuilder>())
			{
				type.CreateType();
			}
			Lokad.ILPack.AssemblyGenerator generator = new Lokad.ILPack.AssemblyGenerator();
			//var bytes = generator.GenerateAssemblyBytes(m_asm);
			generator.GenerateAssembly(m_asm, $"{m_asmName.Name}.dll");
		}

		private readonly Dictionary<long, Type> m_types = new();
		private Type GetType(long il2cppTypeIndex)
		{
			if (!m_types.ContainsKey(il2cppTypeIndex))
			{
				Il2CppDumper.Il2CppType il2CppType = m_context.Il2Cpp.types[il2cppTypeIndex];
				Il2CppTypeInfo typeInfo = m_context.Executor.GetTypeInfo(il2CppType);

				var type = Type.GetType(FullyQualifiedNameFor(typeInfo));
				if (type != null)
				{
					m_types.Add(il2cppTypeIndex, type);
					return type;
				}

				TypeBuilder typeBuilder = m_module.DefineType(FullyQualifiedNameFor(typeInfo), TypeAttributes.Public | TypeAttributes.Class, typeof(StructBase));
				m_types.Add(il2cppTypeIndex, typeBuilder);
			}
			return m_types[il2cppTypeIndex];
		}

		private void BuildType(TypeBuilder type, Il2CppTypeDefinitionInfo typeDef)
		{
			type.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			foreach (var field in typeDef.Fields)
			{
				type.DefineField(field.Name, GetType(field.Type.TypeIndex), FieldAttributes.Public);
			}
		}
	}
}
