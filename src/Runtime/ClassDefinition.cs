using IL2CS.Core;

namespace IL2CS.Runtime
{
	[Size(4376)]
	public class ClassDefinition : StructBase
	{
		[Offset(16)]
		private string _Name;
		public string Name { get { Load(); return _Name; } }


		[Offset(24)]
		private string _Namespace;
		public string Namespace { get { Load(); return _Namespace; } }

		[Offset(184)]
		[Indirection(2)]
		private UnknownClass _StaticFields;
		public UnknownClass StaticFields { get { Load(); return _StaticFields; } }

	}
}
