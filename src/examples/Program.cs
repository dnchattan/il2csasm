using il2cs.Assembly;
using Runtime;
using System;
using System.Diagnostics;
using System.Linq;

namespace examples
{
	class Program
	{
		static void Main(string[] args)
		{
            var raidProc = GetRaidProcess();
            var runtime = new Il2CsRuntimeContext(raidProc);
            var statics = runtime.ReadStruct<AppModel.Statics>().GetStaticFields<AppModelStaticFields>();
            var appModel = statics.Instance;
            Console.WriteLine(appModel.UserId); // avoid compile error by dumping this out
		}
		static private Process GetRaidProcess()
        {
            var process = Process.GetProcessesByName("Raid").FirstOrDefault();
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
        public class Statics : StructBase
        {
            [Address(58242656)]
            public MethodDefinition GetInstance;
        }
		[Offset(352)]
        public long UserId;
    }
}
