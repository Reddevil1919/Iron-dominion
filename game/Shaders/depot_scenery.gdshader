shader_type spatial;
render_mode specular_disabled;

uniform sampler2D visibility_mask : filter_linear, repeat_disable;
uniform vec2 floor_origin;
uniform vec2 floor_size;
uniform vec4 freight_yard;

varying vec3 world_position;

void vertex() {
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

float ground_visibility(vec2 point) {
    return texture(visibility_mask, (point - floor_origin) / floor_size).r;
}

void fragment() {
    vec2 site = world_position.xz;
    float visibility = ground_visibility(site);
    if (all(greaterThanEqual(site, freight_yard.xy)) && all(lessThanEqual(site, freight_yard.zw))) {
        // The central collider occludes its own interior cells. Nearby visible ground lights the static yard.
        vec2 cell = floor_size / vec2(textureSize(visibility_mask, 0));
        vec2 west = freight_yard.xy - cell;
        vec2 east = freight_yard.zw + cell;
        visibility = max(visibility, ground_visibility(vec2(west.x, site.y)));
        visibility = max(visibility, ground_visibility(vec2(east.x, site.y)));
        visibility = max(visibility, ground_visibility(vec2(site.x, west.y)));
        visibility = max(visibility, ground_visibility(vec2(site.x, east.y)));
    }
    vec3 paint = COLOR.rgb;
    float wear = 1.0 - COLOR.a;
    if (wear > 0.01) {
        vec3 p = world_position;
        float panel = sin(p.x * 4.6 + p.z * 5.1) * sin(p.y * 3.5 + p.z * 10.3);
        float runoff = smoothstep(0.58, 1.0, sin(p.x * 19.0 + p.z * 21.0 + sin(p.y * 2.8)));
        float dust = 1.0 - smoothstep(0.14, 1.16, p.y + panel * 0.15);
        paint *= 1.0 + wear * (0.17 * panel - 0.20 * runoff);
        paint = mix(paint, vec3(0.46, 0.39, 0.29), dust * wear * 0.34);
    }
    ALBEDO = mix(vec3(0.033, 0.041, 0.051), paint, mix(0.24, 1.0, visibility));
    ROUGHNESS = 0.87;
}
