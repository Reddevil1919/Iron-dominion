shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;

uniform float clock_phase;
varying flat float born_phase;

void vertex() {
    born_phase = INSTANCE_CUSTOM.x;
}

void fragment() {
    float age = fract(clock_phase - born_phase + 0.0005) * 16.0;
    float across = abs(2.0 * UV.x - 1.0);
    float grit = sin(UV.x * 97.0 + UV.y * 61.0) * sin(UV.x * 53.0 - UV.y * 89.0);
    float edge = (1.0 - smoothstep(0.73, 1.0, across + 0.09 * grit))
        * smoothstep(0.0, 0.088, min(UV.y, 1.0 - UV.y));
    float bars = smoothstep(0.01, 0.68, sin((UV.y * 3.2 + across * 0.23) * TAU));
    ALBEDO = COLOR.rgb * (1.08 - bars * 0.27 + grit * 0.13);
    ALPHA = COLOR.a * edge * (0.34 + 0.40 * bars) * (1.0 - smoothstep(7.5, 12.0, age));
}
