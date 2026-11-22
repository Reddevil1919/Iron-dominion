shader_type spatial;

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
}
