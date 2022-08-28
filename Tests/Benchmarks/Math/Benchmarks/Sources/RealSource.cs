﻿using Benchmarks.BenchmarkBase.Benchmarks;
using System.Runtime.CompilerServices;

namespace Benchmarks.Math.Benchmarks.Sources;

public abstract class RealSource : BenchmarkBase<DataSet<RealSource.RealData[]>, RealSource.RealData[]> {
	public readonly struct RealData {
		internal readonly float Single;
		internal readonly double Double;

		internal RealData(double value) {
			Single = (float)value;
			Double = value;
		}
	}

	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	protected static void AddSet(IList<double> set) {
		var newData = GC.AllocateUninitializedArray<RealData>(set.Count);
		for (var i = 0; i < set.Count; ++i) {
			var real = set[i];
			newData[i] = new(real);
		}
		DefaultDataSetsStatic.Add(new(newData));
	}
}