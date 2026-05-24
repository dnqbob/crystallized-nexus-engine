#region Copyright & License Information
/*
 * Crystallized Nexus Mod
 * Owns the screenspace shader passes that blur the glow FBO and additively
 * composite the result onto the world FBO. The glow FBO itself is owned by
 * Renderer (paired with a ping-pong buffer for the separable Gaussian).
 */
#endregion

using System;

namespace OpenRA.Graphics
{
	sealed class BloomGlowRenderer : IDisposable
	{
		readonly Renderer renderer;
		readonly IShader blurShader;
		readonly IShader compositeShader;
		readonly IVertexBuffer<RenderPostProcessPassTexturedVertex> buffer;
		readonly RenderPostProcessPassTexturedVertex[] vertices = new RenderPostProcessPassTexturedVertex[6];

		public BloomGlowRenderer(Renderer renderer)
		{
			this.renderer = renderer;
			var blurBindings = new RenderPostProcessPassTexturedShaderBindings("bloomblur");
			var compositeBindings = new RenderPostProcessPassTexturedShaderBindings("bloomcomposite");
			blurShader = renderer.CreateShader(blurBindings);
			compositeShader = renderer.CreateShader(compositeBindings);
			buffer = renderer.CreateVertexBuffer(blurBindings, vertices, true);
		}

		public void Blur(WorldRenderer wr, ITexture source, IFrameBuffer dest, bool horizontal)
		{
			dest.Bind();
			SetupQuad(wr, blurShader);
			blurShader.SetTexture("SourceTexture", source);
			blurShader.SetVec("BlurDirection", horizontal ? 1f : 0f, horizontal ? 0f : 1f);
			DrawQuad(blurShader, BlendMode.None);
			dest.Unbind();
		}

		public void Composite(WorldRenderer wr, IFrameBuffer source, float strength)
		{
			// Caller has bound the world buffer; additive blend folds the glow
			// halo on top of the existing world colour (dst = dst + src).
			SetupQuad(wr, compositeShader);
			compositeShader.SetTexture("SourceTexture", source.Texture);
			compositeShader.SetVec("Strength", strength);
			DrawQuad(compositeShader, BlendMode.Additive);
		}

		void SetupQuad(WorldRenderer wr, IShader shader)
		{
			var topLeft = wr.Viewport.TopLeft;
			var bottomRight = wr.Viewport.BottomRight;

			vertices[0] = new RenderPostProcessPassTexturedVertex(topLeft.X, topLeft.Y, 0, 0);
			vertices[1] = new RenderPostProcessPassTexturedVertex(bottomRight.X, topLeft.Y, 1, 0);
			vertices[2] = new RenderPostProcessPassTexturedVertex(bottomRight.X, bottomRight.Y, 1, 1);
			vertices[3] = new RenderPostProcessPassTexturedVertex(bottomRight.X, bottomRight.Y, 1, 1);
			vertices[4] = new RenderPostProcessPassTexturedVertex(topLeft.X, bottomRight.Y, 0, 1);
			vertices[5] = new RenderPostProcessPassTexturedVertex(topLeft.X, topLeft.Y, 0, 0);

			buffer.SetData(vertices, 6);

			var size = renderer.WorldFrameBufferSize;
			var width = 2f / (renderer.WorldDownscaleFactor * size.Width);
			var height = 2f / (renderer.WorldDownscaleFactor * size.Height);
			shader.SetVec("Scroll", topLeft.X, topLeft.Y);
			shader.SetVec("p1", width, height);
			shader.SetVec("p2", -1, -1);
		}

		void DrawQuad(IShader shader, BlendMode blendMode)
		{
			shader.PrepareRender();
			renderer.Context.SetBlendMode(blendMode);
			renderer.DrawBatch(buffer, shader, 0, 6, PrimitiveType.TriangleList);
			renderer.Context.SetBlendMode(BlendMode.None);
		}

		public void Dispose()
		{
			buffer?.Dispose();
			blurShader.Dispose();
			compositeShader.Dispose();
		}
	}
}
