shader_type spatial;
render_mode cull_disabled;

uniform vec2 travel;
uniform sampler2D armor_albedo : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D armor_normal : hint_normal, filter_linear_mipmap, repeat_disable;
uniform sampler2D armor_orm : filter_linear_mipmap, repeat_disable;

void fragment() {
    float phase = mix(travel.x, travel.y, COLOR.r);
    float section = fract(UV.x - phase);
    float groove = smoothstep(0.04, 0.14, section) * (1.0 - smoothstep(0.82, 0.94, section));
    float shoe = groove * smoothstep(0.055, 0.13, min(UV.y, 1.0 - UV.y));
    float grouser = smoothstep(0.36, 0.44, section) * (1.0 - smoothstep(0.59, 0.65, section));
    float rim = 1.0 - smoothstep(0.04, 0.14, min(UV.y, 1.0 - UV.y));
    vec2 atlas_uv = vec2(0.756 + section * 0.237, 0.516 + UV.y * 0.468);
    vec3 finish = texture(armor_albedo, atlas_uv).rgb;
    vec3 orm = texture(armor_orm, atlas_uv).rgb;
    ALBEDO = finish * (0.39 + 0.50 * shoe + 0.32 * grouser * shoe + 0.35 * rim);
    NORMAL_MAP = texture(armor_normal, atlas_uv).rgb;
    NORMAL_MAP_DEPTH = 0.33;
    AO = mix(0.70, orm.r, shoe);
    ROUGHNESS = mix(0.94, orm.g, shoe);
    METALLIC = orm.b * (0.39 + 0.61 * grouser);
}
