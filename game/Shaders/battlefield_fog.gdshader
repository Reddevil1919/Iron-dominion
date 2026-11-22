shader_type spatial;
render_mode unshaded, shadows_disabled;

uniform sampler2D visibility_mask : filter_linear, repeat_disable;

void fragment() {
    float visibility = texture(visibility_mask, UV).r;
    ALBEDO = vec3(0.055, 0.078, 0.12);
    ALPHA = 0.76 * (1.0 - visibility);
}
