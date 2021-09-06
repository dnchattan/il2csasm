using IL2CS.Core;

namespace IL2CS.Runtime
{
	[Size(4376)]
	public class ClassDefinition : StructBase
	{
		public ClassDefinition(Il2CsRuntimeContext context, long address) : base(context, address)
		{
		}

		[Offset(16)]
#pragma warning disable 649
		private string _name;
#pragma warning restore 649
		public string Name { get { Load(); return _name; } }


		[Offset(24)]
#pragma warning disable 649
		private string _Namespace;
#pragma warning restore 649
		public string Namespace { get { Load(); return _Namespace; } }

		[Offset(184)]
		[Indirection(2)]
#pragma warning disable 649
		private UnknownClass _StaticFields;
#pragma warning restore 649
		public UnknownClass StaticFields { get { Load(); return _StaticFields; } }
	}
}
