﻿//using ManagedSquish;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SpriteMaster.Extensions;
using SpriteMaster.Metadata;
using SpriteMaster.Resample;
using SpriteMaster.Types;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using xBRZNet2;
using static SpriteMaster.ScaledTexture;

namespace SpriteMaster {
	internal sealed class Upscaler {
		internal static void PurgeHash (Texture2D reference) {
			reference.Meta().CachedData = null;
		}

		// https://stackoverflow.com/a/12996028
		private static ulong HashULong (ulong x) {
			unchecked {
				x = (x ^ (x >> 30)) * 0xbf58476d1ce4e5b9ul;
				x = (x ^ (x >> 27)) * 0x94d049bb133111ebul;
				x = x ^ (x >> 31);
			}
			return x;
		}

		internal static ulong GetHash (SpriteInfo input, bool desprite) {
			// Need to make Hashing.CombineHash work better.
			ulong hash;
			if (!Config.Resample.EnableDynamicScale) {
				hash = Hashing.CombineHash(input.Reference.Name?.GetHashCode(), input.Reference.Meta().GetHash(input));
			}
			else {
				hash = Hashing.CombineHash(input.Reference.Name?.GetHashCode(), input.Reference.Meta().GetHash(input), HashULong(unchecked((ulong)input.ExpectedScale)));
			}
			if (desprite) {
				hash = Hashing.CombineHash(hash, input.Size.Hash());
			}
			else {

			}
			return hash;
		}

		private static readonly WeakSet<Texture2D> GarbageMarkSet = Config.GarbageCollectAccountUnownedTextures ? new WeakSet<Texture2D>() : null;

		// TODO : use MemoryFailPoint class. Extensively.

		internal static ManagedTexture2D Upscale (ScaledTexture texture, ref int scale, SpriteInfo input, bool desprite, ulong hash, ref Vector2B wrapped, bool async) {
			// Try to process the texture twice. Garbage collect after a failure, maybe it'll work then.
			foreach (var _ in 0.To(1)) {
				try {
					return UpscaleInternal(
						texture: texture,
						scale: ref scale,
						input: input,
						desprite: desprite,
						hash: hash,
						wrapped: ref wrapped,
						async: async
					);
				}
				catch (OutOfMemoryException) {
					Garbage.Collect(compact: true, blocking: true, background: false);
				}
			}

			texture.Texture = null;
			return null;
		}

		private static unsafe ManagedTexture2D UpscaleInternal (ScaledTexture texture, ref int scale, SpriteInfo input, bool desprite, ulong hash, ref Vector2B wrapped, bool async) {
			var rawTextureData = input.Data;
			var spriteFormat = TextureFormat.Color;

			if (Config.GarbageCollectAccountUnownedTextures && GarbageMarkSet.Add(input.Reference)) {
				Garbage.Mark(input.Reference);
				input.Reference.Disposing += (object obj, EventArgs args) => {
					Garbage.Unmark((Texture2D)obj);
				};
			}

			bool isWater = input.Size.Right <= 640 && input.Size.Top >= 2000 && input.Size.Width >= 4 && input.Size.Height >= 4 && texture.Name == "LooseSprites\\Cursors";

			var spriteBounds = input.Size;
			var textureSize = input.ReferenceSize;

			if (isWater) {
				spriteBounds.X /= 4;
				spriteBounds.Y /= 4;
				spriteBounds.Width /= 4;
				spriteBounds.Height /= 4;

				textureSize.Width /= 4;
				textureSize.Height /= 4;
			}

			wrapped.Set(false);

			ManagedTexture2D output = null;

			var inputSize = desprite ? spriteBounds.Extent : textureSize;

			if (Config.Resample.Scale) {
				int originalScale = scale;
				scale = 2;
				foreach (int s in originalScale..2) {
					var newDimensions = inputSize * s;
					if (newDimensions.X <= Config.PreferredMaxTextureDimension && newDimensions.Y <= Config.PreferredMaxTextureDimension) {
						scale = s;
						break;
					}
				}
			}

			var scaledSize = inputSize * scale;
			var newSize = scaledSize.Min(Config.ClampDimension);

			var scaledDimensions = spriteBounds.Extent * scale; // TODO : should this be (inputSize * scale)?
			texture.UnpaddedSize = newSize;

			texture.AdjustedScale = (Vector2)newSize / inputSize;

			/*
      if (TextureCache.TryGetValue(hash, out Texture2D cachedTexture))
      {
        if (!cachedTexture.IsDisposed)
        {
          Debug.ErrorLn("Found Cached Texture");
          return cachedTexture;
        }
      }
      */

			// Apparently, using the filename is not sufficient for hashing cache data. So, we will need to hash the texture data, apparently. This is really messy.
			var hashString = hash.ToString("x");
			var cachePath = Cache.GetPath($"{hashString}.cache");

			int[] bitmapData = null;
			try {
				try {
					if (Cache.Fetch(cachePath, out var fetchSize, out spriteFormat, out wrapped, out texture.Padding, out texture.BlockPadding, out bitmapData)) {
						scaledDimensions = newSize = scaledSize = fetchSize;
					}
				}
				catch (Exception ex) {
					ex.PrintWarning();
				}

				if (bitmapData == null) {
					Bitmap GetDumpBitmap (Bitmap source) {
						var dump = (Bitmap)source.Clone();
						foreach (int y in 0..dump.Height)
							foreach (int x in 0..dump.Width) {
								dump.SetPixel(
									x, y,
									Texel.FromARGB(dump.GetPixel(x, y)).Color
								);
							}

						return dump;
					}


					Vector2B WrappedX;
					Vector2B WrappedY;

					// Water in the game is pre-upscaled by 4... which is weird.
					Span<int> rawData;
					if (isWater) {
						var veryRawData = MemoryMarshal.Cast<byte, int>(rawTextureData.AsSpan());
						rawData = new Span<int>(new int[veryRawData.Length / 16]);
						foreach (int y in 0..textureSize.Height) {
							var ySourceOffset = (y * 4) * input.ReferenceSize.Width;
							var yDestinationOffset = y * textureSize.Width;
							foreach (int x in 0..textureSize.Width) {
								rawData[yDestinationOffset + x] = veryRawData[ySourceOffset + (x * 4)];
							}
						}
					}
					else {
						rawData = MemoryMarshal.Cast<byte, int>(rawTextureData.AsSpan());
					}

					var edgeResults = Edge.AnalyzeLegacy(
						reference: input.Reference,
						data: rawData,
						rawSize: textureSize,
						spriteSize: spriteBounds,
						Wrapped: input.Wrapped
					);

					(WrappedX, WrappedY, wrapped) = (edgeResults.WrappedX, edgeResults.WrappedY, edgeResults.Wrapped);

					// Recolor
					/*
					{
						foreach (int y in input.Size.Top..input.Size.Bottom) {
							int offsetY = y * input.ReferenceSize.Width;
							foreach (int x in input.Size.Left..input.Size.Right) {
								int offset = offsetY + x;

								var texel = unchecked((uint)rawData[offset]);

								var bI = (texel >> 16) & 0xFFU;
								var gI = (texel >> 8) & 0xFFU;
								var rI = (texel) & 0xFFU;

								var color = new Vector3(
									(float)rI / 255.0f,
									(float)gI / 255.0f,
									(float)bI / 255.0f
								);

								float Curve (float x) {
									return (float)((Math.Sin(x * Math.PI - (Math.PI / 2.0))) + 1) / 2;
								}

								float Recolor (float x) {
									return Floating.Lerp(x, Curve(x), 0.25f);
								}

								color.X = Recolor(color.X);
								color.Y = Recolor(color.Y);
								color.Z = Recolor(color.Z);

								bI = (uint)Math.Min(255f, color.Z * 255f);
								gI = (uint)Math.Min(255f, color.Y * 255f);
								rI = (uint)Math.Min(255f, color.X * 255f);

								texel =
									(texel & 0xFF000000U) |
									(bI << 16) |
									(gI << 8) |
									(rI);

								rawData[offset] = unchecked((int)texel);
							}
						}
					}
					*/
					// ~Recolor

					wrapped &= Config.Resample.EnableWrappedAddressing;

					if (Config.Debug.Sprite.DumpReference) {
						using (var filtered = Textures.CreateBitmap(rawData.ToArray(), textureSize, PixelFormat.Format32bppArgb)) {
							using (var submap = (Bitmap)filtered.Clone(spriteBounds, filtered.PixelFormat)) {
								var dump = GetDumpBitmap(submap);
								var path = Cache.GetDumpPath($"{input.Reference.SafeName().Replace("\\", ".").Replace("/", ".")}.{hashString}.reference.png");
								File.Delete(path);
								dump.Save(path, ImageFormat.Png);
							}
						}
					}

					if (Config.Resample.Smoothing) {
						var scalerConfig = new xBRZNet2.Config(wrappedX: (wrapped.X && false) || isWater, wrappedY: (wrapped.Y && false) || isWater);

						// Do we need to pad the sprite?
						var prescaleData = rawData;
						var prescaleSize = textureSize;

						var shouldPad = new Vector2B(
							!(WrappedX.Positive || WrappedX.Negative) && Config.Resample.Padding.Enabled && inputSize.X > 1,
							!(WrappedY.Positive || WrappedX.Negative) && Config.Resample.Padding.Enabled && inputSize.Y > 1
						);

						var outputSize = spriteBounds;

						if (
							(
								prescaleSize.X <= Config.Resample.Padding.MinimumSizeTexels &&
								prescaleSize.Y <= Config.Resample.Padding.MinimumSizeTexels
							) ||
							(Config.Resample.Padding.IgnoreUnknown && !input.Reference.Name.IsBlank())
						) {
							shouldPad = Vector2B.False;
						}

						// TODO : make X and Y variants of the whitelist and blacklist
						if (input.Reference.Name != null) {
							if (Config.Resample.Padding.Whitelist.Contains(input.Reference.Name)) {
								shouldPad = Vector2B.True;
							}
							else if (Config.Resample.Padding.Blacklist.Contains(input.Reference.Name)) {
								shouldPad = Vector2B.False;
							}
						}

						if (shouldPad.Any) {
							int expectedPadding = Math.Max(1, scale / 2);

							// TODO we only need to pad the edge that has texels. Double padding is wasteful.
							var paddedSize = inputSize.Clone();
							var spriteSize = inputSize.Clone();

							if (shouldPad.X) {
								if ((paddedSize.X + expectedPadding * 2) * scale > Config.ClampDimension) {
									shouldPad.X = false;
								}
								else {
									paddedSize.X += expectedPadding * 2;
									texture.Padding.X = expectedPadding;
								}
							}
							if (shouldPad.Y) {
								if ((paddedSize.Y + expectedPadding * 2) * scale > Config.ClampDimension) {
									shouldPad.Y = false;
								}
								else {
									paddedSize.Y += expectedPadding * 2;
									texture.Padding.Y = expectedPadding;
								}
							}

							paddedSize += texture.BlockPadding;

							var hasPadding = shouldPad;

							if (hasPadding.Any) {
								int[] paddedData = new int[paddedSize.Area];

								int y = 0;

								// TODO : when writing padding, we might want to consider color clamping but without alpha, especially for block padding.

								const int padConstant = 0x00000000;

								void WritePaddingY () {
									if (!hasPadding.Y)
										return;
									foreach (int i in 0..texture.Padding.Y) {
										int strideOffset = y * paddedSize.Width;
										foreach (int x in 0..paddedSize.Width) {
											paddedData[strideOffset + x] = padConstant;
										}
										++y;
									}
								}

								WritePaddingY();

								foreach (int i in 0..spriteSize.Height) {
									int strideOffset = y * paddedSize.Width;
									int strideOffsetRaw = (i + spriteBounds.Top) * prescaleSize.Width;
									// Write a padded X line
									int xOffset = strideOffset;
									void WritePaddingX () {
										if (!hasPadding.X)
											return;
										foreach (int x in 0..texture.Padding.X) {
											paddedData[xOffset++] = padConstant;
										}
									}
									WritePaddingX();
									foreach (int x in 0..spriteSize.Width) {
										paddedData[xOffset++] = rawData[strideOffsetRaw + x + spriteBounds.Left];
									}
									WritePaddingX();
									++y;
								}

								WritePaddingY();

								prescaleData = new Span<int>(paddedData);
								prescaleSize = paddedSize;
								scaledDimensions = scaledSize = newSize = prescaleSize * scale;
								outputSize = prescaleSize;
								//scaledDimensions = originalPaddedSize * scale;
							}
						}

						bitmapData = new int[scaledSize.Area];

						// TODO add a stride/mask so it won't write to the block padding area.
						try {
							new xBRZScaler(
								scaleMultiplier: scale,
								sourceData: prescaleData,
								sourceWidth: prescaleSize.Width,
								sourceHeight: prescaleSize.Height,
								sourceTarget: outputSize,
								targetData: bitmapData,
								configuration: scalerConfig
							);
						}
						catch (Exception ex) {
							ex.PrintError();
							throw;
						}
						//ColorSpace.ConvertLinearToSRGB(bitmapData, Texel.Ordering.ARGB);
					}
					else {
						bitmapData = rawData.ToArray();
					}

					if (Config.Debug.Sprite.DumpResample) {
						using (var filtered = Textures.CreateBitmap(bitmapData, scaledDimensions, PixelFormat.Format32bppArgb)) {
							using (var dump = GetDumpBitmap(filtered)) {
								var path = Cache.GetDumpPath($"{input.Reference.SafeName().Replace("\\", ".")}.{hashString}.resample-{WrappedX}-{WrappedY}-{texture.Padding.X}-{texture.Padding.Y}.png");
								File.Delete(path);
								dump.Save(path, System.Drawing.Imaging.ImageFormat.Png);
							}
						}
					}

					if (scaledDimensions != newSize) {
						// This should be incredibly rare - we very rarely need to scale back down.
						using (var filtered = Textures.CreateBitmap(bitmapData, scaledDimensions, PixelFormat.Format32bppArgb)) {
							using (var resized = filtered.Resize(newSize, System.Drawing.Drawing2D.InterpolationMode.Bicubic)) {
								var resizedData = resized.LockBits(new Bounds(resized), ImageLockMode.ReadOnly, resized.PixelFormat);

								try {
									bitmapData = new int[resized.Width * resized.Height];
									var dataSize = resizedData.Stride * resizedData.Height;
									var dataPtr = resizedData.Scan0;
									var widthSize = resizedData.Width;

									var dataBytes = new byte[dataSize];
									int offsetSource = 0;
									int offsetDest = 0;
									foreach (int y in 0..resizedData.Height) {
										Marshal.Copy(dataPtr + offsetSource, bitmapData, offsetDest, widthSize);
										offsetSource += resizedData.Stride;
										offsetDest += widthSize;
									}
								}
								finally {
									resized.UnlockBits(resizedData);
								}
							}
						}
					}

					// TODO : We can technically allocate the block padding before the scaling phase, and pass it a stride
					// so it will just ignore the padding areas. That would be more efficient than this.
					var requireBlockPadding = new Vector2B(
						newSize.X >= 4 && !BlockCompress.IsBlockMultiple(newSize.X),
						newSize.Y >= 4 && !BlockCompress.IsBlockMultiple(newSize.Y)
					) & Config.Resample.UseBlockCompression;

					if (requireBlockPadding.Any) {
						var blockPaddedSize = newSize + new Vector2I(3, 3);
						blockPaddedSize.X &= ~3;
						blockPaddedSize.Y &= ~3;

						var newBuffer = new int[blockPaddedSize.Area];

						int y;
						for (y = 0; y < newSize.Y; ++y) {
							int newBufferOffset = y * blockPaddedSize.X;
							int bitmapOffset = y * newSize.X;
							int x;
							for (x = 0; x < newSize.X; ++x) {
								newBuffer[newBufferOffset + x] = bitmapData[bitmapOffset + x];
							}
							int lastX = x - 1;
							for (; x < blockPaddedSize.X; ++x) {
								newBuffer[newBufferOffset + x] = bitmapData[bitmapOffset + lastX];
							}
						}
						int lastY = y - 1;
						int sourceOffset = lastY * newSize.X;
						for (; y < blockPaddedSize.Y; ++y) {
							int newBufferOffset = y * blockPaddedSize.X;
							for (int x = 0; x < blockPaddedSize.X; ++x) {
								newBuffer[newBufferOffset + x] = newBuffer[sourceOffset + x];
							}
						}

						bitmapData = newBuffer;
						texture.BlockPadding = blockPaddedSize - newSize;
						newSize = blockPaddedSize;
					}

					// Check for special cases
					bool HasAlpha = true;
					bool IsPunchThroughAlpha = false;
					bool IsMasky = false;
					bool hasR = true;
					bool hasG = true;
					bool hasB = true;
					{
						const int MaxShades = 256;
						var alpha = stackalloc int[MaxShades];
						var blue = stackalloc int[MaxShades];
						var green = stackalloc int[MaxShades];
						var red = stackalloc int[MaxShades];

						int idx = 0;
						foreach (var color in bitmapData) {
							var aValue = color.ExtractByte(24);
							if (aValue == 0) {
								// Clear out all other colors for alpha of zero.
								bitmapData[idx] = 0;
							}
							alpha[aValue]++;
							blue[color.ExtractByte(16)]++;
							green[color.ExtractByte(8)]++;
							red[color.ExtractByte(0)]++;

							++idx;
						}

						hasR = red[0] != bitmapData.Length;
						hasG = green[0] != bitmapData.Length;
						hasB = blue[0] != bitmapData.Length;

						//Debug.WarningLn($"Punch-through Alpha: {bitmapData.Length}");
						IsPunchThroughAlpha = IsMasky = ((alpha[0] + alpha[MaxShades - 1]) == bitmapData.Length);
						HasAlpha = (alpha[MaxShades - 1] != bitmapData.Length);

						if (HasAlpha && !IsPunchThroughAlpha) {
							var alphaDeviation = Statistics.StandardDeviation(alpha, MaxShades, 1, MaxShades - 2);
							IsMasky = alphaDeviation < Config.Resample.BlockHardAlphaDeviationThreshold;
						}

						bool grayscale = true;
						foreach (int i in 0..256) {
							if (blue[i] != green[i] || green[i] != red[i]) {
								grayscale = false;
								break;
							}
						}
						if (grayscale) {
							//Debug.WarningLn($"Grayscale: {bitmapData.Length}");
						}
					}

					bool useBlockCompression = Config.Resample.UseBlockCompression && BlockCompress.IsBlockMultiple(newSize);

					if (useBlockCompression) {
						BlockCompress.Compress(
							data: ref bitmapData,
							format: ref spriteFormat,
							dimensions: newSize,
							HasAlpha: HasAlpha,
							IsPunchThroughAlpha: IsPunchThroughAlpha,
							IsMasky: IsMasky,
							HasR: hasR,
							HasG: hasG,
							HasB: hasB
						);
					}

					try {
						Cache.Save(cachePath, newSize, spriteFormat, wrapped, texture.Padding, texture.BlockPadding, bitmapData);
					}
					catch { }
				}

				ManagedTexture2D CreateTexture(int[] data) {
					if (input.Reference.GraphicsDevice.IsDisposed) {
						return null;
					}
					var newTexture = new ManagedTexture2D(
						texture: texture,
						reference: input.Reference,
						dimensions: newSize,
						format: spriteFormat
					);
					newTexture.SetDataEx(data);
					return newTexture;
				}

				bool isAsync = Config.AsyncScaling.Enabled && async;
				if (isAsync && !Config.RendererSupportsAsyncOps) {
					var reference = input.Reference;
					Action asyncCall = () => {
						if (reference.IsDisposed) {
							return;
						}
						ManagedTexture2D newTexture = null;
						try {
							newTexture = CreateTexture(bitmapData);
							texture.Texture = newTexture;
							texture.Finish();
						}
						catch (Exception ex) {
							ex.PrintError();
							if (newTexture != null) {
								newTexture.Dispose();
							}
							texture.Dispose();
						}
					};
					ScaledTexture.AddPendingAction(asyncCall);
					return null;
				}
				else {
					ManagedTexture2D newTexture = null;
					try {
						newTexture = CreateTexture(bitmapData);
						if (isAsync) {
							texture.Texture = newTexture;
							texture.Finish();
						}
						output = newTexture;
					}
					catch (Exception ex) {
						ex.PrintError();
						if (newTexture != null) {
							newTexture.Dispose();
						}
					}
				}
			}
			catch (Exception ex) {
				ex.PrintError();
			}

			//TextureCache.Add(hash, output);
			return output;
		}
	}
}
