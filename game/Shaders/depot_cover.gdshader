shader_type spatial;
render_mode specular_disabled;

varying vec3 parcel;

void vertex() {
    parcel = VERTEX;
}

float grit_at(vec2 point) {
    vec3 p = fract(vec3(point.xyx) * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return fract((p.x + p.y) * p.z);
}

void fragment() {
    vec2 p = parcel.xz;
    float coarse = grit_at(floor(p * 1.8));
    float fine = grit_at(floor(p * 17.0));
    float wear = sin(p.x * 2.7 + sin(p.y * 1.6)) * 0.022 + (coarse - 0.5) * 0.042;
    float rut = 1.0 - smoothstep(0.14, 0.42, abs(abs(p.y) - 0.95));
    vec3 gravel = vec3(0.41, 0.38, 0.31) + vec3(wear + (fine - 0.5) * 0.044 - rut * 0.048);
    ALBEDO = gravel;
    ROUGHNESS = 0.96;
}
