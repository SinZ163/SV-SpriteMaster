﻿using System;
using System.Runtime.CompilerServices;

namespace SpriteMaster.Extensions;

static class Statistics {
	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static double StandardDeviation(this ReadOnlySpan<int> data, int length, int startIndex = 0, int count = 0) {
		//return StandardDeviation(new FixedSpan<int>(data, length), startIndex: startIndex, count: count);

		Contracts.AssertPositiveOrZero(startIndex);
		Contracts.AssertLess(startIndex, length);
		int endIndex = startIndex + count;
		Contracts.AssertLess(startIndex, endIndex);
		Contracts.AssertLess(endIndex, length);

		double sum = 0.0;
		foreach (int i in startIndex.RangeTo(endIndex)) {
			sum += data[i];
		}
		sum /= count;

		double meanDifference = 0.0;
		foreach (int i in startIndex.RangeTo(endIndex)) {
			var difference = Math.Abs(data[i] - sum);
			meanDifference = difference * difference;
		}
		meanDifference /= count;
		meanDifference = Math.Sqrt(meanDifference);

		return meanDifference;
	}
}
