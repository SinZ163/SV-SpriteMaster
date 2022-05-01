﻿using Microsoft.Xna.Framework.Graphics;
using SpriteMaster.Configuration;
using SpriteMaster.Types;
using StardewValley;
using System;
using System.Diagnostics;
using System.Text;

namespace SpriteMaster.Harmonize.Patches.Game;

static class ShadowedText {
	private static bool LongWords => Game1.content.GetCurrentLanguage() switch {
		LocalizedContentManager.LanguageCode.ru => true,
		LocalizedContentManager.LanguageCode.de => true,
		_ => false
	};

	[Harmonize(
		typeof(StardewValley.Utility),
		"drawTextWithShadow",
		Harmonize.Fixation.Prefix,
		Harmonize.PriorityLevel.Last,
		instance: false,
		critical: false
	)]
	public static bool DrawTextWithShadow(
		SpriteBatch b,
		StringBuilder text,
		SpriteFont font,
		XNA.Vector2 position,
		XNA.Color color,
		float scale = 1f,
		float layerDepth = -1f,
		int horizontalShadowOffset = -1,
		int verticalShadowOffset = -1,
		float shadowIntensity = 1f,
		int numShadows = 3
	) {
		if (!Config.IsUnconditionallyEnabled || !Config.Extras.StrokeShadowedText) {
			return true;
		}

		if (layerDepth == -1f) {
			layerDepth = position.Y / 10000f;
		}

		/*
		if (horizontalShadowOffset == -1) {
			horizontalShadowOffset = ((font.Equals(Game1.smallFont) || LongWords) ? (-2) : (-3));
		}
		if (verticalShadowOffset == -1) {
			verticalShadowOffset = ((font.Equals(Game1.smallFont) || LongWords) ? 2 : 3);
		}
		*/

		if (text is null) {
			throw new ArgumentNullException(nameof(text));
		}

		DrawStrokedText(
			b,
			text.ToString(),
			font,
			position,
			color,
			new XNA.Color(221, 148, 84) * shadowIntensity,
			scale,
			layerDepth,
			(horizontalShadowOffset, verticalShadowOffset),
			numShadows
		);

		return false;
	}

	[Harmonize(
		typeof(StardewValley.Utility),
		"drawTextWithShadow",
		Harmonize.Fixation.Prefix,
		Harmonize.PriorityLevel.Last,
		instance: false,
		critical: false
	)]
	public static bool DrawTextWithShadow(
		SpriteBatch b,
		string text,
		SpriteFont font,
		XNA.Vector2 position,
		XNA.Color color,
		float scale = 1f,
		float layerDepth = -1f,
		int horizontalShadowOffset = -1,
		int verticalShadowOffset = -1,
		float shadowIntensity = 1f,
		int numShadows = 3
	) {
		if (!Config.IsUnconditionallyEnabled || !Config.Extras.StrokeShadowedText) {
			return true;
		}

		if (layerDepth == -1f) {
			layerDepth = position.Y / 10000f;
		}

		if (horizontalShadowOffset == -1) {
			horizontalShadowOffset = ((font.Equals(Game1.smallFont) || LongWords) ? (-2) : (-3));
		}
		if (verticalShadowOffset == -1) {
			verticalShadowOffset = ((font.Equals(Game1.smallFont) || LongWords) ? 2 : 3);
		}

		text ??= "";

		// true;

		DrawStrokedText(
			b,
			text,
			font,
			position,
			color,
			new XNA.Color(221, 148, 84) * shadowIntensity,
			scale,
			layerDepth,
			(horizontalShadowOffset, verticalShadowOffset),
			numShadows
		);

		return false;
	}

	[Harmonize(
		typeof(StardewValley.Utility),
		"drawTextWithColoredShadow",
		Harmonize.Fixation.Prefix,
		Harmonize.PriorityLevel.Last,
		instance: false,
		critical: false
	)]
	public static bool DrawTextWithColoredShadow(
		SpriteBatch b,
		string text,
		SpriteFont font,
		XNA.Vector2 position,
		XNA.Color color,
		XNA.Color shadowColor,
		float scale = 1f,
		float layerDepth = -1f,
		int horizontalShadowOffset = -1,
		int verticalShadowOffset = -1,
		int numShadows = 3
	) {
		if (!Config.IsUnconditionallyEnabled || !Config.Extras.StrokeShadowedText) {
			return true;
		}

		if (layerDepth == -1f) {
			layerDepth = position.Y / 10000f;
		}

		if (horizontalShadowOffset == -1) {
			horizontalShadowOffset = ((font.Equals(Game1.smallFont) || LongWords) ? (-2) : (-3));
		}
		if (verticalShadowOffset == -1) {
			verticalShadowOffset = ((font.Equals(Game1.smallFont) || LongWords) ? 2 : 3);
		}

		text ??= "";

		DrawStrokedText(
			b,
			text,
			font,
			position,
			color,
			shadowColor,
			scale,
			layerDepth,
			(horizontalShadowOffset, verticalShadowOffset),
			numShadows
		);

		return false;
	}

	private static readonly Vector2F[] ShadowedStringOffsets = { (-1, -1), (1, -1), (-1, 1), (1, 1) };

	private static void DrawStrokedText(
		SpriteBatch b,
		string text,
		SpriteFont font,
		Vector2F position,
		XNA.Color color,
		XNA.Color shadowColor,
		float scale,
		float layerDepth,
		Vector2I shadowOffset,
		int numShadows
	) {
		foreach (var offset in ShadowedStringOffsets) {
			b.DrawString(
				font,
				text,
				position + offset,
				shadowColor,
				0f,
				XNA.Vector2.Zero,
				scale,
				SpriteEffects.None,
				layerDepth
			);
		}

		b.DrawString(
			font,
			text,
			position,
			color,
			0f,
			XNA.Vector2.Zero,
			scale,
			SpriteEffects.None,
			layerDepth
		);
	}
}
