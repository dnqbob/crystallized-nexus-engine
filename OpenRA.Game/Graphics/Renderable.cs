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
using OpenRA.Primitives;

namespace OpenRA.Graphics
{
	public interface IRenderable
	{
		WPos Pos { get; }
		int ZOffset { get; }
		bool IsDecoration { get; }

		// Marks the renderable as the actor's drop shadow. Default false.
		bool IsShadow => false;

		IRenderable WithZOffset(int newOffset);
		IRenderable OffsetBy(in WVec offset);
		IRenderable AsDecoration();

		// Marks the renderable as a drop shadow.
		// Default: no-op (most renderables cannot be shadows).
		IRenderable AsShadow() => this;

		IFinalizedRenderable PrepareRender(WorldRenderer wr);
	}

	public interface IPalettedRenderable : IRenderable
	{
		PaletteReference Palette { get; }
		IPalettedRenderable WithPalette(PaletteReference newPalette);
	}

	[Flags]
	public enum TintModifiers
	{
		None = 0,
		IgnoreWorldTint = 1,
		ReplaceColor = 2,

		// Marks the sprite as a bloom source: the combined shader's glow
		// extract pass treats its non-transparent pixels as glow, even when
		// the sprite is RGBA (no palette index to test). Opt-in via the
		// "BloomGlow: True" sequence field; hardcoded true by the voxel
		// renderer for its dedicated FullBrightSprite.
		BloomGlow = 4
	}

	public interface IModifyableRenderable : IRenderable
	{
		float Alpha { get; }
		float3 Tint { get; }
		TintModifiers TintModifiers { get; }

		IModifyableRenderable WithAlpha(float newAlpha);
		IModifyableRenderable WithTint(in float3 newTint, TintModifiers newTintModifiers);
	}

	public interface IFinalizedRenderable
	{
		// Mirrors IRenderable.IsShadow after PrepareRender.
		bool IsShadow => false;
		void Render(WorldRenderer wr);
		void RenderDebugGeometry(WorldRenderer wr);
		Rectangle ScreenBounds(WorldRenderer wr);
	}
}
