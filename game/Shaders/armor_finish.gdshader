shader_type spatial;

uniform sampler2D armor_albedo : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D armor_normal : hint_normal, filter_linear_mipmap, repeat_disable;
uniform sampler2D armor_orm : filter_linear_mipmap, repeat_disable;
uniform vec4 paint_tint : source_color;
uniform vec4 team_tint : source_color;
uniform float damage_stage = 0.0;

varying vec3 panel_position;

void vertex() {
    panel_position = VERTEX;
}

void fragment() {
    float finish = floor(UV.x * 4.0) + floor(UV.y * 2.0) * 4.0;
    vec3 base = texture(armor_albedo, UV).rgb;
    vec3 orm = texture(armor_orm, UV).rgb;
    NORMAL_MAP = texture(armor_normal, UV).rgb;
    NORMAL_MAP_DEPTH = 0.46;

    vec3 p = panel_position;
    float patch = sin(p.x * 3.1 + sin(p.z * 4.2) + p.y * 2.2) * sin(p.z * 2.9 - p.x * 2.3);
    float wear = sin(p.x * 15.0 - p.z * 10.2 + sin(p.y * 51.0)) * sin(p.z * 25.0 + p.y * 9.0);

    if (finish < 0.5) {
        base *= paint_tint.rgb * 1.30;
        base *= 1.0 + patch * 0.09;
        float dust = 1.0 - smoothstep(0.35, 1.28, p.y + patch * 0.16);
        base = mix(base, vec3(0.43, 0.36, 0.27), dust * 0.20);
        orm.g = mix(orm.g, 0.8, dust * 0.45);
    } else if (abs(finish - 5.0) < 0.5) {
        base *= team_tint.rgb * 1.16;
    }

    if (damage_stage > 0.5 && finish < 1.5) {
        float chipped = smoothstep(0.13, 0.78, patch + wear * 0.33);
        float scratches = chipped * smoothstep(0.02, 0.93, sin(p.x * 51.0 + p.z * 7.5 + p.y * 31.0 + wear * 1.08));
        base *= 1.0 - chipped * 0.21;
        base = mix(base, vec3(0.48, 0.47, 0.42), scratches * 0.80);
        orm.b += scratches * 0.27;
        if (damage_stage > 1.5) {
            float rear = 1.0 - smoothstep(0.24, 1.52, length(p.xz - vec2(0.44, 1.92)));
            float soot = max(rear, smoothstep(-0.27, 0.77, patch + wear * 0.17));
            base = mix(base, vec3(0.103, 0.089, 0.069), soot * 0.89);
            orm.g = mix(orm.g, 0.95, soot);
        }
    }

    ALBEDO = base;
    AO = orm.r;
    ROUGHNESS = orm.g;
    METALLIC = orm.b;
}
