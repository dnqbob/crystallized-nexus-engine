#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D SourceTexture;
uniform vec2 Normal;
uniform float Distortion;
uniform float WaveScale;
uniform float WaveSpeed;
uniform float EdgeSoftness;
uniform float Time;

in vec2 vTexCoord;
out vec4 fragColor;

void main()
{
	float edge = 1.0 - abs(vTexCoord.y);
	if (edge <= 0.0)
		discard;

	float sideFade = smoothstep(0.0, EdgeSoftness, edge);
	float endFade = min(smoothstep(0.0, 0.08, vTexCoord.x), smoothstep(0.0, 0.08, 1.0 - vTexCoord.x));
	float falloff = sideFade * endFade;

	float phase = Time * WaveSpeed + vTexCoord.x * WaveScale;
	float crossWave = sin(phase) + 0.18 * sin(phase * 1.7 + 1.2);
	vec2 offset = Normal * crossWave * Distortion * falloff;

	vec4 src = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy), 0);
	vec4 refracted = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy + offset), 0);
	fragColor = mix(src, refracted, falloff);
}
