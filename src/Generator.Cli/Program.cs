using System.Collections.Generic;
using System.Linq;
using CommandLine;

namespace IL2CS.Generator.Cli
{
	internal class Program
	{
		public class Options
		{
			[Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
			public bool Verbose { get; set; }

			[Option('n', "name", Required = true, HelpText = "Output assembly name")]
			public string AssemblyName { get; set; }

			[Option('g', "game-assembly", Required = true, HelpText = "Path to GameAssembly.dll")]
			public string GameAssemblyPath { get; set; }

			[Option('m', "metadata", Required = true, HelpText = "Path to global-metadata.dat")]
			public string MetadataPath { get; set; }

			[Option('i', "include", Required = false, Separator = ',', HelpText = "Types to include")]
			public IEnumerable<string> IncludeTypes { get; set; }

			[Option('o', "out-path", Required = true, HelpText = "Output file path")]
			public string OutputPath { get; set; }

		}

		private static void Main(string[] args)
		{
			Parser.Default.ParseArguments<Options>(args)
			.WithParsed(o =>
			{
				using (LoggingScope scope = new())
				{
					AssemblyGenerator asm = new(new AssemblyGeneratorOptions
					{
						LogFactory = scope.Factory,
						AssembyName = o.AssemblyName,
						GameAssemblyPath = o.GameAssemblyPath, // @"C:\Users\PowerSpec\AppData\Local\Plarium\PlariumPlay\StandAloneApps\raid\247\GameAssembly.dll",
						MetadataPath = o.MetadataPath, // @"C:\Users\PowerSpec\AppData\Local\Plarium\PlariumPlay\StandAloneApps\raid\247\Raid_Data\il2cpp_data\Metadata\global-metadata.dat",
						TypeSelectors = new System.Func<TypeManagement.TypeDescriptor, bool>[]
						{
							td => o.IncludeTypes == null || o.IncludeTypes.Contains(td.Name),
							//td => td.Name.StartsWith("System.Collections")
							//td => td.Name == "Plarium.GS.Model.Modules.GlobalRatings.Entities.GlobalRatingStatusIds"
							// td => td.Name == "Client.Model.AppModel"
							//td => td.Name == "Client.App.Configuration.BuildConfiguration",
							//td => td.Name == "Client.Model.Settings.ConfigManager"
						}
					});
					asm.Generate(o.OutputPath);
				}
			});
		}
	}
}
