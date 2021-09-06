using IL2CS.Core;

namespace IL2CS.Runtime
{
	[Size(80)]
	public class MethodDefinition : StructBase
	{
		public MethodDefinition(Il2CsRuntimeContext context, long address) : base(context, address)
		{
		}

		[Offset(24)]
		[Indirection(2)]
#pragma warning disable 649
		private ClassDefinition _klass;
#pragma warning restore 649

		// ReSharper disable once InconsistentNaming
		public ClassDefinition klass { get { Load(); return _klass; } }

	}
}
