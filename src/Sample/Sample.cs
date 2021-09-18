using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Client.Model.Gameplay.Heroes;
using Client.Model.Gameplay.Heroes.Data;
using Client.Model.Guard;
using IL2CS.Core;
using IL2CS.Runtime;
using SharedModel.Meta.Heroes;
using SharedModel.Meta.Heroes.Dtos;

namespace examples
{
	enum A
	{
		A,
		B
	}
	internal class Sample
	{
		private static void Main(string[] args)
		{
			Process raidProc = GetRaidProcess();
			Il2CsRuntimeContext runtime = new(raidProc);
			var statics = Client.App.SingleInstance<Client.Model.AppModel>.method_get_Instance.GetMethodInfo(runtime).DeclaringClass.StaticFields
				.As<AppModelStaticFields>();
			Client.Model.AppModel appModel = statics.Instance;
			Console.WriteLine($"UserId: {appModel.UserId}"); // avoid compile error by dumping this out
		
			UserWrapper userWrapper = appModel._userWrapper;
			HeroesWrapper heroes = userWrapper.Heroes;
			foreach (SetHeroMarkerDto marker in heroes._markers)
			{
				Enum.GetName(marker.Marker);
			}
			UpdatableHeroData heroData = heroes.HeroData;
			IReadOnlyDictionary<int, Hero> heroById = heroData.HeroById;
			foreach ((int key, Hero value) in heroById)
			{
				Console.WriteLine($"{key}: {value._type?.Name.DefaultValue ?? "??"}");
				foreach (var skill in value.Skills)
				{
					Console.WriteLine($"\tSkill {skill.Id}: {skill.TypeId}");
				}
			}
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
	public struct AppModelStaticFields
	{
		[Offset(8)]
		public Client.Model.AppModel Instance;
	}
}