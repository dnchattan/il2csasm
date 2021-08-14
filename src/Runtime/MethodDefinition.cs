using IL2CS.Core;

namespace IL2CS.Runtime
{
	[Size(80)]
	public class MethodDefinition : StructBase
	{
		[Offset(24)]
		[Indirection(2)]
		public ClassDefinition klass;
	}
}
