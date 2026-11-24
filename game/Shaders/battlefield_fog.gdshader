shader_type spatial;
render_mode unshaded, shadows_disabled;
#include "res://Shaders/depot_palette.gdshaderinc"

uniform sampler2D visibility_mask : filter_linear, repeat_disable;

void fragment() {
    float visibility = texture(visibility_mask, UV).r;
    vec3 tint = vec3(0.055, 0.078, 0.12);
    ALBEDO = OUTPUT_IS_SRGB ? tint : depot_palette(tint);
    ALPHA = 0.76 * (1.0 - visibility);
}
