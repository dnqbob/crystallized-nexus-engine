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
using OpenRA.Mods.Common.Graphics;
using OpenRA.Primitives;
using OpenRA.Traits;

namespace OpenRA.Mods.Common.Traits
{
	[TraitLocation(SystemActors.World | SystemActors.EditorWorld)]
	[Desc("Renders screenspace refraction for AreaBeam projectiles.")]
	public class AreaBeamDistortionRendererInfo : TraitInfo
	{
		public override object Create(ActorInitializer init) { return new AreaBeamDistortionRenderer(); }
	}

	public sealed class AreaBeamDistortionRenderer : IRenderPostProcessPass, INotifyActorDisposing
	{
		readonly struct Beam
		{
			public readonly float3 Start;
			public readonly float3 End;
			public readonly float Width;
			public readonly float Distortion;
			public readonly float WaveScale;
			public readonly float WaveSpeed;
			public readonly float EdgeSoftness;
			public readonly WPos BeamPos;
			public readonly WVec BeamLength;
			public readonly BeamRenderableShape BeamShape;
			public readonly WDist BeamWidth;
			public readonly Color BeamColor;
			public readonly float BeamEdgeSoftness;

			public Beam(float3 start, float3 end, float width, float distortion, float waveScale, float waveSpeed, float edgeSoftness,
				WPos beamPos, WVec beamLength, BeamRenderableShape beamShape, WDist beamWidth, Color beamColor, float beamEdgeSoftness)
			{
				Start = start;
				End = end;
				Width = width;
				Distortion = distortion;
				WaveScale = waveScale;
				WaveSpeed = waveSpeed;
				EdgeSoftness = edgeSoftness;
				BeamPos = beamPos;
				BeamLength = beamLength;
				BeamShape = beamShape;
				BeamWidth = beamWidth;
				BeamColor = beamColor;
				BeamEdgeSoftness = beamEdgeSoftness;
			}
		}

		readonly Renderer renderer;
		readonly IShader shader;
		readonly IVertexBuffer<RenderPostProcessPassTexturedVertex> buffer;
		readonly RenderPostProcessPassTexturedVertex[] vertices = new RenderPostProcessPassTexturedVertex[6];
		readonly System.Collections.Generic.List<Beam> beams = [];

		public AreaBeamDistortionRenderer()
		{
			renderer = Game.Renderer;
			var bindings = new RenderPostProcessPassTexturedShaderBindings("areabeamdistortion");
			shader = renderer.CreateShader(bindings);
			buffer = renderer.CreateVertexBuffer(bindings, vertices, true);
		}

		public void Draw(float3 start, float3 end, float width, float distortion, float waveScale, float waveSpeed, float edgeSoftness,
			WPos beamPos, WVec beamLength, BeamRenderableShape beamShape, WDist beamWidth, Color beamColor, float beamEdgeSoftness)
		{
			if (distortion <= 0f || width <= 0f)
				return;

			var beam = new Beam(start, end, width, distortion, waveScale, waveSpeed, edgeSoftness,
				beamPos, beamLength, beamShape, beamWidth, beamColor, beamEdgeSoftness);
			foreach (var queuedBeam in beams)
				if (SameBeam(queuedBeam, beam))
					return;

			beams.Add(beam);
		}

		static bool SameBeam(Beam a, Beam b)
		{
			return a.Start == b.Start && a.End == b.End && a.Width == b.Width && a.Distortion == b.Distortion &&
				a.WaveScale == b.WaveScale && a.WaveSpeed == b.WaveSpeed && a.EdgeSoftness == b.EdgeSoftness &&
				a.BeamPos == b.BeamPos && a.BeamLength == b.BeamLength && a.BeamShape == b.BeamShape &&
				a.BeamWidth == b.BeamWidth && a.BeamColor == b.BeamColor && a.BeamEdgeSoftness == b.BeamEdgeSoftness;
		}

		PostProcessPassType IRenderPostProcessPass.Type => PostProcessPassType.AfterWorld;
		bool IRenderPostProcessPass.Enabled => beams.Count > 0;

		void IRenderPostProcessPass.Draw(WorldRenderer wr)
		{
			var scroll = wr.Viewport.TopLeft;
			var size = renderer.WorldFrameBufferSize;
			var widthScale = 2f / (renderer.WorldDownscaleFactor * size.Width);
			var heightScale = 2f / (renderer.WorldDownscaleFactor * size.Height);

			shader.SetVec("Scroll", scroll.X, scroll.Y);
			shader.SetVec("p1", widthScale, heightScale);
			shader.SetVec("p2", -1, -1);
			shader.SetVec("Time", Game.LocalTick);
			shader.SetTexture("SourceTexture", Game.Renderer.GetRenderBufferSnapshot());
			shader.PrepareRender();

			foreach (var beam in beams)
			{
				var delta = beam.End - beam.Start;
				var length = delta.XY.Length;
				if (length <= 0f)
					continue;

				var direction = delta.XY / length;
				var normal = new float2(-direction.Y, direction.X);
				var corner = normal * (beam.Width / 2f);

				vertices[0] = Vertex(beam.Start, beam.Start - corner, 0f, -1f);
				vertices[1] = Vertex(beam.Start, beam.Start + corner, 0f, 1f);
				vertices[2] = Vertex(beam.Start, beam.End + corner, 1f, 1f);
				vertices[3] = Vertex(beam.Start, beam.End + corner, 1f, 1f);
				vertices[4] = Vertex(beam.Start, beam.End - corner, 1f, -1f);
				vertices[5] = Vertex(beam.Start, beam.Start - corner, 0f, -1f);

				buffer.SetData(vertices, 6);
				shader.SetVec("Pos", beam.Start.X, beam.Start.Y);
				shader.SetVec("Normal", normal.X, normal.Y);
				shader.SetVec("Distortion", beam.Distortion);
				shader.SetVec("WaveScale", beam.WaveScale);
				shader.SetVec("WaveSpeed", beam.WaveSpeed);
				shader.SetVec("EdgeSoftness", Math.Max(0.001f, beam.EdgeSoftness));
				renderer.DrawBatch(buffer, shader, 0, 6, PrimitiveType.TriangleList);
			}

			Game.Renderer.Flush();
			foreach (var beam in beams)
				new BeamRenderable(beam.BeamPos, 0, beam.BeamLength, beam.BeamShape, beam.BeamWidth,
					beam.BeamColor, beam.BeamEdgeSoftness).Render(wr);

			beams.Clear();
		}

		static RenderPostProcessPassTexturedVertex Vertex(float3 origin, float3 pos, float s, float t)
		{
			return new RenderPostProcessPassTexturedVertex(pos.X - origin.X, pos.Y - origin.Y, s, t);
		}

		void INotifyActorDisposing.Disposing(Actor self)
		{
			buffer.Dispose();
			shader.Dispose();
		}
	}
}
