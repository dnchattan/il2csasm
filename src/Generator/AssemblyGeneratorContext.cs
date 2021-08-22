using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Il2CppDumper;

namespace IL2CS.Generator
{
	public class AssemblyGeneratorContext
	{
		public readonly Il2Cpp Il2Cpp;
		public readonly Il2CppExecutor2 Executor;
		public readonly Metadata Metadata;

		private const uint IL2CPPMAGIC_PE = 0x905A4D;

		public AssemblyGeneratorContext(AssemblyGeneratorOptions options)
		{
			byte[] metadataBytes = File.ReadAllBytes(options.MetadataPath);
			Metadata = new Metadata(new MemoryStream(metadataBytes));

			byte[] il2cppBytes = File.ReadAllBytes(options.GameAssemblyPath);
			uint il2cppMagic = BitConverter.ToUInt32(il2cppBytes, 0);
			MemoryStream il2CppMemory = new(il2cppBytes);

			if (il2cppMagic != IL2CPPMAGIC_PE)
			{
				throw new ApplicationException("Unexpected il2cpp magic number.");
			}

			Il2Cpp = new PE(il2CppMemory);


			Il2Cpp.SetProperties(Metadata.Version, Metadata.maxMetadataUsages);

			if (Il2Cpp.Version >= 27 && Il2Cpp is ElfBase elf && elf.IsDumped)
			{
				Metadata.Address = Convert.ToUInt64(Console.ReadLine(), 16);
			}

			try
			{
				bool flag = Il2Cpp.PlusSearch(Metadata.methodDefs.Count(x => x.methodIndex >= 0), Metadata.typeDefs.Length, Metadata.imageDefs.Length);
				if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				{
					if (!flag && Il2Cpp is PE)
					{
						Il2Cpp = PELoader.Load(options.GameAssemblyPath);
						Il2Cpp.SetProperties(Metadata.Version, Metadata.maxMetadataUsages);
						flag = Il2Cpp.PlusSearch(Metadata.methodDefs.Count(x => x.methodIndex >= 0), Metadata.typeDefs.Length, Metadata.imageDefs.Length);
					}
				}
				if (!flag)
				{
					flag = Il2Cpp.Search();
				}
				if (!flag)
				{
					flag = Il2Cpp.SymbolSearch();
				}
				if (!flag)
				{
					ulong codeRegistration = Convert.ToUInt64(Console.ReadLine(), 16);
					ulong metadataRegistration = Convert.ToUInt64(Console.ReadLine(), 16);
					Il2Cpp.Init(codeRegistration, metadataRegistration);
				}
			}
			catch(Exception e)
			{
				Console.Error.WriteLine(e.ToString());
				throw new ApplicationException("ERROR: An error occurred while processing.");
			}

			Executor = new Il2CppExecutor2(Metadata, Il2Cpp);
		}
	}
}
