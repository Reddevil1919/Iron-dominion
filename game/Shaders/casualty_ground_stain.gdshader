shader_type spatial;
render_mode cull_disabled, shadows_disabled;

uniform float age_seconds = 0.0;

void fragment() {
    vec2 point = UV * 2.0 - 1.0;
    float grain = sin(point.x * 29.0 + point.y * 14.0)
        * sin(point.y * 26.0 - point.x * 17.0);
    float shape = length(point * vec2(1.06, 1.14));
    float edges = sin(point.x * 9.0 + point.y * 7.0)
        * sin(point.y * 11.0 - point.x * 4.0);
    float pool = 1.0 - smoothstep(0.38, 0.74, shape + edges * 0.16 + grain * 0.05);
    float drops = 1.0 - smoothstep(0.055, 0.12,
        min(length(point - vec2(0.63, -0.42)), length(point - vec2(-0.56, 0.52))) + grain * 0.014);
    float drying = smoothstep(3.0, 10.0, age_seconds);
    float fade = 1.0 - smoothstep(10.0, 13.0, age_seconds);
    vec3 red = vec3(0.37, 0.032, 0.023);
    vec3 brown = vec3(0.19, 0.079, 0.044);

    ALBEDO = mix(red, brown, drying) * (0.83 + grain * 0.17);
    ROUGHNESS = mix(0.53, 0.96, drying);
    ALPHA = max(pool, drops * 0.63) * (0.83 - grain * 0.06) * fade;
}
