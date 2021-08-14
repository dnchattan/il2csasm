using IL2CS.Core;
using IL2CS.Runtime;
using System;
using System.Diagnostics;
using System.Linq;

namespace examples
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Process raidProc = GetRaidProcess();
			Il2CsRuntimeContext runtime = new Il2CsRuntimeContext(raidProc);
			AppModelStaticFields statics = runtime.ReadStruct<AppModel.Statics>().GetInstance.klass.StaticFields.As<AppModelStaticFields>();
			AppModel appModel = statics.Instance;
			Console.WriteLine(appModel.UserId); // avoid compile error by dumping this out
		}
		private static Process GetRaidProcess()
		{
			Process process = Process.GetProcessesByName("Raid").FirstOrDefault();
			if (process == null)
			{
				throw new Exception("Raid needs to be running before running RaidExtractor");
			}

			return process;
		}
	}
	[Size(16)]
	public class AppModelStaticFields : StructBase
	{
		[Offset(8)]
		[Indirection(2)]
		public AppModel Instance;
	}

	[Size(512 + 8)]
	public class AppModel : StructBase
	{
		[Static]
		public class Statics : StaticStructBase
		{
			[Address(58242656, "GameAssembly.dll")]
			[Indirection(2)]
			public MethodDefinition GetInstance;
		}
		[Offset(352)]
		public long UserId;
	}
}