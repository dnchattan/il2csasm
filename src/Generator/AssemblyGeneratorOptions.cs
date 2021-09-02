using System;
using IL2CS.Generator.TypeManagement;
using Microsoft.Extensions.Logging;

namespace IL2CS.Generator
{
	public class AssemblyGeneratorOptions
	{
		public string AssembyName;

		public string GameAssemblyPath;
		public string MetadataPath;

		public Func<TypeDescriptor, bool>[] TypeSelectors;

		public ILoggerFactory LogFactory;
	}
}
