using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace IL2CS.Generator.TypeManagement
{
	public class FieldDescriptor
	{
		private static readonly Regex BackingFieldRegex = new("<(.+)>k__BackingField", RegexOptions.Compiled);

		public FieldDescriptor(string name, TypeReference typeReference, FieldAttributes attrs, int offset)
		{
			Name = BackingFieldRegex.Replace(name, match => match.Groups[1].Value);
			// this is kinda evil, but it will make them consistent in name at least =)
			StorageName = $"_{Name}_BackingField";
			Type = typeReference;
			Attributes = attrs;
			Offset = offset;
		}

		public readonly string StorageName;
		public readonly string Name;
		public readonly TypeReference Type;
		public FieldAttributes Attributes;
		public readonly int Offset;
		public object DefaultValue;
	}
}
