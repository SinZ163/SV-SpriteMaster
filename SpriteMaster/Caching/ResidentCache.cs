﻿using SpriteMaster.Configuration;
using SpriteMaster.Types.MemoryCache;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;

namespace SpriteMaster.Caching;

/// <summary>
/// Used to cache original texture data so it doesn't need to perform blocking fetches as often
/// </summary>
internal static class ResidentCache {
	internal static bool Enabled => Config.ResidentCache.Enabled;

	private static readonly IMemoryCache<ulong, byte> Cache = CreateCache();

	internal static long Size => Cache.SizeBytes;

	private static IMemoryCache<ulong, byte> CreateCache() => AbstractMemoryCache<ulong, byte>.Create(
		name: "ResidentCache",
		maxSize: Config.ResidentCache.MaxSize,
		compressed: true
	);

	[MethodImpl(Runtime.MethodImpl.Inline)]
	internal static byte[]? Get(ulong key) =>
		Cache.Get(key);

	[MethodImpl(Runtime.MethodImpl.Inline)]
	internal static bool TryGet(ulong key, [NotNullWhen(true)] out byte[]? value) =>
		Cache.TryGet(key, out value);

	internal static byte[] Set(ulong key, byte[] value) =>
		Cache.Set(key, value);

	[MethodImpl(Runtime.MethodImpl.Inline)]
	internal static byte[]? Remove(ulong key) =>
		Cache.Remove(key);

	[MethodImpl(Runtime.MethodImpl.Inline)]
	internal static void RemoveFast(ulong key) =>
		Cache.RemoveFast(key);

	[MethodImpl(Runtime.MethodImpl.Inline)]
	internal static void Purge() {
		Cache.Clear();
	}

	[MethodImpl(Runtime.MethodImpl.Inline)]
	internal static void OnSettingsChanged() {
		if (!Enabled) {
			Purge();
		}
	}
}
