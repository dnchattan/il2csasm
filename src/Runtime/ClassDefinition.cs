using il2cs.Assembly;

namespace Runtime
{
	[Size(4376)]
	public class ClassDefinition : StructBase
	{
		[Offset(16)]
		public string Name;

		[Offset(24)]
		public string Namespace;
	}
}
