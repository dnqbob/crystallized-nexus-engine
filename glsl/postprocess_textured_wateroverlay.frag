#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D SourceTexture;
uniform vec2 WorldScroll;
uniform float Time;
uniform float Intensity;

uniform vec3 ClearTint;
uniform float ClearAlpha;
uniform float ClearShimmer;
uniform float ClearDistortion;
uniform float ClearWaveScale;
uniform float ClearWaveSpeed;

uniform vec3 StormTint;
uniform float StormAlpha;
uniform float StormShimmer;
uniform float StormDistortion;
uniform float StormPulse;
uniform float StormPulseSpeed;
uniform float StormWaveScale;
uniform float StormWaveSpeed;

in vec2 vTexCoord;
out vec4 fragColor;

float hash(vec2 p)
{
	p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
	return fract(sin(p.x + p.y) * 43758.5453);
}

float noise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(hash(i + vec2(0.0, 0.0)), hash(i + vec2(1.0, 0.0)), u.x),
		mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x),
		u.y);
}

float fbm(vec2 p)
{
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++)
	{
		v += a * noise(p);
		p = p * 2.03 + vec2(17.3, 29.1);
		a *= 0.5;
	}
	return v;
}

void main()
{
	// Always write fragColor first so all control-flow paths satisfy strict drivers.
	vec4 src = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy), 0);
	fragColor = src;

	// UV sentinel (2, 2): cliff-block passthrough - return with the pixel we already wrote.
	if (vTexCoord.x > 1.5)
		return;

	float diamond = abs(vTexCoord.x) + abs(vTexCoord.y);
	if (diamond > 1.0)
		discard;

	vec2 world = gl_FragCoord.xy + WorldScroll;

	float distortion = mix(ClearDistortion, StormDistortion, Intensity);

	vec2 clearP = world * ClearWaveScale;
	float clearT = Time * ClearWaveSpeed;
	vec2 stormP = world * StormWaveScale;
	float stormT = Time * StormWaveSpeed;

	vec2 clearQ = vec2(
		fbm(clearP + vec2(0.0, 0.0) + 0.12 * clearT),
		fbm(clearP + vec2(5.2, 1.3) - 0.10 * clearT));
	vec2 clearR = vec2(
		fbm(clearP + 3.0 * clearQ + vec2(1.7, 9.2) + 0.15 * clearT),
		fbm(clearP + 3.0 * clearQ + vec2(8.3, 2.8) - 0.13 * clearT));
	float clearField = fbm(clearP * 1.7 + 2.0 * clearQ + 0.20 * clearT);

	vec2 stormQ = vec2(
		fbm(stormP + vec2(0.0, 0.0) + 0.12 * stormT),
		fbm(stormP + vec2(5.2, 1.3) - 0.10 * stormT));
	vec2 stormR = vec2(
		fbm(stormP + 3.0 * stormQ + vec2(1.7, 9.2) + 0.15 * stormT),
		fbm(stormP + 3.0 * stormQ + vec2(8.3, 2.8) - 0.13 * stormT));
	float stormField = fbm(stormP * 1.7 + 2.0 * stormQ + 0.20 * stormT);

	vec2 rr = mix(clearR, stormR, Intensity);
	vec2 wave = (rr - 0.5) * 2.0;
	float field = mix(clearField, stormField, Intensity);

	vec4 base = texelFetch(SourceTexture, ivec2(gl_FragCoord.xy + wave * distortion), 0);

	// Clear shimmer + tint (driven by the noise field, not a sine)
	float clearShimmer = ClearShimmer * (field - 0.5) * 2.0;
	vec3 clearOverlay = base.rgb * ClearTint + clearShimmer;

	// Storm ionized pulse + shimmer. The slow global breath makes the
	// whole surface visibly respond, while the noise masks keep it watery.
	float breath = 0.5 + 0.5 * sin(Time * StormPulseSpeed);
	breath = breath * breath * (3.0 - 2.0 * breath);
	float localPulse = smoothstep(0.48, 0.92, rr.x);
	float veinPulse = smoothstep(0.52, 0.88, field) * smoothstep(0.18, 0.82, rr.y);
	float pulse = StormPulse * (0.28 + 0.72 * breath) * (0.35 + 0.65 * max(localPulse, veinPulse));
	float stormShimmer = StormShimmer * (field - 0.5) * 2.0 * (0.65 + 0.35 * breath);
	vec3 stormOverlay = base.rgb * StormTint + pulse * vec3(0.48, 1.0, 0.82) + stormShimmer;

	vec3 overlay = mix(clearOverlay, stormOverlay, Intensity);
	float alpha = mix(ClearAlpha, StormAlpha + StormPulse * 0.18 * breath, Intensity);

	fragColor = vec4(mix(src.rgb, overlay, alpha), src.a);
}
