using IL2CS.Core;
using IL2CS.Runtime.Types.corelib;

namespace IL2CS.Runtime.Types.Reflection
{
	[Size(4376)]
	public class ClassDefinition : StructBase
	{
		public ClassDefinition(Il2CsRuntimeContext context, ulong address) : base(context, address)
		{
		}

		[Offset(16)]
#pragma warning disable 649
		private Native__LPSTR m_name;
#pragma warning restore 649
		public string Name { get { Load(); return m_name.Value; } }


		[Offset(24)]
#pragma warning disable 649
		private Native__LPSTR m_namespace;
#pragma warning restore 649
		public string Namespace { get { Load(); return m_namespace.Value; } }

		[Offset(184)]
		[Indirection(2)]
#pragma warning disable 649
		private UnknownClass m_staticFields;
#pragma warning restore 649
		public UnknownClass StaticFields { get { Load(); return m_staticFields; } }
	}
}
