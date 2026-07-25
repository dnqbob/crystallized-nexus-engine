#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D SourceTexture;
uniform vec3 Tint;
uniform float GlowStrength;
uniform float BleedStrength;
uniform float ShimmerStrength;
uniform float Distortion;
uniform float WaveScale;
uniform float WaveSpeed;
uniform float PulseSpeed;
uniform float Time;

in vec2 vTexCoord;
out vec4 fragColor;

void main()
{
	// Strict-driver-safe: always write first.
	vec4 src = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy), 0);
	fragColor = src;

	// Radial falloff from cell centre (vTexCoord is -1..1 across the padded quad).
	float r = length(vTexCoord);
	if (r >= 1.0)
		discard;

	// Smooth, soft falloff to avoid hard per-cell circle seams.
	float falloff = pow(1.0 - clamp(r, 0.0, 1.0), 2.0);

	// Animated heat-shimmer: refract the underlying pixels near the crystals.
	// Bias toward the brighter of src/sampled so the refraction never drags a
	// dark fringe over the bright crystals (previous hard halo artifact).
	float phase = Time * WaveSpeed + gl_FragCoord.x * WaveScale + gl_FragCoord.y * WaveScale * 0.6;
	vec2 wave = vec2(sin(phase) + 0.4 * sin(phase * 1.7 + 1.3), cos(phase * 1.3)) * Distortion * falloff;
	vec4 sampled = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy + wave), 0);
	vec3 refracted = max(src.rgb, sampled.rgb);
	vec3 shimmered = mix(src.rgb, refracted, ShimmerStrength * falloff);

	// Self-illumination pulse on the crystal core.
	float pulse = 0.6 + 0.4 * sin(Time * PulseSpeed + (gl_FragCoord.x + gl_FragCoord.y) * 0.05);
	float core = falloff * falloff;
	vec3 emissive = Tint * GlowStrength * core * pulse;

	// Soft coloured ground light-bleed across the whole footprint.
	vec3 bleed = Tint * BleedStrength * falloff;

	fragColor = vec4(shimmered + emissive + bleed, src.a);
}
