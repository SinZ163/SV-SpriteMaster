﻿using SpriteMaster.Extensions;
using System;
using System.Runtime.CompilerServices;

namespace SpriteMaster;

internal sealed class TexelTimer {
	private long TotalDuration = 0;
	private long TotalTexels = 0;

	private double DurationPerTexel => (TotalTexels == 0) ? 0.0 : (double)TotalDuration / TotalTexels;

	internal void Reset() {
		TotalDuration = 0;
		TotalTexels = 0;
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal void Add(int texels, in TimeSpan duration) {
		// Avoid a division by zero
		if (texels == 0) {
			return;
		}

		TotalDuration += duration.Ticks;
		TotalTexels += texels;
		//var texelDuration = (double)duration.Ticks / texels;
		//DurationPerTexel -= DurationPerTexel / MaxDurationCounts;
		//DurationPerTexel += texelDuration / MaxDurationCounts;
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal void Add(TextureAction action, in TimeSpan duration) => Add(action.Size, duration);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal TimeSpan Estimate(int texels) => TimeSpan.FromTicks((DurationPerTexel * texels).NextLong());

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal TimeSpan Estimate(TextureAction action) => Estimate(action.Size);
}
