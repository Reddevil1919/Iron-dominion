shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;

void fragment() {
    float shape = floor(UV.x);
    vec2 p = vec2(fract(UV.x), UV.y) * 2.0 - 1.0;
    float r = length(p);
    vec3 shade = COLOR.rgb;
    float mask;

    if (shape < 0.5) {
        float tuft = sin(p.x * 9.0 + p.y * 3.3) * sin(p.y * 8.5 - p.x * 3.7);
        float lobe = sin(p.x * 3.5 - p.y * 4.4);
        float cloud = 1.0 - smoothstep(0.12, 1.0, r + tuft * 0.12 + lobe * 0.067);
        mask = cloud * (0.76 + tuft * 0.18);
        shade *= 0.92 + lobe * 0.085;
    } else if (shape < 1.5) {
        float taper = 1.0 - smoothstep(0.12, 1.0, abs(p.x));
        float tongue = (1.0 - smoothstep(0.06, max(0.08, taper * 0.71), abs(p.y))) * taper;
        float core = 1.0 - smoothstep(0.01, 0.42, r);
        mask = max(core, tongue);
        shade = mix(COLOR.rgb, vec3(1.0, 0.985, 0.89), core);
    } else if (shape < 2.5) {
        float core = 1.0 - smoothstep(0.14, 0.83, r);
        mask = 1.0 - smoothstep(0.10, 0.98, r);
        shade = mix(COLOR.rgb, vec3(1.0, 0.97, 0.88), core * 0.83);
    } else if (shape < 3.5) {
        float angle = atan(p.y, p.x + 0.0001);
        float lobes = 0.11 * sin(angle * 5.0 + 2.3) + 0.065 * sin(angle * 9.0);
        float fire = 1.0 - smoothstep(0.31, 0.98, r + lobes);
        float core = 1.0 - smoothstep(0.05, 0.62, r + lobes * 0.6);
        shade = mix(COLOR.rgb, vec3(1.0, 0.98, 0.91), core);
        mask = fire;
    } else if (shape < 4.5) {
        float body = length(vec2(p.x, p.y * 1.32));
        mask = 1.0 - smoothstep(0.70, 0.95, body);
        float glint = (1.0 - smoothstep(0.04, 0.69, length(p - vec2(0.28, -0.37))));
        shade = mix(COLOR.rgb * 0.68, vec3(0.85, 0.84, 0.78), glint);
    } else {
        float swirl = sin(p.x * 11.0 + p.y * 7.0) * sin(p.y * 9.0 - p.x * 4.0);
        float rim = 1.0 - smoothstep(0.02, 0.30, abs(r + swirl * 0.13 - 0.64));
        mask = rim * (0.56 + swirl * 0.31);
    }

    ALBEDO = shade;
    ALPHA = COLOR.a * mask;
}
