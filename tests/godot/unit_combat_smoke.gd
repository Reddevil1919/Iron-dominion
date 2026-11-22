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
	var hull := blue.get_node("Model") as Node3D
	var turret := hull.get_node("Turret") as Node3D
	var gun := turret.get_node("Gun") as Node3D
	var initial := blue.global_position
	var gun_rest := gun.position.z
	await _click(camera.unproject_position(initial + Vector3.UP), MOUSE_BUTTON_LEFT)

	var firing_position := Vector3(17.0, 0.0, -3.0)
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
	for tick in range(390):
		await physics_frame
		recoiled = recoiled or gun.position.z > gun_rest + 0.08
		aimed = aimed or abs(angle_difference(0.0, turret.rotation.y)) > 0.20
		recovered = recovered or (recoiled and is_equal_approx(gun.position.z, gun_rest))
		if not is_instance_valid(orange):
			break
	assert(recoiled and recovered, "The tank's gun recoils and returns after firing")
	assert(aimed, "The turret can turn independently of its hull")
	assert(not is_instance_valid(orange), "The ordered infantry target is destroyed and removed")
	assert(is_instance_valid(orange_tank), "The closer tank remains alive after the focused attack")
	assert(is_instance_valid(blue), "The attacking tank survives the test")
	assert(blue.global_position.distance_to(firing_position) < 0.08, "Attack clicks preserve the firing position")

	print("Unit combat smoke checks passed.")
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
