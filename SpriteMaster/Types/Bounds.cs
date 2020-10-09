﻿using SpriteMaster.Extensions;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SpriteMaster.Types {
	using DrawingRectangle = System.Drawing.Rectangle;
	using XNARectangle = Microsoft.Xna.Framework.Rectangle;
	using XTileRectangle = xTile.Dimensions.Rectangle;

	public struct Bounds :
		ICloneable,
		IComparable,
		IComparable<Bounds>,
		IComparable<DrawingRectangle>,
		IComparable<XNARectangle>,
		IComparable<XTileRectangle>,
		IEquatable<Bounds>,
		IEquatable<DrawingRectangle>,
		IEquatable<XNARectangle>,
		IEquatable<XTileRectangle> {
		public static readonly Bounds Empty = new Bounds(0, 0, 0, 0);

		public Vector2I Offset;
		public Vector2I Extent;

		public Vector2I Position {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Offset = value; }
		}

		public Vector2I Location {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Offset = value; }
		}

		[Browsable(false)]
		public Vector2I Size {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Extent; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Extent = value; }
		}

		public int X {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset.X; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Offset.X = value; }
		}

		public int Y {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset.Y; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Offset.Y = value; }
		}

		public int Width {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Extent.X; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Extent.X = value; }
		}

		public int Height {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Extent.Y; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Extent.Y = value; }
		}

		[Browsable(false)]
		public int Left {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset.X; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Offset.X = value; }
		}

		[Browsable(false)]
		public int Top {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset.Y; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Offset.Y = value; }
		}

		[Browsable(false)]
		public int Right {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset.X + Extent.X; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Extent.X = value - Offset.X; }
		}

		[Browsable(false)]
		public int Bottom {
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			readonly get { return Offset.Y + Extent.Y; }
			[MethodImpl(MethodImplOptions.AggressiveInlining)]
			set { Extent.Y = value - Offset.Y; }
		}

		public readonly int Area => Extent.X * Extent.Y;

		public readonly bool Degenerate => Extent.X <= 0 || Extent.Y <= 0;

		public readonly bool IsEmpty => Area == 0;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (Vector2I offset, Vector2I extent) {
			Contract.AssertPositiveOrZero(extent.Width, $"{nameof(extent.Width)} is not positive");
			Contract.AssertPositiveOrZero(extent.Height, $"{nameof(extent.Height)} is not positive");
			Offset = offset;
			Extent = extent;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (int x, int y, int width, int height) : this(new Vector2I(x, y), new Vector2I(width, height)) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (int width, int height) : this(0, 0, width, height) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (Vector2I extent) : this(Vector2I.Zero, extent) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (in Bounds bounds) {
			Offset = bounds.Offset;
			Extent = bounds.Extent;
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (in DrawingRectangle rect) : this(rect.X, rect.Y, rect.Width, rect.Height) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (in XNARectangle rect) : this(rect.X, rect.Y, rect.Width, rect.Height) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (in XTileRectangle rect) : this(rect.X, rect.Y, rect.Width, rect.Height) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (Microsoft.Xna.Framework.Graphics.Texture2D tex) : this(tex.Width, tex.Height) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public Bounds (System.Drawing.Bitmap bmp) : this(bmp.Width, bmp.Height) { }

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public bool Overlaps (in Bounds other) =>
		!(
			other.Left > Right ||
			other.Right < Left ||
			other.Top > Bottom ||
			other.Bottom < Top
		);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly Bounds Clone () => this;

		readonly object ICloneable.Clone () => this;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator DrawingRectangle (in Bounds bounds) => new DrawingRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator XNARectangle (in Bounds bounds) => new XNARectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator XTileRectangle (in Bounds bounds) => new XTileRectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Bounds (in DrawingRectangle rect) => new Bounds(rect);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Bounds (in XNARectangle rect) => new Bounds(rect);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static implicit operator Bounds (in XTileRectangle rect) => new Bounds(rect);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public override readonly string ToString () => $"[{X}, {Y}, {Width}, {Height}]";

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo (Bounds other) {
			var result = Offset.CompareTo(other.Offset);
			if (result != 0) {
				return result;
			}
			return Extent.CompareTo(other.Extent);
		}

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo (DrawingRectangle other) => CompareTo((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo (XNARectangle other) => CompareTo((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly int CompareTo (XTileRectangle other) => CompareTo((Bounds)other);

		readonly int IComparable.CompareTo (object other) => other switch {
			Bounds bounds => CompareTo(bounds),
			DrawingRectangle rect => CompareTo(rect),
			XNARectangle rect => CompareTo(rect),
			XTileRectangle rect => CompareTo(rect),
			_ => throw new ArgumentException(),
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override int GetHashCode () => unchecked((int)Hash.Combine(Offset.GetHashCode(), Extent.GetHashCode()));

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly override bool Equals (object other) => other switch {
			Bounds bounds => Equals(bounds),
			DrawingRectangle rect => Equals(rect),
			XNARectangle rect => Equals(rect),
			XTileRectangle rect => Equals(rect),
			_ => throw new ArgumentException(),
		};

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals (Bounds other) => Offset == other.Offset && Extent == other.Extent;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals (DrawingRectangle other) => Equals((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals (XNARectangle other) => Equals((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool Equals (XTileRectangle other) => Equals((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool NotEquals (in Bounds other) => Offset != other.Offset || Extent != other.Extent;

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool NotEquals (in DrawingRectangle other) => NotEquals((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool NotEquals (in XNARectangle other) => NotEquals((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public readonly bool NotEquals (in XTileRectangle other) => NotEquals((Bounds)other);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in Bounds lhs, in Bounds rhs) => lhs.Equals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in Bounds lhs, in Bounds rhs) => lhs.NotEquals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in Bounds lhs, in DrawingRectangle rhs) => lhs.Equals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in Bounds lhs, in DrawingRectangle rhs) => lhs.NotEquals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in DrawingRectangle lhs, in Bounds rhs) => rhs.Equals(lhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in DrawingRectangle lhs, in Bounds rhs) => rhs.NotEquals(lhs);


		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in Bounds lhs, in XNARectangle rhs) => lhs.Equals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in Bounds lhs, in XNARectangle rhs) => lhs.NotEquals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in XNARectangle lhs, in Bounds rhs) => rhs.Equals(lhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in XNARectangle lhs, in Bounds rhs) => rhs.NotEquals(lhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in Bounds lhs, in XTileRectangle rhs) => lhs.Equals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in Bounds lhs, in XTileRectangle rhs) => lhs.NotEquals(rhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator == (in XTileRectangle lhs, in Bounds rhs) => rhs.Equals(lhs);

		[MethodImpl(MethodImplOptions.AggressiveInlining)]
		public static bool operator != (in XTileRectangle lhs, in Bounds rhs) => rhs.NotEquals(lhs);
	}
}
