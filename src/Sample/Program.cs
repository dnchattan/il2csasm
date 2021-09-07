using System;
using System.Diagnostics;
using System.Linq;
using IL2CS.Core;
using IL2CS.Runtime;
using IL2CS.Runtime.Types.Reflection;

namespace examples
{
	internal class Program
	{
		private static void Main(string[] args)
		{
			Process raidProc = GetRaidProcess();
			Il2CsRuntimeContext runtime = new(raidProc);
			var statics = Client.App.SingleInstance<Client.Model.AppModel>.method_get_Instance.Get(runtime).DeclaringClass.StaticFields
				.As<AppModelStaticFields>();
			Client.Model.AppModel appModel = statics.Instance;
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
		public AppModelStaticFields(Il2CsRuntimeContext context, long address) : base(context, address)
		{
		}

		[Offset(8)]
		[Indirection(2)]
		private Client.Model.AppModel _Instance;

		public Client.Model.AppModel Instance
		{
			get
			{
				Load();
				return _Instance;
			}
		}
	}

	[Static]
	public class AppModelStatics : StructBase
	{
		public AppModelStatics(Il2CsRuntimeContext context, long address) : base(context, address)
		{
		}

		[Address(58725120, "GameAssembly.dll")]
		[Indirection(2)]
		private NativeMethodInfo _GetInstance;

		public NativeMethodInfo GetInstance
		{
			get
			{
				Load();
				return _GetInstance;
			}
		}
	}
}