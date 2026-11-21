extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


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
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0, "Navigation map is ready")

	var menu := current_scene.get_node("MatchMenuLayer/MatchMenu")
	var pause_button := menu.get_node("PauseButton") as Button
	var overlay := menu.get_node("MatchOverlay") as Control
	var title := overlay.find_child("MatchTitle", true, false) as Label
	var resume := overlay.find_child("ResumeButton", true, false) as Button
	var restart := overlay.find_child("RestartButton", true, false) as Button
	var credits := _credit_label()
	var tank := current_scene.get_node("Units/Unit3") as Node3D
	var orange_hq := current_scene.get_node("OrangeHQ") as StaticBody3D
	var camera := root.get_camera_3d()
	assert(not paused and not overlay.visible, "The match starts running")
	await _click(camera.unproject_position(tank.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	assert(tank.get_node("SelectionRing").visible, "The tank is selected before pausing")

	await _click(pause_button.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	assert(paused and overlay.visible and title.text == "Paused", "The pause button opens a modal pause menu")
	var before := credits.text
	var position := tank.global_position
	await _click(camera.unproject_position(Vector3(-11, 0, -43)), MOUSE_BUTTON_RIGHT)
	for tick in range(24):
		await physics_frame
	assert(credits.text == before and tank.global_position == position, "The paused world does not advance")

	await _click(resume.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	assert(not paused and not overlay.visible, "The resume button continues the existing match")
	for tick in range(20):
		await physics_frame
	assert(credits.text != before, "Income resumes after unpausing")
	assert(tank.global_position == position, "World clicks do not issue orders through the pause menu")
	_press_escape()
	assert(paused and title.text == "Paused", "Escape opens the pause menu")
	_press_escape()
	assert(not paused and not overlay.visible, "Escape closes the pause menu")

	await _click(camera.unproject_position(tank.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	await _move_to(tank, camera, Vector3(-11, 0, -43))
	await _move_to(tank, camera, Vector3(32, 0, -38))
	await _click(camera.unproject_position(orange_hq.global_position + Vector3.UP * 3), MOUSE_BUTTON_RIGHT)
	for tick in range(1450):
		await physics_frame
		if overlay.visible:
			break
	assert(paused and overlay.visible and title.text == "Victory", "Destroying the orange HQ ends the match")
	assert(not resume.visible, "A finished match has no resume action")
	assert(not orange_hq.has_node("HealthBar"), "Destruction updates the battlefield before the result")
	before = credits.text
	_press_escape()
	for tick in range(24):
		await physics_frame
	assert(paused and title.text == "Victory" and credits.text == before, "The final result cannot be resumed")

	var old_scene := current_scene.get_instance_id()
	var point := restart.get_global_rect().get_center()
	await _click(point, MOUSE_BUTTON_LEFT)
	for tick in range(10):
		if current_scene != null and current_scene.get_instance_id() != old_scene:
			break
		await process_frame
	assert(current_scene != null and current_scene.get_instance_id() != old_scene, "Restart reloads a fresh scene")
	assert(not paused and current_scene.get_node("Units").get_child_count() == 6, "Restart restores the starting roster")
	assert(current_scene.has_node("OrangeHQ/HealthBar"), "Restart restores the enemy HQ")
	var balance := int(_credit_label().text.get_slice(" ", 0))
	assert(balance >= 500 and balance <= 505, "Restart resets credits")
	var hud := current_scene.get_node("SelectionLayer")
	assert(not hud.get_node("SelectionPanel").visible, "Restart clears selection")
	var alpha := hud.get_node("SectorPanel").find_child("Alpha", true, false) as Label
	assert(alpha.text == "A   Neutral", "Restart resets sector ownership")
	assert(not current_scene.get_node("MatchMenuLayer/MatchMenu/MatchOverlay").visible, "The new match runs")

	print("Match lifecycle smoke checks passed.")
	quit()


func _credit_label() -> Label:
	return current_scene.get_node("SelectionLayer/ProductionPanel").find_child("Credits", true, false) as Label


func _move_to(tank: Node3D, camera: Camera3D, point: Vector3) -> void:
	await _click(camera.unproject_position(point), MOUSE_BUTTON_RIGHT)
	for tick in range(460):
		await physics_frame
		if tank.global_position.distance_to(point) < 0.1:
			break
	assert(tank.global_position.distance_to(point) < 0.1, "The tank reaches its approach position")


func _press_escape() -> void:
	var event := InputEventKey.new()
	event.keycode = KEY_ESCAPE
	event.pressed = true
	_send(event)
	event = InputEventKey.new()
	event.keycode = KEY_ESCAPE
	_send(event)


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
