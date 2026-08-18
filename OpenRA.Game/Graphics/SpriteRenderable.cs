#region Copyright & License Information
/*
 * Copyright (c) The OpenRA Developers and Contributors
 * This file is part of OpenRA, which is free software. It is made
 * available to you under the terms of the GNU General Public License
 * as published by the Free Software Foundation, either version 3 of
 * the License, or (at your option) any later version. For more
 * information, see COPYING.
 */
#endregion

using System;
using System.Collections.Generic;
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public class SpriteRenderable : IPalettedRenderable, IModifyableRenderable, IFinalizedRenderable
	{
		public static readonly IEnumerable<IRenderable> None = [];

		readonly Sprite sprite;
		readonly WPos pos;
		readonly float scale;
		readonly WAngle rotation = WAngle.Zero;
		readonly bool flipY;
		readonly uint fullBrightPaletteRanges;
		readonly float bloomIntensity;
		readonly float rotationPivot;

		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset, PaletteReference palette, float scale, float alpha,
			float3 tint, TintModifiers tintModifiers, bool isDecoration, WAngle rotation, bool flipY = false, uint fullBrightPaletteRanges = 0,
			bool isShadow = false, float bloomIntensity = 1f, float rotationPivot = 0f)
		{
			this.sprite = sprite;
			this.pos = pos;
			Offset = offset;
			ZOffset = zOffset;
			Palette = palette;
			this.scale = scale;
			this.rotation = rotation;
			this.flipY = flipY;
			this.fullBrightPaletteRanges = fullBrightPaletteRanges;
			this.bloomIntensity = bloomIntensity;
			this.rotationPivot = rotationPivot;
			Tint = tint;
			IsDecoration = isDecoration;
			IsShadow = isShadow;
			TintModifiers = tintModifiers;
			Alpha = alpha;

			// PERF: Remove useless palette assignments for RGBA sprites
			// HACK: This is working around the fact that palettes are defined on traits rather than sequences
			// and can be removed once this has been fixed
			if (sprite.Channel == TextureChannel.RGBA && !(palette?.HasColorShift ?? false))
				Palette = null;
		}

		public SpriteRenderable(Sprite sprite, WPos pos, WVec offset, int zOffset, PaletteReference palette, float scale, float alpha,
			float3 tint, TintModifiers tintModifiers, bool isDecoration)
			: this(sprite, pos, offset, zOffset, palette, scale, alpha, tint, tintModifiers, isDecoration, WAngle.Zero) { }

		public WPos Pos => pos + Offset;
		public WVec Offset { get; }
		public PaletteReference Palette { get; }
		public int ZOffset { get; }
		public bool IsDecoration { get; }
		public bool IsShadow { get; }

		public float Alpha { get; }
		public float3 Tint { get; }
		public TintModifiers TintModifiers { get; }

		public IPalettedRenderable WithPalette(PaletteReference newPalette)
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, newPalette, scale, Alpha, Tint, TintModifiers,
				IsDecoration, rotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		public IRenderable WithZOffset(int newOffset)
		{
			return new SpriteRenderable(sprite, pos, Offset, newOffset, Palette, scale, Alpha, Tint, TintModifiers,
				IsDecoration, rotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new SpriteRenderable(sprite, pos + vec, Offset, ZOffset, Palette, scale, Alpha, Tint, TintModifiers,
				IsDecoration, rotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		public IRenderable AsDecoration()
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, Alpha, Tint, TintModifiers,
				true, rotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		public IRenderable AsShadow()
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, Alpha, Tint, TintModifiers,
				IsDecoration, rotation, flipY, fullBrightPaletteRanges, true, bloomIntensity, rotationPivot);
		}

		public IModifyableRenderable WithAlpha(float newAlpha)
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, newAlpha, Tint, TintModifiers,
				IsDecoration, rotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		public IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers)
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, Alpha, newTint, newTintModifiers,
				IsDecoration, rotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		public SpriteRenderable WithYFlip()
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, Alpha, Tint, TintModifiers,
				IsDecoration, rotation, true, fullBrightPaletteRanges, IsShadow, bloomIntensity, rotationPivot);
		}

		// pivot is the distance in screen pixels from the sprite centre down to the point that should stay
		// planted as the sprite tilts, e.g. the base of a tree trunk. Zero rotates around the centre.
		public SpriteRenderable WithRotation(WAngle newRotation, float pivot = 0f)
		{
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, Alpha, Tint, TintModifiers,
				IsDecoration, newRotation, flipY, fullBrightPaletteRanges, IsShadow, bloomIntensity, pivot);
		}

		public SpriteRenderable WithFullBrightOnly(uint paletteRanges, float bloomIntensity = 1f)
		{
			// Fold the per-sequence bloom multiplier (carried on this renderable) into the
			// per-actor one passed by the caller so a single sequence can be dimmed even when
			// the glow comes from the full-bright palette path rather than the BloomGlow flag.
			return new SpriteRenderable(sprite, pos, Offset, ZOffset, Palette, scale, Alpha, Tint,
				TintModifiers | TintModifiers.IgnoreWorldTint, IsDecoration, rotation, flipY, paletteRanges, IsShadow, this.bloomIntensity * bloomIntensity,
				rotationPivot);
		}

		float3 ScreenPosition(WorldRenderer wr)
		{
			var s = 0.5f * scale * sprite.Size;
			var p = wr.Screen3DPxPosition(pos) + wr.ScreenPxOffset(Offset) - new float3((int)s.X, (int)s.Y, s.Z);

			// The quad is rotated about the sprite centre, which drags a point rotationPivot below that
			// centre sideways by rotationPivot * sin(rotation). Undo that here so the point stays planted.
			// This has to happen in screen space: Screen3DPxPosition snaps pos to whole pixels, so routing
			// the correction through a world-space offset would round it away and the sprite would jump a
			// full pixel at a time instead of tilting smoothly.
			if (rotationPivot != 0f && rotation != WAngle.Zero)
				p -= new float3(rotationPivot * (float)Math.Sin(rotation.RendererRadians()), 0, 0);

			return p;
		}

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var wsr = Game.Renderer.WorldSpriteRenderer;
			var ignoreWorldTint = (TintModifiers & TintModifiers.IgnoreWorldTint) != 0;
			var isBloomSource = (TintModifiers & TintModifiers.BloomGlow) != 0;
			var t = Alpha * Tint;
			if (wr.TerrainLighting != null && !ignoreWorldTint)
				t *= wr.TerrainLighting.TintAt(pos);

			// Shader interprets negative alpha as a flag to use the tint colour directly instead of multiplying the sprite colour
			var a = Alpha;
			if ((TintModifiers & TintModifiers.ReplaceColor) != 0)
				a *= -1;

			if (flipY)
				wsr.DrawSprite(sprite, Palette, ScreenPosition(wr) + new float3(0, scale * sprite.Size.Y, 0),
					new float3(scale, -scale, scale), t, a, rotation.RendererRadians(), ignoreWorldTint, fullBrightPaletteRanges, isBloomSource,
					bloomIntensity);
			else
				wsr.DrawSprite(sprite, Palette, ScreenPosition(wr), scale, t, a, rotation.RendererRadians(), ignoreWorldTint, fullBrightPaletteRanges,
					isBloomSource, bloomIntensity);
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var pos = ScreenPosition(wr) + sprite.Offset;
			var tl = wr.Viewport.WorldToViewPx(pos);
			var br = wr.Viewport.WorldToViewPx(pos + sprite.Size);
			if (rotation == WAngle.Zero)
				Game.Renderer.RgbaColorRenderer.DrawRect(tl, br, 1, Color.Red);
			else
				Game.Renderer.RgbaColorRenderer.DrawPolygon(Util.RotateQuad(tl, br - tl, rotation.RendererRadians()), 1, Color.Red);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var screenOffset = ScreenPosition(wr) + sprite.Offset;
			return Util.BoundingRectangle(screenOffset, sprite.Size, rotation.RendererRadians());
		}
	}
}
