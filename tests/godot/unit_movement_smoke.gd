extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var load_result := change_scene_to_file(BATTLEFIELD)
	assert(load_result == OK, "Battlefield loads")
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
	var tank := current_scene.get_node("Units/Unit3") as Node3D
	var enemy := current_scene.get_node("Units/Unit6") as Node3D
	var initial := tank.global_position
	var enemy_initial := enemy.global_position
	var model := tank.get_node("Model")
	var suspension := model.get_node("Suspension") as Node3D
	var belts := model.get_node("TrackBelts") as MeshInstance3D
	var belt_material := belts.material_override as ShaderMaterial
	var dust := current_scene.get_node("TrackDust") as MeshInstance3D
	assert(not dust.visible, "An idle tank produces no track dust")
	await _click(camera.unproject_position(initial + Vector3.UP), MOUSE_BUTTON_LEFT)
	assert(tank.get_node("SelectionRing").visible, "The blue tank is selected")

	await _click(camera.unproject_position(Vector3(0.0, 3.0, 0.0)), MOUSE_BUTTON_RIGHT)
	await _click(camera.unproject_position(Vector3(0.0, 0.0, 11.5)), MOUSE_BUTTON_RIGHT)
	assert(tank.global_position.is_equal_approx(initial), "Blocked ground and the obstacle do not move the tank")

	var goal := Vector3(17.0, 0.0, 7.0)
	await _click(camera.unproject_position(goal), MOUSE_BUTTON_RIGHT)
	var previous := tank.global_position
	var steering_seen := false
	var suspension_seen := false
	var dust_seen := false
	for tick in range(440):
		await physics_frame
		var current := tank.global_position
		var travel: Vector2 = belt_material.get_shader_parameter("travel")
		steering_seen = steering_seen or abs(travel.x - travel.y) > 0.06
		suspension_seen = suspension_seen or abs(suspension.rotation.x) > 0.006
		dust_seen = dust_seen or dust.visible
		assert(current.distance_to(previous) <= 0.151, "The tank moves at its configured tick speed")
		assert(abs(current.x) >= 10.99 or abs(current.z) >= 12.99, "The tank stays outside the expanded obstacle")
		if current.distance_to(goal) < 0.08:
			break
		previous = current

	assert(tank.global_position.distance_to(goal) < 0.08, "The tank reaches the clicked ground")
	assert(enemy.global_position.is_equal_approx(enemy_initial), "The enemy tank never moves")
	assert(steering_seen, "The two belts advance by different amounts on a turn")
	assert(suspension_seen, "The hull reacts to acceleration")
	assert(dust_seen, "Travel creates dust")
	for tick in range(25):
		await physics_frame
	assert(not dust.visible, "Track dust clears after stopping")
	assert(abs(suspension.rotation.x) < 0.008, "The hull settles after braking")

	var beyond_enemy := Vector3(17, 0, -15)
	await _click(camera.unproject_position(beyond_enemy), MOUSE_BUTTON_RIGHT)
	var detoured := false
	for tick in range(380):
		await physics_frame
		var current := tank.global_position
		assert(current.distance_to(enemy.global_position) >= 6.59, "Tank hulls stay apart throughout the pass")
		detoured = detoured or abs(current.x - enemy_initial.x) > 5.0
		if current.distance_to(beyond_enemy) < 0.08:
			break
	assert(detoured and tank.global_position.distance_to(beyond_enemy) < 0.08,
		"The tank steers around the stationary tank and reaches its destination")
	print("Unit movement smoke checks passed.")
	quit()


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
