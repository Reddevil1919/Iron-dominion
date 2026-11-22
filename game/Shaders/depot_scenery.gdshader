shader_type spatial;
render_mode specular_disabled;

uniform sampler2D visibility_mask : filter_linear, repeat_disable;
uniform vec2 floor_origin;
uniform vec2 floor_size;

varying vec2 fog_coordinates;

void vertex() {
    vec2 world_xz = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xz;
    fog_coordinates = (world_xz - floor_origin) / floor_size;
}

void fragment() {
    float visibility = texture(visibility_mask, fog_coordinates).r;
    ALBEDO = mix(vec3(0.033, 0.041, 0.051), COLOR.rgb, mix(0.24, 1.0, visibility));
    ROUGHNESS = 0.87;
}
