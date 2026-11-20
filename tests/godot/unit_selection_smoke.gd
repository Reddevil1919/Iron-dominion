extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	var load_result := change_scene_to_file(BATTLEFIELD)
	assert(load_result == OK, "Battlefield loads")
	await scene_changed
	root.size = Vector2i(1280, 720)
	await _settle()

	var units := current_scene.get_node("Units")
	var camera := root.get_camera_3d()
	assert(units.get_child_count() == 6, "The selection demo spawns six units")
	var points: Array[Vector2] = []
	for unit in units.get_children():
		points.append(camera.unproject_position(unit.global_position + Vector3.UP))

	await _click(points[0])
	assert(_selected(units) == ["Unit1"], "Click selects the allied squad")
	await _click(points[1], true)
	assert(_selected(units) == ["Unit1", "Unit2"], "Shift-click adds an ally")
	await _click(points[0], true)
	assert(_selected(units) == ["Unit2"], "Shift-click removes the selected ally")
	await _click(points[3], true)
	assert(_selected(units) == ["Unit2"], "Shift-click ignores an enemy")

	await _group(1, true)
	await _click(points[3])
	assert(_selected(units).is_empty(), "An enemy click cannot select an enemy")
	await _group(1)
	assert(_selected(units) == ["Unit2"], "A number recalls its saved group")

	var area := Rect2(points[0], Vector2.ZERO)
	for point in points:
		area = area.expand(point)
	area = area.grow(12.0)

	await _drag(area.end, area.position)
	assert(_selected(units) == ["Unit1", "Unit2", "Unit3"], "Reverse drag selects only allies")
	await _group(9, true)
	await _drag(area.position, area.end, true)
	assert(_selected(units).is_empty(), "Shift-drag toggles the allied units out")
	await _group(9)
	assert(_selected(units) == ["Unit1", "Unit2", "Unit3"], "The ninth group keeps its snapshot")
	await _click(root.get_visible_rect().get_center())
	assert(_selected(units).is_empty(), "Clicking empty terrain clears the selection")

	print("Unit selection smoke checks passed.")
	quit()


func _selected(units: Node) -> Array[String]:
	var names: Array[String] = []
	for unit in units.get_children():
		if unit.get_node("SelectionRing").visible:
			names.append(str(unit.name))
	return names


func _click(point: Vector2, shift := false) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	_send(motion)
	_mouse_button(point, true, shift)
	await process_frame
	_mouse_button(point, false, shift)
	await _settle()


func _drag(start: Vector2, end: Vector2, shift := false) -> void:
	_mouse_button(start, true, shift)
	var motion := InputEventMouseMotion.new()
	motion.position = end
	motion.global_position = end
	motion.button_mask = MOUSE_BUTTON_MASK_LEFT
	motion.shift_pressed = shift
	_send(motion)
	_mouse_button(end, false, shift)
	await _settle()


func _mouse_button(point: Vector2, pressed: bool, shift: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = MOUSE_BUTTON_LEFT
	event.position = point
	event.global_position = point
	event.button_mask = MOUSE_BUTTON_MASK_LEFT if pressed else 0
	event.pressed = pressed
	event.shift_pressed = shift
	_send(event)


func _group(number: int, ctrl := false) -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.physical_keycode = KEY_1 + number - 1
		event.pressed = pressed
		event.ctrl_pressed = ctrl
		_send(event)
	await _settle()


func _send(event: InputEvent) -> void:
	Input.parse_input_event(event)
	Input.flush_buffered_events()


func _settle() -> void:
	await physics_frame
	await physics_frame
