﻿using Microsoft.Extensions.Logging;

namespace IL2CS.Generator
{
	public class AssemblyGeneratorOptions
	{
		public string AssembyName;

		public string GameAssemblyPath;
		public string MetadataPath;

		public string[] IncludeImages;

		public ILoggerFactory LogFactory;
	}
}
