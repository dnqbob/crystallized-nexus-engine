#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D SourceTexture;
uniform vec2 BlurDirection;

in vec2 vTexCoord;
out vec4 fragColor;

// Separable 13-tap Gaussian (sigma ~3.5 px). Wide enough for a glow halo
// to fall off out to ~6 pixels.
void main()
{
	ivec2 px = ivec2(gl_FragCoord.xy);
	ivec2 dir = ivec2(BlurDirection);

	vec4 c = texelFetch(SourceTexture, px, 0) * 0.122;
	c += texelFetch(SourceTexture, px + dir, 0) * 0.117;
	c += texelFetch(SourceTexture, px - dir, 0) * 0.117;
	c += texelFetch(SourceTexture, px + dir * 2, 0) * 0.103;
	c += texelFetch(SourceTexture, px - dir * 2, 0) * 0.103;
	c += texelFetch(SourceTexture, px + dir * 3, 0) * 0.084;
	c += texelFetch(SourceTexture, px - dir * 3, 0) * 0.084;
	c += texelFetch(SourceTexture, px + dir * 4, 0) * 0.063;
	c += texelFetch(SourceTexture, px - dir * 4, 0) * 0.063;
	c += texelFetch(SourceTexture, px + dir * 5, 0) * 0.044;
	c += texelFetch(SourceTexture, px - dir * 5, 0) * 0.044;
	c += texelFetch(SourceTexture, px + dir * 6, 0) * 0.028;
	c += texelFetch(SourceTexture, px - dir * 6, 0) * 0.028;
	fragColor = c;
}
