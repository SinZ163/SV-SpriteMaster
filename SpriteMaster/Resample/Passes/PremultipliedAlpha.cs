﻿using SpriteMaster.Configuration;
using SpriteMaster.Types;
using System;
using System.Runtime.CompilerServices;

namespace SpriteMaster.Resample.Passes;

internal static class PremultipliedAlpha {
	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static void Apply(Span<Color16> data, Vector2I size) {
		foreach (ref var color in data) {
			color.R *= color.A;
			color.G *= color.A;
			color.B *= color.A;
		}
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static void Reverse(Span<Color16> data, Vector2I size) {
		foreach (ref var color in data) {
			if (color.A == Types.Fixed.Fixed16.Zero || color.A == Types.Fixed.Fixed16.Max) {
				continue;
			}

			if (color.A.Value < Config.Resample.PremultiplicationLowPass) {
				continue;
			}

			color.R = color.R.ClampedDivide(color.A);
			color.G = color.G.ClampedDivide(color.A);
			color.B = color.B.ClampedDivide(color.A);
		}
	}
}
