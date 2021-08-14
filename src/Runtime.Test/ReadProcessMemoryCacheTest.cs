using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace IL2CS.Runtime.Test
{
	[TestClass]
	public class ReadProcessMemoryCacheTest
	{
		[TestMethod]
		public void EmptyCache()
		{
			ReadProcessMemoryCache cache = new();
			ReadOnlyMemory<byte>? entry = cache.Find(64, 8);
			Assert.IsNull(entry);
		}
		[TestMethod]
		public void PartiallyCachedBefore()
		{
			ReadProcessMemoryCache cache = new();
			cache.Store(60, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
			ReadOnlyMemory<byte>? entry = cache.Find(64, 8);
			Assert.IsNull(entry);
		}
		[TestMethod]
		public void PartiallyCachedAfter()
		{
			ReadProcessMemoryCache cache = new();
			cache.Store(68, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
			ReadOnlyMemory<byte>? entry = cache.Find(64, 8);
			Assert.IsNull(entry);
		}
		[TestMethod]
		public void CacheMiss()
		{
			ReadProcessMemoryCache cache = new();
			cache.Store(56, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
			cache.Store(72, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
			ReadOnlyMemory<byte>? entry = cache.Find(64, 8);
			Assert.IsNull(entry);
		}
		[TestMethod]
		public void CacheHitExtraBefore()
		{
			ReadProcessMemoryCache cache = new();
			cache.Store(60, new byte[] { 9, 9, 9, 9, 0, 1, 2, 3, 4, 5, 6, 7 });
			ReadOnlyMemory<byte>? memory = cache.Find(64, 8);
			Assert.IsNotNull(memory);
			Assert.AreEqual(8, memory?.Length);
		}
		[TestMethod]
		public void CacheHitExtraAfter()
		{
			ReadProcessMemoryCache cache = new();
			cache.Store(64, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 9, 9, 9, 9, });
			ReadOnlyMemory<byte>? memory = cache.Find(64, 8);
			Assert.IsNotNull(memory);
			Assert.AreEqual(8, memory?.Length);
		}
		[TestMethod]
		public void CacheHitCollected()
		{
			ReadProcessMemoryCache cache = new();
			// must be done in a separate function to ensure GC can clean up the reference in debug mode
			StoreData(cache);
			GC.Collect();
			ReadOnlyMemory<byte>? memory = cache.Find(64, 8);
			Assert.IsNull(memory);
		}

		private static void StoreData(ReadProcessMemoryCache cache)
		{
			cache.Store(64, new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 });
		}
	}
}
