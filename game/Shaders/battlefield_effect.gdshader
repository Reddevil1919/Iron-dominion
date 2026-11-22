shader_type spatial;
render_mode unshaded, cull_disabled, shadows_disabled, fog_disabled;

void fragment() {
    float flare = step(1.0, UV.x);
    vec2 p = vec2(UV.x - flare, UV.y) * 2.0 - 1.0;
    float radius = length(p);
    float cloud = 1.0 - smoothstep(0.14, 1.0, radius);
    float core = 1.0 - smoothstep(0.06, 0.80, radius + min(abs(p.x), abs(p.y)) * 0.55);
    float ray = (1.0 - smoothstep(0.025, 0.19, min(abs(p.x), abs(p.y))))
        * (1.0 - smoothstep(0.24, 1.0, radius)) * 0.60;
    ALBEDO = mix(COLOR.rgb, vec3(1.0, 0.97, 0.83), core * flare);
    ALPHA = COLOR.a * mix(cloud, max(core, ray), flare);
}
