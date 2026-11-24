shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_never, shadows_disabled;
#include "res://Shaders/depot_palette.gdshaderinc"

uniform float profile;

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float shadow;
    if (profile < 0.5) {
        shadow = (1.0 - smoothstep(0.24, 1.0, length(p))) * 0.44;
    } else if (profile < 1.5) {
        float rails = 1.0 - smoothstep(0.08, 0.28, abs(abs(p.x) - 0.70));
        float ends = 1.0 - smoothstep(0.68, 1.0, abs(p.y));
        float belly = 1.0 - smoothstep(0.05, 1.0, length(p * vec2(1.05, 1.12)));
        shadow = max(rails * ends * 0.45, belly * 0.25);
    } else {
        shadow = (1.0 - smoothstep(0.56, 1.0, max(abs(p.x), abs(p.y)))) * 0.43;
    }
    vec3 tint = vec3(0.062, 0.071, 0.079);
    ALBEDO = OUTPUT_IS_SRGB ? tint : depot_palette(tint);
    ALPHA = shadow;
}
