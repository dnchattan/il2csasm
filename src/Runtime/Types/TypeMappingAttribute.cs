﻿using System;

namespace IL2CS.Runtime.Types
{
	[AttributeUsage(AttributeTargets.Class, Inherited = true, AllowMultiple = false)]
	public class TypeMappingAttribute : Attribute
	{
		public Type Type { get; private set; }
		public TypeMappingAttribute(Type offset)
		{
			Type = offset;
		}
	}
}
