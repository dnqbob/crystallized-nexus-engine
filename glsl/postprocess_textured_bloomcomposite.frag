#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D SourceTexture;
uniform float Strength;

in vec2 vTexCoord;
out vec4 fragColor;

void main()
{
	// SourceTexture is the (blurred) glow buffer. Sprites were drawn with
	// premultiplied alpha, so glow.rgb is already premultiplied. The host
	// has bound this pass with additive blending (dst = dst + src), so we
	// just emit Strength-scaled premultiplied RGB and a zero alpha (alpha
	// channel is irrelevant under additive blend on the world buffer).
	vec4 glow = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy), 0);
	fragColor = vec4(glow.rgb * Strength, 0.0);
}
