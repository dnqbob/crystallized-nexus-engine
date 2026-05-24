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
using OpenRA.Primitives;

namespace OpenRA.Mods.Common.Graphics
{
	public enum BeamRenderableShape { Cylindrical, Flat }
	public class BeamRenderable : IRenderable, IFinalizedRenderable
	{
		readonly WVec length;
		readonly BeamRenderableShape shape;
		readonly WDist width;
		readonly Color color;
		readonly float edgeSoftness;

		public BeamRenderable(
			WPos pos, int zOffset, in WVec length, BeamRenderableShape shape, WDist width, Color color, float edgeSoftness = 0f)
		{
			Pos = pos;
			ZOffset = zOffset;
			this.length = length;
			this.shape = shape;
			this.width = width;
			this.color = color;
			this.edgeSoftness = edgeSoftness;
		}

		public WPos Pos { get; }
		public int ZOffset { get; }
		public bool IsDecoration => true;

		public IRenderable WithZOffset(int newOffset)
		{
			return new BeamRenderable(Pos, newOffset, length, shape, width, color, edgeSoftness);
		}

		public IRenderable OffsetBy(in WVec vec)
		{
			return new BeamRenderable(Pos + vec, ZOffset, length, shape, width, color, edgeSoftness);
		}

		public IRenderable AsDecoration() { return this; }

		public IFinalizedRenderable PrepareRender(WorldRenderer wr) { return this; }
		public void Render(WorldRenderer wr)
		{
			var vecLength = length.Length;
			if (vecLength == 0)
				return;

			if (shape == BeamRenderableShape.Flat)
			{
				var delta = length * width.Length / (2 * vecLength);
				var corner = new WVec(-delta.Y, delta.X, delta.Z);
				var a = wr.Screen3DPosition(Pos - corner);
				var b = wr.Screen3DPosition(Pos + corner);
				var c = wr.Screen3DPosition(Pos + corner + length);
				var d = wr.Screen3DPosition(Pos - corner + length);

				if (edgeSoftness > 0f)
					FillSoftBeam(a, b, c, d, color, edgeSoftness);
				else
					Game.Renderer.WorldRgbaColorRenderer.FillRect(a, b, c, d, color, ignoreWorldTint: true, isBloomSource: true);
			}
			else
			{
				var start = wr.Screen3DPosition(Pos);
				var end = wr.Screen3DPosition(Pos + length);
				var screenWidth = wr.ScreenVector(new WVec(width, WDist.Zero, WDist.Zero))[0];

				if (edgeSoftness > 0f)
					DrawSoftLine(start, end, screenWidth, color, edgeSoftness);
				else
					Game.Renderer.WorldRgbaColorRenderer.DrawLine(start, end, screenWidth, color, ignoreWorldTint: true, isBloomSource: true);
			}
		}

		static void DrawSoftLine(in float3 start, in float3 end, float width, Color color, float edgeSoftness)
		{
			var screenLength = (end - start).XY.Length;
			if (screenLength == 0f)
				return;

			var delta = (end - start) / screenLength;
			var corner = width / 2 * new float3(-delta.Y, delta.X, 0);

			FillSoftBeam(start - corner, start + corner, end + corner, end - corner, color, edgeSoftness);
		}

		static void FillSoftBeam(in float3 a, in float3 b, in float3 c, in float3 d, Color color, float edgeSoftness)
		{
			var softness = Math.Min(0.49f, Math.Max(0f, edgeSoftness));
			if (softness <= 0f)
			{
				Game.Renderer.WorldRgbaColorRenderer.FillRect(a, b, c, d, color, ignoreWorldTint: true, isBloomSource: true);
				return;
			}

			var transparent = Color.FromArgb(0, color);
			var leftStart = float3.Lerp(a, b, softness);
			var leftEnd = float3.Lerp(d, c, softness);
			var rightStart = float3.Lerp(a, b, 1f - softness);
			var rightEnd = float3.Lerp(d, c, 1f - softness);
			var renderer = Game.Renderer.WorldRgbaColorRenderer;

			renderer.FillRect(a, leftStart, leftEnd, d, transparent, color, color, transparent,
				ignoreWorldTint: true, isBloomSource: true);
			renderer.FillRect(leftStart, rightStart, rightEnd, leftEnd, color, color, color, color,
				ignoreWorldTint: true, isBloomSource: true);
			renderer.FillRect(rightStart, b, c, rightEnd, color, transparent, transparent, color,
				ignoreWorldTint: true, isBloomSource: true);
		}

		public void RenderDebugGeometry(WorldRenderer wr) { }
		public Rectangle ScreenBounds(WorldRenderer wr) { return Rectangle.Empty; }
	}
}
