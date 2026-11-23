extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"
const PICKING_LAYER := 2
const OBSTACLE_LAYER := 4


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	assert(change_scene_to_file(BATTLEFIELD) == OK, "Battlefield loads")
	await scene_changed
	root.size = Vector2i(1280, 720)
	var region := current_scene.get_node("NavigationRegion") as NavigationRegion3D
	NavigationServer3D.map_set_use_async_iterations(region.get_navigation_map(), false)
	for tick in range(10):
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0:
			break
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0, "Navigation is ready")

	var fog := current_scene.get_node("FogOfWar") as MeshInstance3D
	var tank := current_scene.get_node("Units/Unit3") as StaticBody3D
	var enemy := current_scene.get_node("Units/Unit4") as StaticBody3D
	var enemy_tank := current_scene.get_node("Units/Unit6") as StaticBody3D
	var headquarters := current_scene.get_node("OrangeHQ") as StaticBody3D
	var camera := root.get_camera_3d()
	var orders := current_scene.get_node("SelectionLayer/UnitSelection")
	var start := tank.global_position
	assert(fog.visible and fog.material_override is ShaderMaterial, "The battlefield has a fog overlay")
	assert(tank.visible and tank.get_node("HealthBar").is_visible_in_tree(), "Friendly visuals remain visible")
	assert(not enemy_tank.visible, "The roadblock conceals an enemy tank within vision range")
	assert(not enemy.visible and (enemy.collision_layer & PICKING_LAYER) == 0, "A distant enemy is hidden and unpickable")
	assert(not enemy.get_node("HealthBar").is_visible_in_tree(), "A hidden enemy does not leave a health bar")
	assert(not headquarters.visible and headquarters.collision_layer == OBSTACLE_LAYER,
		"A hidden enemy building still blocks line of sight without being pickable")
	await _hover(camera.unproject_position(enemy.global_position + Vector3.UP))
	assert(not orders.HasAttackCursor(), "Hidden enemies do not show a targeting cursor")

	await _click(camera.unproject_position(start + Vector3.UP), MOUSE_BUTTON_LEFT)
	assert(tank.get_node("SelectionRing").visible, "A friendly scout can be selected")
	await _click(camera.unproject_position(enemy.global_position + Vector3.UP), MOUSE_BUTTON_RIGHT)
	assert(tank.global_position.distance_to(start) >= 0.1, "Clicking a hidden enemy orders a ground move")

	var scout_position := Vector3(-8, 0, -25)
	await _move_to(tank, camera, scout_position)
	assert(enemy.visible and (enemy.collision_layer & PICKING_LAYER) != 0, "Scouting reveals and exposes the unit to picking")
	assert(enemy_tank.visible and enemy.get_node("HealthBar").is_visible_in_tree(), "Scouting reveals enemy visuals")
	assert(headquarters.visible and (headquarters.collision_layer & PICKING_LAYER) != 0, "The enemy base is revealed")
	await _hover(camera.unproject_position(enemy.global_position + Vector3.UP))
	assert(orders.HasAttackCursor(), "A visible enemy gives the attack crosshair")
	await _hover(camera.unproject_position(scout_position))
	assert(not orders.HasAttackCursor(), "Leaving the enemy restores the pointer")

	var gun := tank.get_node("Model/Suspension/Turret/Gun") as Node3D
	var resting_gun := gun.position.z
	assert(tank.global_position.distance_to(enemy.global_position) > 16.0, "The visible enemy starts out of range")
	await _click(camera.unproject_position(enemy.global_position + Vector3.UP), MOUSE_BUTTON_RIGHT)
	var fired := false
	for tick in range(180):
		await physics_frame
		if gun.position.z > resting_gun + 0.08:
			fired = true
			break
	assert(fired, "One attack click on a visible enemy makes the tank approach and fire")
	var firing_position := tank.global_position
	assert(firing_position.distance_to(enemy.global_position) < 16.05, "The tank stops within firing range")
	assert(firing_position.distance_to(enemy.global_position) > 14.0, "The tank stops short of the target")
	for tick in range(12):
		await physics_frame
	assert(tank.global_position.distance_to(firing_position) < 0.02, "The tank holds its firing position")

	var rifle_bursts := current_scene.get_node("RifleBursts") as MeshInstance3D
	await _move_to(tank, camera, Vector3(11, 0, -21))
	var saw_rifles := false
	for tick in range(25):
		await physics_frame
		saw_rifles = saw_rifles or (rifle_bursts.visible and rifle_bursts.mesh.get_surface_count() == 1)
	assert(saw_rifles, "Infantry fire uses the separate burst geometry")

	await _move_to(tank, camera, start)
	assert(not enemy.visible and (enemy.collision_layer & PICKING_LAYER) == 0, "Retreat removes the contact and picking shape")
	assert(not enemy.get_node("HealthBar").is_visible_in_tree() and not headquarters.visible, "Retreat hides enemy models and bars again")

	print("Fog of war smoke checks passed.")
	quit()


func _move_to(tank: Node3D, camera: Camera3D, point: Vector3) -> void:
	await _click(camera.unproject_position(point), MOUSE_BUTTON_RIGHT)
	for tick in range(400):
		await physics_frame
		if tank.global_position.distance_to(point) < 0.08:
			return
	assert(false, "The tank reaches the scouting waypoint")

func _hover(point: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	Input.parse_input_event(motion)
	Input.flush_buffered_events()
	for tick in range(3):
		await physics_frame


func _click(point: Vector2, button: MouseButton) -> void:
	for pressed in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = button
		event.position = point
		event.global_position = point
		event.pressed = pressed
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame
	for tick in range(6):
		await physics_frame
