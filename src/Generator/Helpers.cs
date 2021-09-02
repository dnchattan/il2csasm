using System.Diagnostics;
using System.Reflection;
using Il2CppDumper;
using static Il2CppDumper.Il2CppConstants;

namespace IL2CS.Generator
{
	internal static class Helpers
	{
		public static void Assert(bool condition, string message)
		{
			if (condition)
			{
				return;
			}
			Trace.WriteLine($"Assertion failed: {message}");
			if (Debugger.IsAttached)
			{
				Debugger.Break();
			}
		}
		public static TypeAttributes GetTypeAttributes(Il2CppTypeDefinition typeDef)
		{
			//return (TypeAttributes)typeDef.flags;
			TypeAttributes attrs = default(TypeAttributes);
			var visibility = typeDef.flags & TYPE_ATTRIBUTE_VISIBILITY_MASK;
			switch (visibility)
			{
				case TYPE_ATTRIBUTE_PUBLIC:
					attrs |= TypeAttributes.Public;
					break;
				case TYPE_ATTRIBUTE_NESTED_PUBLIC:
					attrs |= TypeAttributes.NestedPublic;
					break;
				case TYPE_ATTRIBUTE_NOT_PUBLIC:
					attrs |= TypeAttributes.NotPublic;
					break;
				case TYPE_ATTRIBUTE_NESTED_FAM_AND_ASSEM:
					attrs |= TypeAttributes.NestedFamANDAssem;
					break;
				case TYPE_ATTRIBUTE_NESTED_ASSEMBLY:
					attrs |= TypeAttributes.NestedAssembly;
					break;
				case TYPE_ATTRIBUTE_NESTED_PRIVATE:
					attrs |= TypeAttributes.NestedPrivate;
					break;
				case TYPE_ATTRIBUTE_NESTED_FAMILY:
					attrs |= TypeAttributes.NestedFamily;
					break;
				case TYPE_ATTRIBUTE_NESTED_FAM_OR_ASSEM:
					attrs |= TypeAttributes.NestedFamORAssem;
					break;
			}
			if ((typeDef.flags & TYPE_ATTRIBUTE_ABSTRACT) != 0 && (typeDef.flags & TYPE_ATTRIBUTE_SEALED) != 0)
				attrs |= TypeAttributes.NotPublic;
			else if ((typeDef.flags & TYPE_ATTRIBUTE_INTERFACE) == 0 && (typeDef.flags & TYPE_ATTRIBUTE_ABSTRACT) != 0)
				attrs |= TypeAttributes.Abstract;
			else if (!typeDef.IsValueType && !typeDef.IsEnum && (typeDef.flags & TYPE_ATTRIBUTE_SEALED) != 0)
				attrs |= TypeAttributes.Sealed;
			if ((typeDef.flags & TYPE_ATTRIBUTE_INTERFACE) != 0)
				attrs |= TypeAttributes.Interface | TypeAttributes.Abstract;
			return attrs;
		}

	}
}
