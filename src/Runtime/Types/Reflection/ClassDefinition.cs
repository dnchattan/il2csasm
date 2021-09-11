﻿using IL2CS.Core;
using IL2CS.Runtime.Types.corelib;

namespace IL2CS.Runtime.Types.Reflection
{
	[Size(4376)]
	public struct ClassDefinition
	{
		[Offset(16)]
#pragma warning disable 649
		private Native__LPSTR m_name;
#pragma warning restore 649
		public string Name { get { return m_name.Value; } }


		[Offset(24)]
#pragma warning disable 649
		private Native__LPSTR m_namespace;
#pragma warning restore 649
		public string Namespace { get { return m_namespace.Value; } }

#pragma warning restore 649
		[field: Offset(184)] public UnknownClass StaticFields { get; }
	}
}
