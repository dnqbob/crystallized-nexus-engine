#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D Texture0;
uniform sampler2D Texture1;
uniform sampler2D Texture2;
uniform sampler2D Texture3;
uniform sampler2D Texture4;
uniform sampler2D Texture5;
uniform sampler2D Texture6;
uniform sampler2D Texture7;
uniform sampler2D Palette;
uniform sampler2D ColorShifts;

uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;
uniform float DepthTextureScale;
uniform bool EnablePixelArtScaling;

// Glow extract pass: when set, the world is being rendered a second time into
// a dedicated glow FBO. Every fragment that is NOT a full-bright palette pixel
// is discarded; the surviving fragments are written in their pre-world-tint
// colour so the downstream bloom blur reads a consistently "hot" image
// regardless of time of day.
uniform bool GlowExtractOnly;

// Drifting cloud-shadow as part of the world tint. Darkens terrain + tinted
// sprites, automatically skipped for IgnoreWorldTint geometry. Disabled when
// CloudShadowAlpha == 0 (default), so behaviour is unchanged unless fed.
uniform float CloudShadowAlpha;
uniform float CloudShadowScale;
uniform vec2 CloudShadowWind;
uniform float CloudShadowTime;
uniform float CloudShadowCoverage;
uniform float CloudShadowEdge;

// Day/night world tint. Multiplies terrain + tinted sprites, automatically
// skipped for IgnoreWorldTint geometry. Gated by WorldDayTintEnabled so
// renderers that never set it (chrome/editor) are unaffected (the default
// uninitialised uniform is 0 == disabled).
uniform float WorldDayTintEnabled;
uniform vec3 WorldDayTint;

in vec4 vTexCoord;
flat in float vTexPalette;
flat in vec4 vChannelMask;
flat in uint vChannelSampler;
flat in uint vChannelType;
flat in vec4 vDepthMask;
flat in uint vDepthSampler;
in vec4 vTint;
flat in float vIgnoreWorldTint;
flat in float vFullBrightOnly;
flat in float vBloomGlow;
flat in float vBloomIntensity;
flat in uint vFullBrightRanges;
in vec2 vWorldPos;

#ifdef GL_ES
layout(location = 0) out vec4 fragColor;
layout(location = 1) out vec4 shadowColor;
#else
out vec4 fragColor;
out vec4 shadowColor;
#endif

float cloudHash(vec2 p)
{
	p = vec2(dot(p, vec2(127.1, 311.7)), dot(p, vec2(269.5, 183.3)));
	return fract(sin(p.x + p.y) * 43758.5453);
}

float cloudNoise(vec2 p)
{
	vec2 i = floor(p);
	vec2 f = fract(p);
	vec2 u = f * f * (3.0 - 2.0 * f);
	return mix(
		mix(cloudHash(i + vec2(0.0, 0.0)), cloudHash(i + vec2(1.0, 0.0)), u.x),
		mix(cloudHash(i + vec2(0.0, 1.0)), cloudHash(i + vec2(1.0, 1.0)), u.x),
		u.y);
}

float cloudFbm(vec2 p)
{
	float v = 0.0;
	float a = 0.5;
	for (int i = 0; i < 4; i++)
	{
		v += a * cloudNoise(p);
		p = p * 2.03 + vec2(17.3, 29.1);
		a *= 0.5;
	}
	return v;
}

// World-space drifting cloud-shadow darkening factor (1 = lit, <1 = shadowed).
float cloudShadowFactor()
{
	vec2 w = vWorldPos + CloudShadowWind * CloudShadowTime;
	float n = cloudFbm(w * CloudShadowScale);
	float shadow = smoothstep(CloudShadowCoverage, CloudShadowCoverage + CloudShadowEdge, n) * CloudShadowAlpha;
	return 1.0 - shadow;
}

vec3 rgb2hsv(vec3 c)
{
	// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = c.g < c.b ? vec4(c.bg, K.wz) : vec4(c.gb, K.xy);
	vec4 q = c.r < p.x ? vec4(p.xyw, c.r) : vec4(c.r, p.yzx);
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
	// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

float srgb2linear(float c)
{
	// Standard gamma conversion equation: see e.g. https://entropymine.com/imageworsener/srgbformula/
	return c <= 0.04045f ? c / 12.92f : pow((c + 0.055f) / 1.055f, 2.4f);
}

vec4 srgb2linear(vec4 c)
{
	// The SRGB color has pre-multiplied alpha which we must undo before removing the the gamma correction
	return c.a * vec4(srgb2linear(c.r / c.a), srgb2linear(c.g / c.a), srgb2linear(c.b / c.a), 1.0f);
}

float linear2srgb(float c)
{
	// Standard gamma conversion equation: see e.g. https://entropymine.com/imageworsener/srgbformula/
	return c <= 0.0031308 ? c * 12.92f : 1.055f * pow(c, 1.0f / 2.4f) - 0.055f;
}

vec4 linear2srgb(vec4 c)
{
	// The linear color has pre-multiplied alpha which we must undo before applying the the gamma correction
	return c.a * vec4(linear2srgb(c.r / c.a), linear2srgb(c.g / c.a), linear2srgb(c.b / c.a), 1.0f);
}

vec2 Size(uint samplerIndex)
{
	switch (samplerIndex)
	{
		case 7u:
			return vec2(textureSize(Texture7, 0));
		case 6u:
			return vec2(textureSize(Texture6, 0));
		case 5u:
			return vec2(textureSize(Texture5, 0));
		case 4u:
			return vec2(textureSize(Texture4, 0));
		case 3u:
			return vec2(textureSize(Texture3, 0));
		case 2u:
			return vec2(textureSize(Texture2, 0));
		case 1u:
			return vec2(textureSize(Texture1, 0));
		default:
			return vec2(textureSize(Texture0, 0));
	}
}

vec4 Sample(uint samplerIndex, vec2 pos)
{
	switch (samplerIndex)
	{
		case 7u:
			return texture(Texture7, pos);
		case 6u:
			return texture(Texture6, pos);
		case 5u:
			return texture(Texture5, pos);
		case 4u:
			return texture(Texture4, pos);
		case 3u:
			return texture(Texture3, pos);
		case 2u:
			return texture(Texture2, pos);
		case 1u:
			return texture(Texture1, pos);
		default:
			return texture(Texture0, pos);
	}
}

vec4 SamplePalettedBilinear(uint samplerIndex, vec2 coords, vec2 textureSize)
{
	vec2 texPos = (coords * textureSize) - vec2(0.5);
	vec2 interp = fract(texPos);
	vec2 tl = (floor(texPos) + vec2(0.5)) / textureSize;
	vec2 px = 1.0 / textureSize;

	vec4 x1 = Sample(samplerIndex, tl);
	vec4 x2 = Sample(samplerIndex, tl + vec2(px.x, 0.));
	vec4 x3 = Sample(samplerIndex, tl + vec2(0., px.y));
	vec4 x4 = Sample(samplerIndex, tl + px);

	vec4 c1 = texture(Palette, vec2(dot(x1, vChannelMask), vTexPalette));
	vec4 c2 = texture(Palette, vec2(dot(x2, vChannelMask), vTexPalette));
	vec4 c3 = texture(Palette, vec2(dot(x3, vChannelMask), vTexPalette));
	vec4 c4 = texture(Palette, vec2(dot(x4, vChannelMask), vTexPalette));

	return mix(mix(c1, c2, interp.x), mix(c3, c4, interp.x), interp.y);
}

bool IsFullBrightPaletteIndex(float paletteIndex)
{
	float range1Start = float(vFullBrightRanges & 0xffu);
	float range1End = float((vFullBrightRanges >> 8) & 0xffu);
	float range2Start = float((vFullBrightRanges >> 16) & 0xffu);
	float range2End = float((vFullBrightRanges >> 24) & 0xffu);

	return (range1End >= range1Start && paletteIndex >= range1Start && paletteIndex <= range1End) ||
		(range2End >= range2Start && paletteIndex >= range2Start && paletteIndex <= range2End);
}

vec4 ColorShift(vec4 c, float p)
{
	vec4 range = texture(ColorShifts, vec2(0.25, p));
 	vec4 shift = texture(ColorShifts, vec2(0.75, p));

	vec3 hsv = rgb2hsv(srgb2linear(c).rgb);
	if (hsv.r > range.r && range.g >= hsv.r)
		c = linear2srgb(vec4(hsv2rgb(vec3(hsv.r + shift.r, clamp(hsv.g + shift.g, 0.0, 1.0), hsv.b * clamp(shift.b, 0.0, 1.0))), c.a));

	return c;
}

void main()
{
	vec2 coords = vTexCoord.st;
	bool isPaletted = (vChannelType & 0x01u) != 0u;
	bool isColor = vChannelType == 0u;
	bool fullBrightOnly = vFullBrightOnly > 0.5;
	bool fullBright = false;
	float paletteIndex = -1.0;

	vec4 c;
	if (EnablePixelArtScaling)
	{
		vec2 textureSize = Size(vChannelSampler);
		vec2 vUv = coords.st * textureSize;
		vec2 offset = fract(vUv);
		vec2 pixelsPerTexel = vec2(1.0 / dFdx(vUv.x), 1.0 / dFdy(vUv.y));

		// Offset the sampling point to simulate bilinear intepolation in window coordinates instead of texture coordinates
		// https://csantosbh.wordpress.com/2014/01/25/manual-texture-filtering-for-pixelated-games-in-webgl/
		// https://csantosbh.wordpress.com/2014/02/05/automatically-detecting-the-texture-filter-threshold-for-pixelated-magnifications/
		// ik is defined as 1/k from the articles, set to 1/0.7 because it looks good
		float ik = 1.43;
		vec2 interp = clamp(offset * ik * pixelsPerTexel, 0.0, .5) + clamp((offset - 1.0) * ik * pixelsPerTexel + .5, 0.0, .5);
		coords = (floor(coords.st * textureSize) + interp) / textureSize;

		if (isPaletted)
			c = SamplePalettedBilinear(vChannelSampler, coords, textureSize);
	}

	if (!(EnablePixelArtScaling && isPaletted))
	{
		vec4 x = Sample(vChannelSampler, coords);
		vec2 p = vec2(dot(x, vChannelMask), vTexPalette);
		paletteIndex = p.x * 255.0;
		fullBright = isPaletted && IsFullBrightPaletteIndex(paletteIndex);
		if (isPaletted)
			c = texture(Palette, p);
		else if (isColor)
			c = vTexCoord;
		else
			c = x;
	}
	else
	{
		vec4 x = Sample(vChannelSampler, coords);
		paletteIndex = dot(x, vChannelMask) * 255.0;
		fullBright = IsFullBrightPaletteIndex(paletteIndex);
	}

	if (fullBrightOnly && !fullBright)
		discard;

	// Glow extract keeps normal full-bright palette pixels and also allows
	// explicitly marked BloomGlow renderables to contribute all non-transparent
	// pixels. IgnoreWorldTint by itself does not opt sprites into bloom.
	bool isGlowPixel = fullBright || vBloomGlow > 0.5;
	if (GlowExtractOnly && !isGlowPixel)
		discard;

	// Discard any transparent fragments (both color and depth)
	if (c.a == 0.0)
		discard;

	if (!isPaletted && vTexPalette > 0.0)
		c = ColorShift(c, vTexPalette);

	float depth = gl_FragCoord.z;
	if (length(vDepthMask) > 0.0)
	{
		vec4 y = Sample(vDepthSampler, vTexCoord.pq);
		depth = depth + DepthTextureScale * dot(y, vDepthMask);
	}

	if (EnableDepthPreview)
	{
		gl_FragDepth = depth;
		float intensity = 1.0 - clamp(DepthPreviewParams.x * depth - 0.5 * DepthPreviewParams.x - DepthPreviewParams.y + 0.5, 0.0, 1.0);
		fragColor = vec4(vec3(intensity), 1.0);
		shadowColor = vec4(0.0);
	}
	else if (GlowExtractOnly)
	{
		// Glow extract pass: apply only the per-sprite tint (so player colours
		// propagate) but skip world day-tint and cloud-shadow logic. The bloom
		// must read a consistently bright source regardless of time of day; the
		// time-of-day modulation is done in the composite.
		if (vTint.a < 0.0)
			c = vec4(vTint.rgb, -vTint.a);
		else
			c *= vTint;

		c.rgb *= vBloomIntensity;

		gl_FragDepth = depth;
		fragColor = c;
		shadowColor = vec4(0.0);
	}
	else
	{
		// A negative tint alpha indicates that the tint should replace the colour instead of multiplying it
		if (vTint.a < 0.0)
			c = vec4(vTint.rgb, -vTint.a);
		else
		{
			c *= vTint;

			// Cloud shadow behaves like world tint: applied to terrain and
			// tinted sprites, skipped for IgnoreWorldTint geometry. No-op when
			// CloudShadowAlpha == 0 (default).
			if (CloudShadowAlpha > 0.0 && vIgnoreWorldTint < 0.5)
				c.rgb *= cloudShadowFactor();

			// Day/night world tint: same gating as cloud shadow, so
			// IgnoreWorldTint sprites keep their own brightness. Disabled
			// (no-op) unless a world trait pushes WorldDayTintEnabled.
			if (WorldDayTintEnabled > 0.5 && vIgnoreWorldTint < 0.5)
				c.rgb *= WorldDayTint;
		}

		gl_FragDepth = depth;
		fragColor = c;
		shadowColor = vec4(0.0);
	}
}
