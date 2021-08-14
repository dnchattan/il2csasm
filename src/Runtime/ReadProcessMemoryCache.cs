using System;
using System.Collections.Generic;
using System.Linq;

namespace IL2CS.Runtime
{
	internal class MemoryCacheEntry
	{
		private readonly ReadOnlyMemory<byte> Data;
		private readonly ulong Address;
		private readonly ulong Size; // in bytes

		public MemoryCacheEntry(ulong address, ulong size, byte[] data)
		{
			Data = new ReadOnlyMemory<byte>(data);
			Address = address;
			Size = size;
		}

		public bool Contains(ulong address, ulong size)
		{
			if (address >= Address + Size)
			{
				return false;
			}
			if (address + size > Address + Size)
			{
				return false;
			}
			if (address < Address)
			{
				return false;
			}
			return true;
		}

		public ReadOnlyMemory<byte> ReadRange(ulong address, ulong size)
		{
			if (!Contains(address, size))
			{
				throw new ApplicationException("Requested memory is not contained within this range");
			}
			return Data.Slice((int)(address - Address), (int)size);
		}
	}
	internal class ReadProcessMemoryCache
	{
		private SortedDictionary<ulong, WeakReference<MemoryCacheEntry>> cache = new();

		public MemoryCacheEntry Store(ulong address, byte[] data)
		{
			MemoryCacheEntry entry = new(address, (ulong)data.Length, data);
			cache.Add(address, new(entry));
			return entry;
		}

		public ReadOnlyMemory<byte>? Find(ulong address, ulong size)
		{
			var allValues = cache.Values.Select(value => value.TryGetTarget(out MemoryCacheEntry target) ? target : null).ToArray();
			MemoryCacheEntry entry = allValues.FirstOrDefault(entry => entry != null && entry.Contains(address, size));
			
			// rebuild if anything expired
			if (allValues.Any(value => value == null))
			{
				RebuildCache();
			}
			if (entry == null)
			{
				return null;
			}
			return entry.ReadRange(address, size);
		}

		private void RebuildCache()
		{
			SortedDictionary<ulong, WeakReference<MemoryCacheEntry>> newCache = new();
			foreach (var kvp in cache)
			{
				if (kvp.Value.TryGetTarget(out var value))
				{
					newCache.Add(kvp.Key, kvp.Value);
				}
			}
			cache = newCache;
		}
	}
}
