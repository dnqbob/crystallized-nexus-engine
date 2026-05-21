#version {VERSION}
#ifdef GL_ES
precision mediump float;
#endif

uniform sampler2D SourceTexture;
uniform float Mode;
uniform float Contrast;
uniform float Saturation;
uniform float Vignette;
uniform vec3 Tint;
uniform float Grain;
uniform float Time;

out vec4 fragColor;

// Precision-safe hash (no sin, no large-coordinate blow-up).
float hash(vec2 p)
{
	vec3 p3 = fract(vec3(p.xyx) * 0.1031);
	p3 += dot(p3, p3.yzx + 33.33);
	return fract((p3.x + p3.y) * p3.z);
}

void main()
{
	ivec2 px = ivec2(gl_FragCoord.xy);
	vec4 src = texelFetch(SourceTexture, px, 0);
	fragColor = src;

	// Mode 0 = Off: pass through untouched (strict-driver-safe early write above).
	if (Mode < 0.5)
		return;

	vec3 c = src.rgb;

	// Contrast around mid-grey.
	c = (c - 0.5) * Contrast + 0.5;

	// Saturation lerp against luminance.
	float luma = dot(c, vec3(0.299, 0.587, 0.114));
	c = mix(vec3(luma), c, Saturation);

	// Atmospheric tint multiply.
	c *= Tint;

	// Radial vignette.
	vec2 res = vec2(textureSize(SourceTexture, 0));
	vec2 uv = gl_FragCoord.xy / res;
	float d = distance(uv, vec2(0.5));
	float vig = 1.0 - Vignette * smoothstep(0.25, 0.85, d);
	c *= vig;

	// Subtle film grain. Time is folded into a small bounded jitter so the
	// hash never receives huge coordinates (which caused diagonal moiré bands).
	if (Grain > 0.0)
	{
		float tw = fract(Time * 0.0137);
		float n = hash(gl_FragCoord.xy + tw * vec2(37.0, 17.0));
		c += (n - 0.5) * Grain;
	}

	fragColor = vec4(clamp(c, 0.0, 1.0), src.a);
}
