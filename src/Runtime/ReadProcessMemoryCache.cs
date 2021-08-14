using System;
using System.Collections.Generic;
using System.Linq;

namespace IL2CS.Runtime
{
	internal class MemoryCacheEntry
	{
		private readonly ReadOnlyMemory<byte> Data;
		private readonly long Address;
		private readonly long Size; // in bytes

		public MemoryCacheEntry(long address, long size, byte[] data)
		{
			Data = new ReadOnlyMemory<byte>(data);
			Address = address;
			Size = size;
		}

		public bool Contains(long address, long size)
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

		public ReadOnlyMemory<byte> ReadRange(long address, long size)
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
		private SortedDictionary<long, WeakReference<MemoryCacheEntry>> cache = new();

		public MemoryCacheEntry Store(long address, byte[] data)
		{
			MemoryCacheEntry entry = new(address, data.Length, data);
			cache.Add(address, new(entry));
			return entry;
		}

		public ReadOnlyMemory<byte>? Find(long address, long size)
		{
			MemoryCacheEntry entry = FindEntry(address, size);

			// rebuild if anything expired
			MemoryCacheEntry[] allValues = cache.Values.Select(value => value.TryGetTarget(out MemoryCacheEntry target) ? target : null).ToArray();
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

		public MemoryCacheEntry FindEntry(long address, long size)
		{
			MemoryCacheEntry[] allValues = cache.Values.Select(value => value.TryGetTarget(out MemoryCacheEntry target) ? target : null).ToArray();
			MemoryCacheEntry entry = allValues.FirstOrDefault(entry => entry != null && entry.Contains(address, size));

			// rebuild if anything expired
			if (allValues.Any(value => value == null))
			{
				RebuildCache();
			}
			return entry;
		}

		private void RebuildCache()
		{
			SortedDictionary<long, WeakReference<MemoryCacheEntry>> newCache = new();
			foreach (KeyValuePair<long, WeakReference<MemoryCacheEntry>> kvp in cache)
			{
				if (kvp.Value.TryGetTarget(out MemoryCacheEntry value))
				{
					newCache.Add(kvp.Key, kvp.Value);
				}
			}
			cache = newCache;
		}
	}
}
