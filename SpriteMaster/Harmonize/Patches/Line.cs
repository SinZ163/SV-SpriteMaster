﻿using Microsoft.Xna.Framework.Graphics;
using SpriteMaster.Types;
using StardewValley;
using System;

namespace SpriteMaster.Harmonize.Patches;

static class Line {
	internal static readonly Lazy<InternalTexture2D> LineTexture = new(() => {
		var data = new Color8[] { new(0, 0, 0, 0), new(255, 255, 255, 255), new(0, 0, 0, 0) };
		var texture = new InternalTexture2D(DrawState.Device, 1, 3, false, SurfaceFormat.Color, 1);
		texture.SetData(data);
		return texture;
	});

	[Harmonize(
		typeof(StardewValley.Utility),
		"drawLineWithScreenCoordinates",
		Harmonize.Fixation.Prefix,
		Harmonize.PriorityLevel.Last,
		instance: false
	)]
	public static bool drawLineWithScreenCoordinates(int x1, int y1, int x2, int y2, SpriteBatch b, XNA.Color color1, float layerDepth) {
		if (!Config.Enabled || !Config.Extras.SmoothLines) {
			return true;
		}

		var start = new Vector2I(x2, y2);
		var end = new Vector2I(x1, y1);

		if (start == end) {
			return false;
		}

		var integralVector = start - end;
		float angle = (float)Math.Atan2(integralVector.Y, integralVector.X);

		Vector2F expectedSize = (integralVector.Length + 1.0f, 3.0f);
		if (expectedSize.X == 0.0f || expectedSize.Y == 0.0f) {
			return false;
		}

		var texture = LineTexture.Value;

		Vector2F spriteSize = (Vector2I)texture.Bounds.Size;
		Vector2F scale = expectedSize / spriteSize;

		var vector = ((Vector2F)integralVector).Normalized * 0.5f;

		Vector2F startPoint = (((Vector2F)end) + (0f, 2.0f)) - (Vector2F)vector;
		b.Draw(texture, startPoint, null, color1, angle, Vector2F.Zero, scale, SpriteEffects.None, layerDepth);
		return false;
	}
}
