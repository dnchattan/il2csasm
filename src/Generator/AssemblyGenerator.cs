using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using IL2CS.Core;
using IL2CS.Runtime;

namespace IL2CS.Generator
{
	public class AssemblyGenerator
	{
		private readonly string m_assemblyName;
		private readonly AssemblyGeneratorOptions m_options;
		private readonly AssemblyName m_asmName;
		private readonly AssemblyBuilder m_asm;
		private readonly Il2CppIndex m_index;
		private readonly ModuleBuilder m_module;
		private readonly AssemblyGeneratorContext m_context;
		public AssemblyGenerator(AssemblyGeneratorOptions options)
		{
			m_options = options;
			m_assemblyName = System.IO.Path.GetFileName(m_options.GameAssemblyPath);
			m_context = new AssemblyGeneratorContext(options);
			m_asmName = new AssemblyName(options.AssembyName);
			m_asm = AssemblyBuilder.DefineDynamicAssembly(m_asmName, AssemblyBuilderAccess.RunAndCollect);
			m_module = m_asm.DefineDynamicModule(m_asmName.Name);
			m_index = new Il2CppIndex(m_context);
		}

		private static string FullyQualifiedNameFor(Il2CppTypeInfo typeInfo)
		{
			return FullyQualifiedName(typeInfo.Namespace, typeInfo.TypeName);
		}

		private static string FullyQualifiedName(string ns, string typeName)
		{
			if (!string.IsNullOrEmpty(ns))
			{
				return $"{ns}.{typeName}";
			}
			return typeName;
		}

		private static string FieldName(Il2CppFieldInfo fieldInfo)
		{
			return System.Text.RegularExpressions.Regex.Replace(fieldInfo.Name, "<(.+)>k__BackingField", match => match.Groups[1].Value);
		}

		public void Generate(string outputPath)
		{
			Console.WriteLine(string.Join("\n", m_index.TypeInfoList.Select(t => t.ImageName).Distinct().ToArray()));
			Dictionary<TypeBuilder, Il2CppTypeDefinitionInfo> types = new();
			foreach (Il2CppTypeDefinitionInfo typeDef in m_index.TypeInfoList)
			{
				if (!m_options.IncludeImages.Contains(typeDef.ImageName))
				{
					continue;
				}
				if (typeDef.Type.TypeName.IndexOfAny(new char[] { '<', '>', '_', '(', ')', '`' }) > -1)
				{
					continue;
				}
				if (typeDef.Type.DeclaringType != null)
				{
					TypeBuilder declaringType = GetType(typeDef.Type.TypeIndex) as TypeBuilder;
					if (declaringType != null)
					{
						TypeBuilder nestedBuilder = CreateType(typeDef.Type, declaringType: declaringType);
						m_typesToBuild.Add(nestedBuilder);
						types.Add(nestedBuilder, typeDef);
					}
					continue;
				}
				// force type stubs to be generated first
				TypeBuilder tb = GetType(typeDef.Type.TypeIndex) as TypeBuilder;
				if (tb != null)
				{
					types.Add(tb, typeDef);
				}
			}
			foreach (KeyValuePair<TypeBuilder, Il2CppTypeDefinitionInfo> entry in types)
			{
				try
				{
					BuildType(entry.Key, entry.Value);
				}
				catch (Exception e)
				{
					Console.Error.WriteLine($"{FullyQualifiedNameFor(entry.Value.Type)}: {e.Message}");
				}
			}
			foreach (TypeBuilder type in m_typesToBuild)
			{
				type.CreateType();
			}
			Lokad.ILPack.AssemblyGenerator generator = new Lokad.ILPack.AssemblyGenerator();
			//var bytes = generator.GenerateAssemblyBytes(m_asm);
			string outputFile = outputPath;
			if (System.IO.Path.GetExtension(outputFile) != ".dll")
			{
				outputFile = System.IO.Path.Join(outputPath, $"{m_asmName.Name}.dll");
			}
			generator.GenerateAssembly(m_asm, outputFile);
		}

		private readonly Dictionary<long, Type> m_types = new();
		private readonly List<TypeBuilder> m_typesToBuild = new();
		private Type GetType(long il2cppTypeIndex)
		{
			if (!m_types.ContainsKey(il2cppTypeIndex))
			{
				Il2CppDumper.Il2CppType il2CppType = m_context.Il2Cpp.types[il2cppTypeIndex];
				Il2CppTypeInfo typeInfo = m_context.Executor.GetTypeInfo(il2CppType);

				Type type = Type.GetType(FullyQualifiedNameFor(typeInfo));
				if (type != null)
				{
					m_types.Add(il2cppTypeIndex, type);
					return type;
				}
				
				TypeBuilder typeBuilder = CreateType(typeInfo);
				m_types.Add(il2cppTypeIndex, typeBuilder);
				m_typesToBuild.Add(typeBuilder);
			}
			return m_types[il2cppTypeIndex];
		}

		private void BuildType(TypeBuilder typeBuilder, Il2CppTypeDefinitionInfo typeDef)
		{
			TypeBuilder staticsBuilder = null;
			void EnsureNestedStaticType()
			{
				if (staticsBuilder != null)
				{
					return;
				}
				staticsBuilder = CreateType(typeDef.Type, declaringType: typeBuilder, isStatic: true);
			}

			UniqueName nameSet = new();
			//typeBuilder.DefineDefaultConstructor(MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName);
			foreach (Il2CppFieldInfo field in typeDef.Fields)
			{
				FieldBuilder fieldBuilder = typeBuilder.DefineField(nameSet.Get(FieldName(field)), GetType(field.Type.TypeIndex), FieldAttributes.Public);
				fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(OffsetAttribute).GetConstructor(new Type[] { typeof(int) }), new object[] { field.Offset }));
				if (field.Type.Indirection > 1)
				{
					fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(IndirectionAttribute).GetConstructor(new Type[] { typeof(byte) }), new object[] { (byte)field.Type.Indirection }));
				}
			}
			UniqueName staticNameSet = new();
			if (typeDef.StaticFields.Count > 0)
			{
				EnsureNestedStaticType();
				foreach (Il2CppFieldInfo field in typeDef.StaticFields)
				{
					FieldBuilder fieldBuilder = staticsBuilder.DefineField(staticNameSet.Get(FieldName(field)), GetType(field.Type.TypeIndex), FieldAttributes.Public);
					fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(OffsetAttribute).GetConstructor(new Type[] { typeof(int) }), new object[] { field.Offset }));
					if (field.Type.Indirection > 1)
					{
						fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(IndirectionAttribute).GetConstructor(new Type[] { typeof(byte) }), new object[] { (byte)field.Type.Indirection }));
					}
				}
			}
			if (m_index.TypeNameToStaticMethods.TryGetValue(FullyQualifiedNameFor(typeDef.Type), out List<StructStaticMethodInfo> methods))
			{
				EnsureNestedStaticType();
				foreach (StructStaticMethodInfo method in methods)
				{
					if (method.Name.StartsWith("."))
					{
						continue;
					}
					FieldBuilder fieldBuilder = staticsBuilder.DefineField(staticNameSet.Get(method.Name), typeof(MethodDefinition), FieldAttributes.Public);
					fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(AddressAttribute).GetConstructor(new Type[] { typeof(long), typeof(string) }), new object[] { (long)method.Address, m_assemblyName }));
					fieldBuilder.SetCustomAttribute(new CustomAttributeBuilder(typeof(IndirectionAttribute).GetConstructor(new Type[] { typeof(byte) }), new object[] { (byte)2 }));
				}
			}
		}

		private TypeBuilder CreateType(Il2CppTypeInfo typeDef, TypeAttributes typeAttrs = TypeAttributes.Class, TypeBuilder declaringType = null, bool isStatic = false)
		{
			TypeBuilder tb;
			if (declaringType != null)
			{
				string typeName = FullyQualifiedName(declaringType.FullName, isStatic ? "Statics" : typeDef.TypeName);
				var baseType = isStatic ? typeof(StaticStructBase) : typeof(StructBase);
				tb = declaringType.DefineNestedType(typeName, typeAttrs | TypeAttributes.NestedPublic, baseType);
				if (isStatic)
				{
					tb.SetCustomAttribute(new CustomAttributeBuilder(typeof(StaticAttribute).GetConstructor(Type.EmptyTypes), Array.Empty<object>()));
				}
			}
			else
			{
				tb = m_module.DefineType(FullyQualifiedNameFor(typeDef), TypeAttributes.Public | TypeAttributes.Class, typeof(StructBase));
			}
			m_typesToBuild.Add(tb);
			return tb;
		}
	}
}
