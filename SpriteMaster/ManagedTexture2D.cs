﻿using Microsoft.Xna.Framework.Graphics;
using System;
using SpriteMaster.Types;
using SpriteMaster.Extensions;
using TeximpNet.Compression;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace SpriteMaster {
	internal sealed class ManagedTexture2D : Texture2D {
		private static long TotalAllocatedSize = 0L;
		private static int TotalManagedTextures = 0;
		private const bool UseMips = false;

		public readonly WeakReference<Texture2D> Reference;
		public readonly ScaledTexture Texture;
		public readonly Vector2I Dimensions;

		internal static void DumpStats(List<string> output) {
			output.Add("\tManagedTexture2D:");
			output.Add($"\t\tTotal Managed Textures : {TotalManagedTextures}");
			output.Add($"\t\tTotal Texture Size     : {TotalAllocatedSize.AsDataSize()}");
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public ManagedTexture2D (
			ScaledTexture texture,
			Texture2D reference,
			Vector2I dimensions,
			SurfaceFormat format,
			string name = null
		) : base(reference.GraphicsDevice.IsDisposed ? DrawState.Device : reference.GraphicsDevice, dimensions.Width, dimensions.Height, UseMips, format) {
			this.Name = name ?? $"{reference.SafeName()} [RESAMPLED {(CompressionFormat)format}]";

			Reference = reference.MakeWeak();
			Texture = texture;
			Dimensions = dimensions - texture.BlockPadding;

			reference.Disposing += (_, _1) => OnParentDispose();

			TotalAllocatedSize += this.SizeBytes();
			++TotalManagedTextures;

			Garbage.MarkOwned(format, dimensions.Area);
			Disposing += (_, _1) => {
				Garbage.UnmarkOwned(format, dimensions.Area);
				TotalAllocatedSize -= this.SizeBytes();
				--TotalManagedTextures;
			};
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		~ManagedTexture2D() {
			if (!IsDisposed) {
				Dispose(false);
			}
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		private void OnParentDispose() {
			if (!IsDisposed) {
				Debug.TraceLn($"Disposing ManagedTexture2D '{Name}'");
				Dispose();
			}
		}
	}
}
