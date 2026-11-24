extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var loaded := change_scene_to_file(BATTLEFIELD)
	assert(loaded == OK, "Battlefield loads")
	await scene_changed
	root.size = Vector2i(1280, 720)
	var region := current_scene.get_node("NavigationRegion") as NavigationRegion3D
	NavigationServer3D.map_set_use_async_iterations(region.get_navigation_map(), false)
	for tick in range(10):
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0:
			break
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0, "Navigation map is ready")

	var camera := root.get_camera_3d()
	var blue := current_scene.get_node("Units/Unit3") as Node3D
	var orange := current_scene.get_node("Units/Unit4") as Node3D
	var orange_tank := current_scene.get_node("Units/Unit6") as Node3D
	assert(orange.get_node("HealthBar") is Sprite3D, "The target has a health bar")
	assert(_armor_stage(blue) == 0.0 and not current_scene.has_node("DamagedTankSmoke"),
		"A fresh tank has clean armor and no engine smoke")
	_verify_reference_model(blue, orange_tank)
	var hull := blue.get_node("Model") as Node3D
	var turret := hull.get_node("Suspension/Turret") as Node3D
	var gun := turret.get_node("Gun") as Node3D
	var initial := blue.global_position
	var gun_rest := gun.position.z
	var bursts := current_scene.get_node("CannonBursts") as MeshInstance3D
	var shells := current_scene.get_node("CannonShells") as MeshInstance3D
	await _click(camera.unproject_position(initial + Vector3.UP), MOUSE_BUTTON_LEFT)

	var firing_position := Vector3(24.0, 0.0, -3.0)
	await _click(camera.unproject_position(firing_position), MOUSE_BUTTON_RIGHT)
	for tick in range(470):
		await physics_frame
		if blue.global_position.distance_to(firing_position) < 0.08:
			break
	assert(blue.global_position.distance_to(firing_position) < 0.08, "The tank reaches firing range")

	await _click(camera.unproject_position(orange.global_position + Vector3.UP), MOUSE_BUTTON_RIGHT)
	var recoiled := false
	var aimed := false
	var recovered := false
	var burst_seen := false
	var soil_seen := false
	var armor_seen := false
	var shell_in_motion := false
	var shell_is_compact := false
	var previous_shell := Vector3.ZERO
	var worn_armor := false
	var engine_smoke := false
	for tick in range(390):
		await physics_frame
		worn_armor = worn_armor or _armor_stage(blue) == 1.0
		engine_smoke = engine_smoke or (_armor_stage(blue) == 2.0 and _impact_visible("DamagedTankSmoke"))
		burst_seen = burst_seen or (bursts.visible and bursts.mesh.get_surface_count() == 1)
		soil_seen = soil_seen or _impact_visible("SoilImpacts")
		armor_seen = armor_seen or _impact_visible("ArmorImpacts")
		if shells.visible:
			var bounds := shells.get_aabb()
			shell_is_compact = shell_is_compact or bounds.size.length() < 1.0
			var center := bounds.get_center()
			shell_in_motion = shell_in_motion or (previous_shell != Vector3.ZERO
				and center.distance_to(previous_shell) > 0.10)
			previous_shell = center
		else:
			previous_shell = Vector3.ZERO
		recoiled = recoiled or gun.position.z > gun_rest + 0.08
		aimed = aimed or abs(angle_difference(0.0, turret.rotation.y)) > 0.20
		recovered = recovered or (recoiled and is_equal_approx(gun.position.z, gun_rest))
		if not is_instance_valid(orange):
			break
	assert(recoiled and recovered, "The tank's gun recoils and returns after firing")
	assert(aimed, "The turret can turn independently of its hull")
	assert(burst_seen, "Visible fire emits localized burst geometry")
	assert(shell_is_compact and shell_in_motion, "A compact shell travels between the muzzle and the impact")
	assert(soil_seen and armor_seen, "Combat distinguishes soil strikes from hits against tank armor")
	assert(not is_instance_valid(orange), "The ordered infantry target is destroyed and removed")
	assert(is_instance_valid(orange_tank), "The closer tank remains alive after the focused attack")
	assert(is_instance_valid(blue), "The attacking tank survives the test")
	assert(blue.global_position.distance_to(firing_position) < 0.08, "Attack clicks preserve the firing position")

	assert(await _verify_casualties(blue, worn_armor, engine_smoke), "The damage and casualty effects complete their full timeline")
	print("Unit combat smoke checks passed.")
	quit()

func _verify_reference_model(blue: Node3D, orange: Node3D) -> void:
	var mount := "Model/Suspension/"
	for part in ["ArmoredHull", "Turret", "Turret/Gun"]:
		var local := blue.get_node(mount + part) as MeshInstance3D
		var rival := orange.get_node(mount + part) as MeshInstance3D
		assert(local.mesh == rival.mesh and local.mesh.get_surface_count() == 1,
			"Both factions share the imported mesh and draw one surface per part")
		var format: int = local.mesh.surface_get_format(0)
		assert((format & Mesh.ARRAY_FORMAT_TEX_UV) != 0 and (format & Mesh.ARRAY_FORMAT_TANGENT) != 0,
			"The model has atlas coordinates and tangent space for its normal map")
		var arrays: Array = local.mesh.surface_get_arrays(0)
		var coordinates: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		assert(not indices.is_empty(), "The imported asset uses indexed triangles")
		for index in range(0, indices.size(), 3):
			var tile := _armor_tile(coordinates[indices[index]])
			assert(tile == _armor_tile(coordinates[indices[index + 1]])
				and tile == _armor_tile(coordinates[indices[index + 2]]), "Each face stays in one atlas tile")
		var blue_paint := local.get_surface_override_material(0) as ShaderMaterial
		var orange_paint := rival.get_surface_override_material(0) as ShaderMaterial
		assert(blue_paint.get_shader_parameter("team_tint") != orange_paint.get_shader_parameter("team_tint"),
			"Faction identification comes from the material")
		for channel in ["armor_albedo", "armor_normal", "armor_orm"]:
			var texture := blue_paint.get_shader_parameter(channel) as Texture2D
			assert(texture.get_size() == Vector2(2048, 2048) and texture == orange_paint.get_shader_parameter(channel),
				"The two paints share the 2K PBR textures")
	var gun := blue.get_node(mount + "Turret/Gun") as MeshInstance3D
	assert(absf(gun.get_aabb().position.z + 2.70) < 0.02, "The new barrel meets the existing muzzle effect anchor")

func _armor_tile(uv: Vector2) -> Vector2i:
	return Vector2i((uv * Vector2(4.0, 2.0)).floor())

func _impact_visible(path: NodePath) -> bool:
	var effect := current_scene.get_node_or_null(path) as MeshInstance3D
	return effect != null and effect.visible and effect.mesh.get_surface_count() == 1

func _armor_stage(tank: Node3D) -> float:
	var hull := tank.get_node("Model/Suspension/ArmoredHull") as MeshInstance3D
	var turret := tank.get_node("Model/Suspension/Turret") as MeshInstance3D
	var material := hull.material_override as ShaderMaterial
	assert(turret.material_override == material, "Armor damage follows both hull and turret")
	return 0.0 if material == null else material.get_shader_parameter("damage_stage")

func _verify_casualties(blue: Node3D, worn_armor: bool, engine_smoke: bool) -> bool:
	var casualty_effects := current_scene.get_node("CasualtyEffects")
	var body := casualty_effects.get_node("FallenSquad4") as Node3D
	var rig := body.get_node("Model/InfantryRig") as Skeleton3D
	var skin := rig.get_node("InfantrySkin") as MeshInstance3D
	var uniform := skin.material_override as ShaderMaterial
	var stains := casualty_effects.get_node("Blood4") as Node3D
	var paint := (stains.get_node("Stain0") as MeshInstance3D).material_override as ShaderMaterial
	var explosion: MeshInstance3D
	assert(uniform.get_shader_parameter("casualty_stains"), "Only the detached body uses the blood-stained uniform")
	assert(stains.get_child_count() == 4, "Each member has a ground stain")
	assert(not stains.visible, "Ground stains appear when the bodies touch down")
	assert(body.find_children("*", "CollisionObject3D", true, false).is_empty(), "Bodies have no picking or blocking collider")
	var start_height := rig.get_bone_global_pose(1).origin.y
	var saw_ground_pose := false
	var saw_full_body_hold := false
	var saw_stain_after_body := false
	var saw_stain_fade := false
	var last_stain_age := 0.0
	var saw_tank_loss := false
	var saw_explosion := false
	var shard: Node3D
	var first_height := 0.0
	var airborne := false
	var last_position := Vector3.ZERO
	var settled_at := -1
	var expired_at := -1

	for tick in range(320):
		await physics_frame
		if is_instance_valid(blue):
			worn_armor = worn_armor or _armor_stage(blue) == 1.0
			engine_smoke = engine_smoke or (_armor_stage(blue) == 2.0 and _impact_visible("DamagedTankSmoke"))
		if is_instance_valid(stains):
			var age: float = paint.get_shader_parameter("age_seconds")
			last_stain_age = age
			if age > 0.1 and age < 2.9:
				assert(is_instance_valid(body), "Bodies stay on the ground through the hold")
				saw_ground_pose = saw_ground_pose or rig.get_bone_global_pose(1).origin.y < start_height * 0.55
				saw_full_body_hold = saw_full_body_hold or age > 2.84
			if age > 3.13:
				assert(not is_instance_valid(body), "Bodies leave after three seconds on the ground")
				saw_stain_after_body = true
			saw_stain_fade = saw_stain_fade or age > 10.1

		if not saw_tank_loss and not is_instance_valid(blue):
			saw_tank_loss = true
			explosion = current_scene.get_node("TankExplosions") as MeshInstance3D
			var wreck := casualty_effects.get_node("Wreck3") as Node3D
			assert(wreck.get_child_count() == 9, "Destroying the tank scatters several solid fragments")
			shard = wreck.get_node("Fragment0") as Node3D
			first_height = shard.position.y
			last_position = shard.position
		saw_explosion = saw_explosion or (saw_tank_loss and explosion.visible)
		if is_instance_valid(shard) and shard.visible:
			var position := shard.position
			airborne = airborne or position.y > first_height + 0.25
			if airborne and settled_at < 0 and position.distance_to(last_position) < 0.0001:
				settled_at = tick
				assert(position.y < first_height, "The fragment settles below its launch height")
			last_position = position
		elif settled_at >= 0 and expired_at < 0:
			expired_at = tick

	assert(saw_ground_pose and saw_full_body_hold, "Infantry falls and remains grounded for three seconds")
	assert(saw_stain_after_body and saw_stain_fade, "Stains outlive the bodies and reach the fade phase")
	assert(not is_instance_valid(stains) and last_stain_age >= 12.88, "Stains disappear at the end of their fade")
	assert(saw_tank_loss and saw_explosion and airborne, "An eliminated tank explodes and throws debris")
	assert(worn_armor and engine_smoke, "Combat scuffs armor before the critically damaged tank smokes")
	assert(not _impact_visible("DamagedTankSmoke"), "The destroyed tank no longer emits engine smoke")
	assert(settled_at >= 0 and expired_at - settled_at >= 57 and expired_at - settled_at <= 61,
		"Each tracked fragment remains grounded for three seconds")
	assert(not casualty_effects.has_node("Wreck3"), "Wreck fragments are cleaned up")
	return true


func _click(point: Vector2, button: MouseButton) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	_send(motion)
	_mouse_button(point, button, true)
	await process_frame
	_mouse_button(point, button, false)
	for tick in range(4):
		await physics_frame


func _mouse_button(point: Vector2, button: MouseButton, pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.position = point
	event.global_position = point
	event.pressed = pressed
	if pressed:
		event.button_mask = MOUSE_BUTTON_MASK_LEFT if button == MOUSE_BUTTON_LEFT else MOUSE_BUTTON_MASK_RIGHT
	_send(event)


func _send(event: InputEvent) -> void:
	Input.parse_input_event(event)
	Input.flush_buffered_events()
