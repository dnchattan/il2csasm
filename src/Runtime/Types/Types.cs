using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace IL2CS.Runtime.Types
{
	public static class Types
	{
		public static readonly Dictionary<string, Type> NativeMapping = new();
		static Types()
		{
			foreach (var (mapFrom, mapTo) in GetTypesWithHelpAttribute(typeof(Types).Assembly))
			{
				NativeMapping.Add(mapFrom.FullName, mapTo);
			}
		}
		static IEnumerable<(Type, Type)> GetTypesWithHelpAttribute(Assembly assembly)
		{
			foreach (Type type in assembly.GetTypes())
			{
				TypeMappingAttribute tma = type.GetCustomAttribute<TypeMappingAttribute>(true);
				if (tma != null)
				{
					yield return (tma.Type, type);
				}
			}
		}

		public static readonly Dictionary<Type, int> TypeSizes = new()
		{
			{typeof(void), 0},
			{typeof(bool), sizeof(bool)},
			{typeof(char), sizeof(char)},
			{typeof(sbyte), sizeof(sbyte)},
			{typeof(byte), sizeof(byte)},
			{typeof(short), sizeof(short)},
			{typeof(ushort), sizeof(ushort)},
			{typeof(int), sizeof(int)},
			{typeof(uint), sizeof(uint)},
			{typeof(long), sizeof(long)},
			{typeof(ulong), sizeof(ulong)},
			{typeof(float), sizeof(float)},
			{typeof(double), sizeof(double)},
			{typeof(string), 8},
			{typeof(IntPtr), 8},
			{typeof(UIntPtr), 8},
			{typeof(object), 8},
		};

	}
}
