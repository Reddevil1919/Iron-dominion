extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var load_result := change_scene_to_file(BATTLEFIELD)
	assert(load_result == OK, "Battlefield loads")
	await scene_changed

	var rig := current_scene.get_node("CameraRig") as Node3D
	var camera := rig.get_node("OverviewCamera") as Camera3D
	assert(camera.is_current(), "The RTS camera is active")
	assert(is_equal_approx(camera.size, 136.0), "The scene starts at the overview zoom")

	var start := rig.global_position
	var right := camera.global_basis.x
	var forward := -camera.global_basis.z
	forward.y = 0.0
	forward = forward.normalized()

	await _hold_key(KEY_D, true, 12)
	assert((rig.global_position - start).dot(right) > 1.0, "The right movement follows the view")
	var moved_right := rig.global_position
	await _hold_key(KEY_W, true, 12)
	assert((rig.global_position - moved_right).dot(forward) > 1.0, "The up movement follows the view")

	await _hold_key(KEY_RIGHT, false, 180)
	assert(rig.global_position.is_equal_approx(Vector3(64.0, 0.0, -64.0)),
		"Right pan stops at both terrain edges")
	await _hold_key(KEY_LEFT, false, 180)
	assert(rig.global_position.is_equal_approx(Vector3(-64.0, 0.0, 64.0)),
		"Left pan stops at both terrain edges")

	var focus := rig.global_position
	var tilt := camera.global_basis
	_scroll(MOUSE_BUTTON_WHEEL_UP)
	assert(camera.size < 136.0 and camera.size > 32.0, "A single wheel step zooms in")
	for i in range(30):
		_scroll(MOUSE_BUTTON_WHEEL_UP)
	assert(is_equal_approx(camera.size, 32.0), "Zoom stops at the close limit")

	_scroll(MOUSE_BUTTON_WHEEL_DOWN)
	assert(camera.size > 32.0 and camera.size < 136.0, "A single reverse wheel step zooms out")
	for i in range(30):
		_scroll(MOUSE_BUTTON_WHEEL_DOWN)
	assert(is_equal_approx(camera.size, 136.0), "Zoom stops at the overview limit")
	assert(rig.global_position.is_equal_approx(focus), "Zoom preserves the ground focus")
	assert(camera.global_basis.is_equal_approx(tilt), "Zoom preserves the camera angle")

	print("RTS camera smoke checks passed.")
	quit()


func _hold_key(code: Key, physical: bool, frames: int) -> void:
	_key(code, physical, true)
	for i in range(frames):
		await process_frame
	_key(code, physical, false)


func _key(code: Key, physical: bool, pressed: bool) -> void:
	var event := InputEventKey.new()
	if physical:
		event.physical_keycode = code
	else:
		event.keycode = code
	event.pressed = pressed
	Input.parse_input_event(event)
	Input.flush_buffered_events()


func _scroll(button: MouseButton) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.pressed = true
	event.factor = 1.0
	Input.parse_input_event(event)
	Input.flush_buffered_events()
