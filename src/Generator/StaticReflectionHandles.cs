using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using IL2CS.Runtime;
using IL2CS.Runtime.Types.Reflection;

namespace IL2CS.Generator
{
	public class StaticReflectionHandles
	{
		public static class MethodDefinition
		{
			public static class Ctor
			{
				public static readonly System.Type[] Parameters = { typeof(ulong), typeof(string) };
				public static readonly ConstructorInfo ConstructorInfo = typeof(IL2CS.Runtime.Types.Reflection.MethodDefinition).GetConstructor(
					BindingFlags.Public | BindingFlags.Instance,
					null,
					Parameters,
					null);
			}
		}
		
		public static class Type
		{
			public static readonly MethodInfo GetTypeFromHandle = typeof(System.Type).GetMethod("GetTypeFromHandle");
			public static readonly MethodInfo op_Equality =
				typeof(System.Type).GetMethod("op_Equality", BindingFlags.Static | BindingFlags.Public);
		}

		public static class StructBase
		{
			public static readonly MethodInfo Load =
				typeof(IL2CS.Runtime.StructBase).GetMethod("Load", BindingFlags.NonPublic | BindingFlags.Instance);

			public static class Ctor
			{
				public static readonly System.Type[] Parameters = { typeof(Il2CsRuntimeContext), typeof(ulong) };
				public static readonly ConstructorInfo ConstructorInfo = typeof(IL2CS.Runtime.StructBase).GetConstructor(
					BindingFlags.NonPublic | BindingFlags.Instance,
					null,
					Parameters,
					null
					);
			}
		}

		public static class StaticInstance
		{
			public static class Ctor
			{
				public static System.Type[] Parameters = StructBase.Ctor.Parameters;
				public static readonly ConstructorInfo ConstructorInfo = typeof(IL2CS.Runtime.StaticInstance<>).GetConstructor(
					BindingFlags.NonPublic | BindingFlags.Instance,
					null,
					Parameters,
					null
					);
			}
		}
	}
}
