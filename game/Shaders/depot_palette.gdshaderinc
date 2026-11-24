vec3 depot_palette(vec3 srgb) {
    // The hand-authored palette uses display colors. Forward+ lights in linear space.
    vec3 color = max(srgb, vec3(0.0));
    return mix(color / 12.92, pow((color + 0.055) / 1.055, vec3(2.4)), step(vec3(0.04045), color));
}
