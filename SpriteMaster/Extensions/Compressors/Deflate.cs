﻿using Ionic.Zlib;
using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

namespace SpriteMaster.Extensions.Compressors {
	internal static class Deflate {
		internal static bool IsSupported {
			get {
				try {
					var dummyData = new byte[16];
					var compressedData = Compress(dummyData);
					var uncompressedData = Decompress(compressedData, dummyData.Length);
					if (!Enumerable.SequenceEqual(dummyData, uncompressedData)) {
						throw new Exception("Original and Uncompressed Data Mismatch");
					}
					return true;
				}
				catch (Exception ex) {
					Debug.InfoLn($"Deflate Compression not supported: '{ex.ToString()}'");
					return false;
				}
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static int CompressedLengthEstimate(byte[] data) {
			return data.Length >> 1;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static byte[] Compress(byte[] data) {
			using var val = new MemoryStream(CompressedLengthEstimate(data));
			using (var compressor = new DeflateStream(val, CompressionMode.Compress, CompressionLevel.BestCompression)) {
				compressor.Strategy = CompressionStrategy.Filtered;
				compressor.Write(data, 0, data.Length);
			}
			return val.ToArray();
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static byte[] Decompress(byte[] data) {
			return DeflateStream.UncompressBuffer(data);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		internal static byte[] Decompress(byte[] data, int size) {
			using var dataStream = new MemoryStream(data);
			var output = new byte[size];
			using (var val = new MemoryStream(output)) {
				using var compressor = new DeflateStream(dataStream, CompressionMode.Decompress);
				compressor.CopyTo(val);
			}
			return output;
		}
	}
}
