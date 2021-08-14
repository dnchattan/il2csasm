using IL2CS.Core;

namespace IL2CS.Runtime
{
	[Size(4376)]
	public class ClassDefinition : StructBase
	{
		[Offset(16)]
		public string Name;

		[Offset(24)]
		public string Namespace;

		[Offset(184)]
		[Indirection(2)]
		public UnknownClass StaticFields;
	}
}
