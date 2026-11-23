shader_type spatial;

uniform float damage_stage = 0.0;
varying vec3 panel_position;

void vertex() {
    panel_position = VERTEX;
}

void fragment() {
    vec3 p = panel_position;
    float grain = sin(p.x * 39.0 + p.z * 27.0 + sin(p.y * 79.0));
    float patch = sin(p.x * 2.3 + sin(p.z * 3.6) + p.y * 1.7) * sin(p.z * 1.9 - p.x * 2.1);
    float streak = smoothstep(0.76, 0.97, sin(p.x * 18.0 + p.z * 5.2 + sin(p.z * 6.8)));
    float paint = max(COLOR.r, max(COLOR.g, COLOR.b)) - min(COLOR.r, min(COLOR.g, COLOR.b));
    float wear = (patch * 0.11 + grain * 0.018 - streak * 0.047) * (1.0 - smoothstep(0.20, 0.60, paint));
    ALBEDO = COLOR.rgb * (1.0 + wear);
    ROUGHNESS = 0.82 + patch * 0.06;
    METALLIC = 0.12;

    if (damage_stage > 0.5) {
        float unmarked = 1.0 - smoothstep(0.20, 0.56, paint);
        float abrasion = sin(p.x * 5.2 + p.z * 3.9 + p.y * 6.7)
            * sin(p.z * 4.9 - p.x * 3.5 + p.y * 3.8);
        float scuff = smoothstep(0.10, 0.74, abrasion + patch * 0.29);
        float grooves = sin(p.x * 23.0 + p.z * 4.8 + p.y * 15.0 + grain * 0.65);
        float exposed = scuff * smoothstep(0.33, 0.92, grooves);
        ALBEDO *= 1.0 - 0.32 * scuff * unmarked;
        ALBEDO = mix(ALBEDO, vec3(0.54, 0.53, 0.47), exposed * unmarked * 0.81);
        METALLIC += exposed * unmarked * 0.21;

        if (damage_stage > 1.5) {
            float rear = 1.0 - smoothstep(0.34, 1.75, length((p.xz - vec2(0.40, 1.88)) * vec2(0.80, 0.95)));
            float scorch = max(rear, smoothstep(-0.29, 0.50, patch + abrasion * 0.25));
            ALBEDO = mix(ALBEDO, vec3(0.23, 0.17, 0.12), scorch * unmarked * 0.56);
            float char = smoothstep(0.43, 0.94, scorch + abrasion * 0.15 + grain * 0.10);
            ALBEDO = mix(ALBEDO, vec3(0.09, 0.085, 0.075), char * unmarked * 0.92);
            ROUGHNESS = mix(ROUGHNESS, 1.0, char * unmarked);
        }
    }
}
