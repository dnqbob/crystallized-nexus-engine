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
using OpenRA.Graphics;
using OpenRA.Mods.Common.Traits;
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public class AreaBeamDistortionRenderable : IRenderable, IFinalizedRenderable
	{
		readonly AreaBeamDistortionRenderer renderer;
		readonly WVec length;
		readonly WDist width;
		readonly BeamRenderableShape shape;
		readonly WDist beamWidth;
		readonly Color color;
		readonly float beamEdgeSoftness;
		readonly float distortion;
		readonly float waveScale;
		readonly float waveSpeed;
		readonly float edgeSoftness;

		public AreaBeamDistortionRenderable(AreaBeamDistortionRenderer renderer, WPos pos, int zOffset, in WVec length,
			WDist width, BeamRenderableShape shape, WDist beamWidth, Color color, float beamEdgeSoftness,
			float distortion, float waveScale, float waveSpeed, float edgeSoftness)
		{
			this.renderer = renderer;
			Pos = pos;
			ZOffset = zOffset;
			this.length = length;
			this.width = width;
			this.shape = shape;
			this.beamWidth = beamWidth;
			this.color = color;
			this.beamEdgeSoftness = beamEdgeSoftness;
			this.distortion = distortion;
			this.waveScale = waveScale;
			this.waveSpeed = waveSpeed;
			this.edgeSoftness = edgeSoftness;
		}

		public WPos Pos { get; }
		public int ZOffset { get; }
		public bool IsDecoration => false;

		public IRenderable WithZOffset(int newOffset)
		{
			return new AreaBeamDistortionRenderable(renderer, Pos, newOffset, length, width, shape, beamWidth, color,
				beamEdgeSoftness, distortion, waveScale, waveSpeed, edgeSoftness);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new AreaBeamDistortionRenderable(renderer, Pos + vec, ZOffset, length, width, shape, beamWidth, color,
				beamEdgeSoftness, distortion, waveScale, waveSpeed, edgeSoftness);
		}

		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }

		public void Render(WorldRenderer wr)
		{
			if (length.Length == 0)
				return;

			var start = wr.Screen3DPxPosition(Pos);
			var end = wr.Screen3DPxPosition(Pos + length);
			var screenWidth = Math.Abs(wr.ScreenVector(new WVec(width, WDist.Zero, WDist.Zero))[0]);
			renderer.Draw(start, end, screenWidth, distortion, waveScale, waveSpeed, edgeSoftness,
				Pos, length, shape, beamWidth, color, beamEdgeSoftness);
		}

		public void RenderDebugGeometry(WorldRenderer wr)
		{
			var start = wr.Screen3DPxPosition(Pos);
			var end = wr.Screen3DPxPosition(Pos + length);
			var bounds = ScreenBounds(wr);
			Game.Renderer.RgbaColorRenderer.DrawRect(
				new float3(bounds.Left, bounds.Top, start.Z),
				new float3(bounds.Right, bounds.Bottom, end.Z),
				1,
				Color.Red);
		}

		public Rectangle ScreenBounds(WorldRenderer wr)
		{
			var start = wr.Screen3DPxPosition(Pos);
			var end = wr.Screen3DPxPosition(Pos + length);
			var screenWidth = Math.Abs(wr.ScreenVector(new WVec(width, WDist.Zero, WDist.Zero))[0]);
			var padding = (int)Math.Ceiling(screenWidth / 2 + Math.Abs(distortion) + 2);
			var left = (int)Math.Floor(Math.Min(start.X, end.X)) - padding;
			var top = (int)Math.Floor(Math.Min(start.Y, end.Y)) - padding;
			var right = (int)Math.Ceiling(Math.Max(start.X, end.X)) + padding;
			var bottom = (int)Math.Ceiling(Math.Max(start.Y, end.Y)) + padding;
			return Rectangle.FromLTRB(left, top, right, bottom);
		}
	}
}
