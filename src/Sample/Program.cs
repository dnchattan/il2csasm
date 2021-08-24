using System;
using System.Diagnostics;
using System.Linq;
using IL2CS.Core;
using IL2CS.Runtime;

namespace examples
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Process raidProc = GetRaidProcess();
			Il2CsRuntimeContext runtime = new(raidProc);
			AppModelStaticFields statics = runtime.ReadStruct<AppModelStatics>().GetInstance.klass.StaticFields.As<AppModelStaticFields>();
			//Client.Model.AppModel appModel = statics.Instance;
			
			//Console.WriteLine(appModel.UserId); // avoid compile error by dumping this out
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
		//[Offset(8)]
		//[Indirection(2)]
		//public Client.Model.AppModel Instance;
	}

	[Static]
	public class AppModelStatics : StaticStructBase
	{
			[Address(58242656, "GameAssembly.dll")]
			[Indirection(2)]
			public MethodDefinition GetInstance;
	}
}