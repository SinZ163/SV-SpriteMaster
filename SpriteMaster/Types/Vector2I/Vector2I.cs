﻿using SpriteMaster.Extensions;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace SpriteMaster.Types;

[CLSCompliant(false)]
[DebuggerDisplay("[{X}, {Y}}")]
[StructLayout(LayoutKind.Sequential, Pack = Alignment, Size = ByteSize)]
internal partial struct Vector2I :
	ILongHash {
	internal const int ByteSize = sizeof(ulong);
	internal const int Alignment = sizeof(ulong);

	internal static readonly Vector2I MaxValue = (int.MaxValue, int.MaxValue);
	internal static readonly Vector2I MinValue = (int.MinValue, int.MinValue);

	internal static readonly Vector2I Zero = (0, 0);
	internal static readonly Vector2I One = (1, 1);
	internal static readonly Vector2I MinusOne = (-1, -1);
	internal static readonly Vector2I Empty = Zero;

	internal ulong Packed;

	[StructLayout(LayoutKind.Sequential, Pack = Alignment, Size = ByteSize)]
	private struct PackedInt {
		internal int X;
		internal int Y;
	}

	internal int X {
		readonly get => Unsafe.As<ulong, PackedInt>(ref Unsafe.AsRef(in Packed)).X;
		set => Unsafe.As<ulong, PackedInt>(ref Packed).X = value;
	}

	internal int Y {
		readonly get => Unsafe.As<ulong, PackedInt>(ref Unsafe.AsRef(in Packed)).Y;
		set => Unsafe.As<ulong, PackedInt>(ref Packed).Y = value;
	}

	internal int Width {
		[MethodImpl(Runtime.MethodImpl.Hot)]
		readonly get => X;
		[MethodImpl(Runtime.MethodImpl.Hot)]
		set => X = value;
	}
	internal int Height {
		[MethodImpl(Runtime.MethodImpl.Hot)]
		readonly get => Y;
		[MethodImpl(Runtime.MethodImpl.Hot)]
		set => Y = value;
	}

	internal readonly int Area => X * Y;

	internal readonly bool IsEmpty => Packed == 0UL;
	internal readonly bool IsZero => Packed == 0UL;
	internal readonly int MinOf => Math.Min(X, Y);
	internal readonly int MaxOf => Math.Max(X, Y);

	internal readonly int Sum => X + Y;

	internal readonly int LengthSquared => X * X + Y * Y;
	internal readonly float Length => MathF.Sqrt(LengthSquared);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(ulong packed) : this() => Packed = packed;

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static Vector2I From(ulong packed) => new(packed: packed);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(int x, int y) : this() {
		X = x;
		Y = y;
	}

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static Vector2I From(int x, int y) => new(x, y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(in (int X, int Y) vec) : this(vec.X, vec.Y) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static Vector2I From(in (int X, int Y) vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(int value) : this(value, value) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal static Vector2I From(int value) => new(value);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(in XVector2 vector, bool round = true) : this(round ? vector.NearestInt() : vector.TruncateInt()) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(in Vector2F vector, bool round = true) : this(round ? vector.NearestInt() : vector.TruncateInt()) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(Vector2I vec) : this(vec.Packed) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(DrawingPoint v) : this(v.X, v.Y) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(XNA.Point v) : this(v.X, v.Y) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(XTilePoint v) : this(v.X, v.Y) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(DrawingSize v) : this(v.Width, v.Height) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(XTileSize v) : this(v.Width, v.Height) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	internal Vector2I(XTexture2D tex) : this(tex.Width, tex.Height) { }

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Vector2I(in (int X, int Y) vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator (int X, int Y)(Vector2I vec) => (vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator DrawingPoint(Vector2I vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator XNA.Point(Vector2I vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator XTilePoint(Vector2I vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator DrawingSize(Vector2I vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator XTileSize(Vector2I vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator XVector2(Vector2I vec) => new(vec.X, vec.Y);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Vector2I(DrawingPoint vec) => new(vec);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Vector2I(XNA.Point vec) => new(vec);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Vector2I(XTilePoint vec) => new(vec);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Vector2I(DrawingSize vec) => new(vec);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Vector2I(XTileSize vec) => new(vec);

	[MethodImpl(Runtime.MethodImpl.Hot)]
	public static implicit operator Bounds(Vector2I vec) => new(vec);

	public override readonly string ToString() => $"{{{X}, {Y}}}";
	public readonly string ToString(IFormatProvider? provider) => $"{{{X.ToString(provider)}, {Y.ToString(provider)}}}";

	// C# GetHashCode on all integer primitives, even longs, just returns it truncated to an int.
	[MethodImpl(Runtime.MethodImpl.Hot)]
	public override readonly int GetHashCode() => Hashing.Combine(X.GetHashCode(), Y.GetHashCode());

	[MethodImpl(Runtime.MethodImpl.Hot)]
	ulong ILongHash.GetLongHashCode() => ((ulong)X.GetHashCode() << 32) | (uint)Y.GetHashCode();
}
