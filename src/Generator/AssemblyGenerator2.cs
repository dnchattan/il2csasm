using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using Il2CppDumper;
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
            descriptor.GetTypeBuilder();
        }

        public void ResolveTypeBuilder(object sender, ResolveTypeBuilderEventArgs eventArgs)
        {
            TypeDescriptor descriptor = eventArgs.Request;
            TypeBuilder genericParent = descriptor.GenericParent?.GetTypeBuilder();
            if (genericParent != null)
            {
                eventArgs.Result = genericParent.MakeGenericType(descriptor.GenericTypeParams.Select(tp => tp.GetGeneratedType()).ToArray());
                return;
            }

            TypeBuilder elementType = descriptor.ElementType?.GetTypeBuilder();
            if (elementType != null)
            {
                eventArgs.Result = elementType.MakeArrayType(descriptor.ArrayRank ?? 1);
                return;
            }


            Il2CppTypeDefinition typeDef = descriptor.TypeDef;
            TypeAttributes attribs = Helpers.GetTypeAttributes(typeDef);

            Type baseType;
            if (typeDef.IsEnum)
            {
                baseType = typeof(Enum);
                // TODO
                return;
            }
            else
            {
                TypeBuilder baseTypeBuilder = descriptor.Base?.GetTypeBuilder();
                if (baseTypeBuilder.IsGenericType)
				{
                    // TODO: fill with generic types!
                    baseType = baseTypeBuilder.MakeGenericType();
				}
                else
				{
                    baseType = baseTypeBuilder;
				}
            }

            string[] genericParametersNames = Array.Empty<string>();
            if (typeDef.genericContainerIndex >= 0)
            {
                Il2CppGenericContainer genericContainer = m_context.Metadata.genericContainers[typeDef.genericContainerIndex];
                genericParametersNames = m_context.Executor.GetGenericContainerParamNames(genericContainer);
            }

            TypeBuilder tb;
            TypeBuilder declaringType = descriptor.DeclaringParent?.GetTypeBuilder();
            if (declaringType != null)
            {
                tb = declaringType.DefineNestedType(descriptor.Name, attribs, baseType);
            }
            else
            {
                tb = m_module.DefineType(descriptor.Name, attribs, baseType);
            }

            if (descriptor.GenericParameterNames.Length > 0)
            {
                tb.DefineGenericParameters(descriptor.GenericParameterNames);
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
                if (!m_options.IncludeImages.Contains(imageName))
                {
                    continue;
                }
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
    }
}
