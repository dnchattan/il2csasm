namespace IL2CS.Generator.Cli
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			var asm = new AssemblyGenerator(new AssemblyGeneratorOptions {
				AssembyName = "Raid", 
				DllPath = @"C:\Users\PowerSpec\AppData\Local\Plarium\PlariumPlay\StandAloneApps\raid\247\GameAssembly.dll",
				MetadataPath = @"C:\Users\PowerSpec\AppData\Local\Plarium\PlariumPlay\StandAloneApps\raid\247\Raid_Data\il2cpp_data\Metadata\global-metadata.dat",
				IncludeImages = new string[] {
					"Unity.Plarium.Common.dll",
					"Unity.RaidApp.dll"
				}
			});
			asm.Generate();
		}
	}
}
