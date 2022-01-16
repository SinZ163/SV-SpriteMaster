﻿using SpriteMaster.Extensions;
using SpriteMaster.Types;
using SpriteMaster.xBRZ.Blend;
using SpriteMaster.xBRZ.Color;
using SpriteMaster.xBRZ.Common;
using SpriteMaster.xBRZ.Scalers;
using System;
using System.Diagnostics.Contracts;
using System.Runtime.CompilerServices;

#nullable enable

namespace SpriteMaster.xBRZ;

using PreprocessType = Byte;

sealed class Scaler {
	internal const uint MinScale = 2;
	internal const uint MaxScale = Config.MaxScale;

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static Span<Color16> Apply(
		in Config configuration,
		uint scaleMultiplier,
		ReadOnlySpan<Color16> sourceData,
		Vector2I sourceSize,
		Vector2I targetSize,
		Span<Color16> targetData = default
	) {
		if (sourceSize.X * sourceSize.Y > sourceData.Length) {
			throw new ArgumentOutOfRangeException(nameof(sourceData));
		}

		var targetSizeCalculated = sourceSize * scaleMultiplier;
		if (targetSize != targetSizeCalculated) {
			throw new ArgumentOutOfRangeException(nameof(targetSize));
		}

		if (targetData == Span<Color16>.Empty) {
			targetData = SpanExt.MakeUninitialized<Color16>(targetSize.Area);
		}
		else {
			if (targetSize.Area > targetData.Length) {
				throw new ArgumentOutOfRangeException(nameof(targetData));
			}
		}

		var scalerInstance = new Scaler(
			configuration: in configuration,
			scaleMultiplier: scaleMultiplier,
			sourceSize: sourceSize,
			targetSize: targetSize
		);

		scalerInstance.Scale(sourceData, targetData);
		return targetData;
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	private Scaler(
		in Config configuration,
		uint scaleMultiplier,
		Vector2I sourceSize,
		Vector2I targetSize
	) {
		if (scaleMultiplier < MinScale || scaleMultiplier > MaxScale) {
			throw new ArgumentOutOfRangeException(nameof(scaleMultiplier));
		}
		/*
		if (sourceData == null) {
			throw new ArgumentNullException(nameof(sourceData));
		}
		if (targetData == null) {
			throw new ArgumentNullException(nameof(targetData));
		}
		*/
		if (sourceSize.X <= 0 || sourceSize.Y <= 0) {
			throw new ArgumentOutOfRangeException(nameof(sourceSize));
		}

		this.ScalerInterface = scaleMultiplier.ToIScaler(configuration);
		this.Configuration = configuration;
		this.ColorDistance = new(this.Configuration);
		this.ColorEqualizer = new(this.Configuration);
		this.SourceSize = sourceSize;
		this.TargetSize = targetSize;
	}

	private readonly Config Configuration;
	private readonly IScaler ScalerInterface;

	private readonly ColorDist ColorDistance;
	private readonly ColorEq ColorEqualizer;

	private readonly Vector2I SourceSize;
	private readonly Vector2I TargetSize;

	//fill block with the given color
	[MethodImpl(Runtime.MethodImpl.Hot)]
	private static void FillBlock(Span<Color16> trg, int targetOffset, int pitch, Color16 col, int blockSize, int targetWidth) {
		for (var y = 0; y < blockSize; ++y, targetOffset += pitch) {
			trg.Slice(targetOffset, blockSize).Fill(col);
		}
	}

	//detect blend direction
	[Pure]
	[MethodImpl(Runtime.MethodImpl.Hot)]
	private BlendResult PreProcessCorners(in Kernel4x4 ker) {
		var result = new BlendResult();

		if ((ker.F == ker.G && ker.J == ker.K) || (ker.F == ker.J && ker.G == ker.K)) {
			return result;
		}

		var dist = ColorDistance;

		var jg = dist.DistYCbCr(ker.I, ker.F) + dist.DistYCbCr(ker.F, ker.C) + dist.DistYCbCr(ker.N, ker.K) + dist.DistYCbCr(ker.K, ker.H) + Configuration.CenterDirectionBias * dist.DistYCbCr(ker.J, ker.G);
		var fk = dist.DistYCbCr(ker.E, ker.J) + dist.DistYCbCr(ker.J, ker.O) + dist.DistYCbCr(ker.B, ker.G) + dist.DistYCbCr(ker.G, ker.L) + Configuration.CenterDirectionBias * dist.DistYCbCr(ker.F, ker.K);

		if (jg < fk) {
			var dominantGradient = (Configuration.DominantDirectionThreshold * jg < fk) ? BlendType.Dominant : BlendType.Normal;
			if (ker.F != ker.G && ker.F != ker.J) {
				result.F = dominantGradient;
			}
			if (ker.K != ker.J && ker.K != ker.G) {
				result.K = dominantGradient;
			}
		}
		else if (fk < jg) {
			var dominantGradient = (Configuration.DominantDirectionThreshold * fk < jg) ? BlendType.Dominant : BlendType.Normal;
			if (ker.J != ker.F && ker.J != ker.K) {
				result.J = dominantGradient;
			}
			if (ker.G != ker.F && ker.G != ker.K) {
				result.G = dominantGradient;
			}
		}

		return result;
	}

	/*
			input kernel area naming convention:
			-------------
			| A | B | C |
			----|---|---|
			| D | E | F | //input pixel is at position E
			----|---|---|
			| G | H | I |
			-------------
			blendInfo: result of preprocessing all four corners of pixel "e"
	*/
	[MethodImpl(Runtime.MethodImpl.Hot)]
	private void ScalePixel(IScaler scaler, RotationDegree rotDeg, in Kernel3x3 ker, ref OutputMatrix outputMatrix, int targetIndex, PreprocessType blendInfo) {
		var blend = blendInfo.Rotate(rotDeg);

		if (blend.GetBottomR() == BlendType.None) {
			return;
		}

		// int a = ker._[Rot._[(0 << 2) + rotDeg]];
		var b = ker[Rotator.Get((1 << 2) + (int)rotDeg)];
		var c = ker[Rotator.Get((2 << 2) + (int)rotDeg)];
		var d = ker[Rotator.Get((3 << 2) + (int)rotDeg)];
		var e = ker[Rotator.Get((4 << 2) + (int)rotDeg)];
		var f = ker[Rotator.Get((5 << 2) + (int)rotDeg)];
		var g = ker[Rotator.Get((6 << 2) + (int)rotDeg)];
		var h = ker[Rotator.Get((7 << 2) + (int)rotDeg)];
		var i = ker[Rotator.Get((8 << 2) + (int)rotDeg)];

		var eq = ColorEqualizer;
		var dist = ColorDistance;

		bool doLineBlend;

		if (blend.GetBottomR() >= BlendType.Dominant) {
			doLineBlend = true;
		}
		//make sure there is no second blending in an adjacent
		//rotation for this pixel: handles insular pixels, mario eyes
		//but support double-blending for 90� corners
		else if (blend.GetTopR() != BlendType.None && !eq.IsColorEqual(e, g)) {
			doLineBlend = false;
		}
		else if (blend.GetBottomL() != BlendType.None && !eq.IsColorEqual(e, c)) {
			doLineBlend = false;
		}
		//no full blending for L-shapes; blend corner only (handles "mario mushroom eyes")
		else if (eq.IsColorEqual(g, h) && eq.IsColorEqual(h, i) && eq.IsColorEqual(i, f) && eq.IsColorEqual(f, c) && !eq.IsColorEqual(e, i)) {
			doLineBlend = false;
		}
		else {
			doLineBlend = true;
		}

		//choose most similar color
		var px = dist.DistYCbCr(e, f) <= dist.DistYCbCr(e, h) ? f : h;

		outputMatrix.Move(rotDeg, targetIndex);

		if (!doLineBlend) {
			scaler.BlendCorner(px, ref outputMatrix);
			return;
		}

		//test sample: 70% of values max(fg, hc) / min(fg, hc)
		//are between 1.1 and 3.7 with median being 1.9
		var fg = dist.DistYCbCr(f, g);
		var hc = dist.DistYCbCr(h, c);

		var haveShallowLine = Configuration.SteepDirectionThreshold * fg <= hc && e != g && d != g;
		var haveSteepLine = Configuration.SteepDirectionThreshold * hc <= fg && e != c && b != c;

		if (haveShallowLine) {
			if (haveSteepLine) {
				scaler.BlendLineSteepAndShallow(px, ref outputMatrix);
			}
			else {
				scaler.BlendLineShallow(px, ref outputMatrix);
			}
		}
		else {
			if (haveSteepLine) {
				scaler.BlendLineSteep(px, ref outputMatrix);
			}
			else {
				scaler.BlendLineDiagonal(px, ref outputMatrix);
			}
		}
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	private int getX(int x) {
		if (Configuration.Wrapped.X) {
			x = (x + SourceSize.Width) % SourceSize.Width;
		}
		else {
			x = Math.Clamp(x, 0, SourceSize.Width - 1);
		}
		return x;
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	private int getY(int y) {
		if (Configuration.Wrapped.Y) {
			y = (y + SourceSize.Height) % SourceSize.Height;
		}
		else {
			y = Math.Clamp(y, 0, SourceSize.Height - 1);
		}
		return y;
	}

	//scaler policy: see "Scaler2x" reference implementation
	[MethodImpl(Runtime.MethodImpl.Hot)]
	private void Scale(ReadOnlySpan<Color16> source, Span<Color16> destination) {
		int targetStride = TargetSize.Width * ScalerInterface.Scale;
		int yLast = SourceSize.Height;

		if (0 >= yLast) {
			return;
		}

		//temporary buffer for "on the fly preprocessing"
		Span<PreprocessType> preProcBuffer = stackalloc PreprocessType[SourceSize.Width];
		preProcBuffer.Fill(0);

		static Color16 GetPixel(ReadOnlySpan<Color16> src, int stride, int offset) {
			// We can try embedded a distance calculation as well. Perhaps instead of a negative stride/offset, we provide a 
			// negative distance from the edge and just recalculate the stride/offset in that case.
			// We can scale the alpha reduction by the distance to hopefully correct the edges.

			// Alternatively, for non-wrapping textures (or for wrapping ones that only have one wrapped axis) we embed them in a new target
			// which is padded with alpha, and after resampling we manually clamp the colors on it. This will give a normal-ish effect for drawing, and will make it so we might get a more correct edge since it can overdraw.
			// If we do this, we draw the entire texture, with the padding, but we slightly enlarge the target area for _drawing_ to account for the extra padding.
			// This will effectively cause a filtering effect and hopefully prevent the hard edge problems

			if (stride >= 0 && offset >= 0) {
				return src[stride + offset];
			}

			stride = Math.Abs(stride);
			offset = Math.Abs(offset);
			Color16 sample = src[stride + offset];
			return sample with { A = 0 };
		}

		//initialize preprocessing buffer for first row:
		//detect upper left and right corner blending
		//this cannot be optimized for adjacent processing
		//stripes; we must not allow for a memory race condition!
		/*if (yFirst > 0)*/
		{
			var y = -1;

			var sM1 = SourceSize.X * getY(y - 1);
			var s0 = SourceSize.X * getY(y); //center line
			var sP1 = SourceSize.X * getY(y + 1);
			var sP2 = SourceSize.X * getY(y + 2);

			for (var x = 0; x < SourceSize.Width; ++x) {
				var xM1 = getX(x - 1);
				var xP1 = getX(x + 1);
				var xP2 = getX(x + 2);

				//read sequentially from memory as far as possible
				var ker4 = new Kernel4x4(
					GetPixel(source, sM1, xM1),
					GetPixel(source, sM1, x),
					GetPixel(source, sM1, xP1),
					GetPixel(source, sM1, xP2),

					GetPixel(source, s0, xM1),
					GetPixel(source, s0, x),
					GetPixel(source, s0, xP1),
					GetPixel(source, s0, xP2),

					GetPixel(source, sP1, xM1),
					GetPixel(source, sP1, x),
					GetPixel(source, sP1, xP1),
					GetPixel(source, sP1, xP2),

					GetPixel(source, sP2, xM1),
					GetPixel(source, sP2, x),
					GetPixel(source, sP2, xP1),
					GetPixel(source, sP2, xP2)
				);

				var blendResult = PreProcessCorners(in ker4); // writes to blendResult
				/*
				preprocessing blend result:
				---------
				| F | G | //evalute corner between F, G, J, K
				----|---| //input pixel is at position F
				| J | K |
				---------
				*/

				preProcBuffer[x].SetTopR(blendResult.J);

				if (x + 1 < SourceSize.Width) {
					preProcBuffer[x + 1].SetTopL(blendResult.K);
				}
				else if (Configuration.Wrapped.X) {
					preProcBuffer[0].SetTopL(blendResult.K);
				}
			}
		}

		var outputMatrix = new OutputMatrix(ScalerInterface.Scale, destination, TargetSize.Width);

		for (var y = 0; y < yLast; ++y) {
			//consider MT "striped" access
			var targetIndex = y * targetStride;

			var sM1 = SourceSize.X * getY(y - 1);
			var s0 = SourceSize.X * y; //center line
			var sP1 = SourceSize.X * getY(y + 1);
			var sP2 = SourceSize.X * getY(y + 2);

			PreprocessType blendXY1 = 0;

			for (var x = 0; x < SourceSize.Width; ++x, targetIndex += ScalerInterface.Scale) {
				var xM1 = getX(x - 1);
				var xP1 = getX(x + 1);
				var xP2 = getX(x + 2);

				//evaluate the four corners on bottom-right of current pixel
				//blend_xy for current (x, y) position

				//read sequentially from memory as far as possible
				var ker4 = new Kernel4x4(
					GetPixel(source, sM1, xM1),
					GetPixel(source, sM1, x),
					GetPixel(source, sM1, xP1),
					GetPixel(source, sM1, xP2),

					GetPixel(source, s0, xM1),
					source[s0 + x],
					GetPixel(source, s0, xP1),
					GetPixel(source, s0, xP2),

					GetPixel(source, sP1, xM1),
					GetPixel(source, sP1, x),
					GetPixel(source, sP1, xP1),
					GetPixel(source, sP1, xP2),

					GetPixel(source, sP2, xM1),
					GetPixel(source, sP2, x),
					GetPixel(source, sP2, xP1),
					GetPixel(source, sP2, xP2)
				);

				var blendResult = PreProcessCorners(in ker4); // writes to blendResult

				/*
						preprocessing blend result:
						---------
						| F | G | //evaluate corner between F, G, J, K
						----|---| //current input pixel is at position F
						| J | K |
						---------
				*/

				//all four corners of (x, y) have been determined at
				//this point due to processing sequence!
				var blendXY = preProcBuffer[x];
				blendXY.SetBottomR(blendResult.F);
				blendXY1.SetTopR(blendResult.J);
				preProcBuffer[x] = blendXY1;

				blendXY1 = 0;
				blendXY1.SetTopL(blendResult.K);

				//set 3rd known corner for (x + 1, y)
				int? preProcIndex = null;
				if (x + 1 < SourceSize.Width) {
					preProcIndex = x + 1;
				}
				else if (Configuration.Wrapped.X) {
					preProcIndex = 0;
				}
				if (preProcIndex.HasValue) {
					preProcBuffer[preProcIndex.Value].SetBottomL(blendResult.G);
				}

				//fill block of size scale * scale with the given color
				//  //place *after* preprocessing step, to not overwrite the
				//  //results while processing the the last pixel!
				FillBlock(destination, targetIndex, TargetSize.Width, source[s0 + x], ScalerInterface.Scale, TargetSize.Width);

				//blend four corners of current pixel
				if (!blendXY.BlendingNeeded()) {
					continue;
				}

				//read sequentially from memory as far as possible
				var ker3 = new Kernel3x3(
					ker4.A,
					ker4.B,
					ker4.C,

					ker4.E,
					ker4.F,
					ker4.G,

					ker4.I,
					ker4.J,
					ker4.K
				);

				ScalePixel(ScalerInterface, RotationDegree.R0, in ker3, ref outputMatrix, targetIndex, blendXY);
				ScalePixel(ScalerInterface, RotationDegree.R90, in ker3, ref outputMatrix, targetIndex, blendXY);
				ScalePixel(ScalerInterface, RotationDegree.R180, in ker3, ref outputMatrix, targetIndex, blendXY);
				ScalePixel(ScalerInterface, RotationDegree.R270, in ker3, ref outputMatrix, targetIndex, blendXY);
			}
		}
	}
}
