﻿using Microsoft.Xna.Framework.Graphics;
using SpriteMaster.Extensions;
using SpriteMaster.Harmonize.Patches.Game;
using SpriteMaster.Types;
using System;
using System.Diagnostics.CodeAnalysis;
using static SpriteMaster.Harmonize.Harmonize;
using SpriteBatcher = System.Object;

namespace SpriteMaster.Harmonize.Patches.PSpriteBatch;

[SuppressMessage("Code Quality", "IDE0051:Remove unused private members", Justification = "Harmony")]
[SuppressMessage("Style", "IDE0060:Remove unused parameter", Justification = "Harmony")]
static class PlatformRenderBatch {
	private static SamplerState GetNewSamplerState(Texture texture, SamplerState reference) {
		if (!Config.DrawState.SetLinear) {
			return reference;
		}

		bool isManagedTexture = texture is InternalTexture2D;
		bool isLighting = !isManagedTexture && (texture?.NormalizedName().StartsWith(@"LooseSprites\Lighting\") ?? false);

		if (isManagedTexture || isLighting) {
			if (reference.AddressU == TextureAddressMode.Wrap && reference.AddressV == TextureAddressMode.Wrap) {
				if (!isLighting && Config.Resample.Scaler == Resample.Resampler.Scaler.EPX) {
					return SamplerState.PointWrap;
				}
				else {
					return SamplerState.LinearWrap;
				}
			}
			else if (reference.AddressU == TextureAddressMode.Border && reference.AddressV == TextureAddressMode.Border) {
				if (!isLighting && Config.Resample.Scaler == Resample.Resampler.Scaler.EPX) {
					return DrawState.PointBorder.Value;
				}
				else {
					return DrawState.LinearBorder.Value;
				}
			}
			else if (reference.AddressU == TextureAddressMode.Mirror && reference.AddressV == TextureAddressMode.Mirror) {
				if (!isLighting && Config.Resample.Scaler == Resample.Resampler.Scaler.EPX) {
					return DrawState.PointMirror.Value;
				}
				else {
					return DrawState.LinearMirror.Value;
				}
			}
			else {
				if (!isLighting && Config.Resample.Scaler == Resample.Resampler.Scaler.EPX) {
					return SamplerState.PointClamp;
				}
				else {
					return SamplerState.LinearClamp;
				}
			}
		}

		return reference;
	}

	internal readonly record struct States(SamplerState? SamplerState, BlendState? BlendState);

	[Harmonize(
		"Microsoft.Xna.Framework.Graphics",
		"Microsoft.Xna.Framework.Graphics.SpriteBatcher",
		"FlushVertexArray",
		Harmonize.Fixation.Prefix,
		PriorityLevel.First,
		platform: Harmonize.Platform.MonoGame
	)]
	public static void OnFlushVertexArray(
		SpriteBatcher __instance,
		int start,
		int end,
		Effect effect,
		Texture texture,
		GraphicsDevice ____device,
		ref States __state
	) {
		if (!Config.IsEnabled) {
			return;
		}

		SamplerState? originalSamplerState = null;
		BlendState? originalBlendState = null;

		try {
			using var watchdogScoped = WatchDog.WatchDog.ScopedWorkingState;

			{
				var originalState = ____device?.SamplerStates[0] ?? SamplerState.PointClamp;

				var newState = GetNewSamplerState(texture, originalState);

				if (newState != originalState && ____device?.SamplerStates is not null) {
					originalSamplerState = originalState;
					____device.SamplerStates[0] = newState;
				}
				else {
					originalSamplerState = null;
				}
			}
			{
				if (____device is not null) {
					var originalState = ____device.BlendState;
					if (texture == Line.LineTexture.Value) {
						____device.BlendState = BlendState.AlphaBlend;
					}
					originalBlendState = originalState;
				}
				else {
					originalBlendState = null;
				}
			}
		}
		catch (Exception ex) {
			ex.PrintError();
		}

		__state = new(originalSamplerState, originalBlendState);

		return;
	}

	[Harmonize(
		"Microsoft.Xna.Framework.Graphics",
		"Microsoft.Xna.Framework.Graphics.SpriteBatcher",
		"FlushVertexArray",
		Harmonize.Fixation.Postfix,
		PriorityLevel.Last,
		platform: Harmonize.Platform.MonoGame
	)]
	public static void OnFlushVertexArray(
	SpriteBatcher __instance,
	int start,
	int end,
	Effect effect,
	Texture texture,
	GraphicsDevice ____device,
	States __state
) {
		if (!Config.IsEnabled) {
			return;
		}

		try {
			using var watchdogScoped = WatchDog.WatchDog.ScopedWorkingState;

			if (__state.SamplerState is not null && ____device?.SamplerStates is not null && __state.SamplerState != ____device.SamplerStates[0]) {
				____device.SamplerStates[0] = __state.SamplerState;
			}
			if (__state.BlendState is not null && ____device?.BlendState is not null && __state.BlendState != ____device.BlendState) {
				____device.BlendState = __state.BlendState;
			}
		}
		catch (Exception ex) {
			ex.PrintError();
		}
	}
}
