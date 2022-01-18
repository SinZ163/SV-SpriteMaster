﻿using SpriteMaster.Extensions;
using SpriteMaster.Types;
using System;
using System.Runtime.InteropServices;

namespace SpriteMaster.Resample.Passes;

static class ExtractSprite {
	internal static Span<Color8> Extract(ReadOnlySpan<Color8> data, in Bounds textureBounds, in Bounds spriteBounds, int stride, int block, out Vector2I newExtent) {
		//if ((bounds.Width % block) != 0 || (bounds.Height % block) != 0) {
		//	throw new ArgumentOutOfRangeException($"Bounds {bounds} are not multiples of block {block}");
		//}

		if (block == 1) {
			return Extract(data, textureBounds, spriteBounds, stride, out newExtent);
		}

		if (!block.IsPow2()) {
			throw new ArgumentException($"Block size {block} is not a power-of-two");
		}

		Bounds bounds = new Bounds(
			spriteBounds.Offset & ~(block - 1), // 'block' is a power-of-two
			(spriteBounds.Extent / block).Max((1, 1))
		);

		var result = SpanExt.MakeUninitialized<Color8>(bounds.Area);

		int startOffset = (bounds.Offset.Y * stride) + bounds.Offset.X;
		int outOffset = 0;

		for (int y = 0; y < bounds.Extent.Height; ++y) {
			int offset = startOffset + ((y * block) * stride);
			for (int x = 0; x < bounds.Extent.Width; ++x) {
				result[outOffset++] = data[offset + (x * block)];
			}
		}

		newExtent = bounds.Extent;
		return result;
	}

	internal static Span<Color8> Extract(ReadOnlySpan<Color8> data, in Bounds textureBounds, in Bounds inBounds, int stride, out Vector2I newExtent) {
		if (inBounds == textureBounds) {
			newExtent = inBounds.Extent;
			return data.ToSpanUnsafe();
		}
		else {
			var resultData = SpanExt.MakeUninitialized<Color8>(inBounds.Area);
			int sourceOffset = (textureBounds.Width * inBounds.Top) + inBounds.Left;
			int destOffset = 0;
			for (int y = 0; y < inBounds.Height; ++y) {
				data.Slice(sourceOffset, inBounds.Width).CopyTo(resultData.Slice(destOffset, inBounds.Width));
				destOffset += inBounds.Width;
				sourceOffset += textureBounds.Width;
			}
			newExtent = inBounds.Extent;
			return resultData;
		}
	}
}
