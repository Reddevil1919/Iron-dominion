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
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0, "Navigation is ready")

	var tank := current_scene.get_node("Units/Unit3") as Node3D
	var camera := root.get_camera_3d()
	var hud := current_scene.get_node("SelectionLayer/SelectionPanel") as Control
	var attack_move := hud.find_child("AttackMoveButton", true, false) as Button
	var hold := hud.find_child("HoldPositionButton", true, false) as Button
	var trails := current_scene.get_node("CombatTrails") as MeshInstance3D
	var start := tank.global_position
	var staging := Vector3(-8, 0, -25)
	await _click(camera.unproject_position(start + Vector3.UP), MOUSE_BUTTON_LEFT)
	assert(hud.visible and attack_move.visible and hold.visible, "Selection exposes both tactical orders")

	await _click(camera.unproject_position(staging), MOUSE_BUTTON_RIGHT)
	assert(tank.global_position.distance_to(start) > 0.1, "The tank starts moving normally")
	await _key(KEY_H)
	var held := tank.global_position
	await _wait_ticks(12)
	assert(tank.global_position == held, "H cancels the route")

	await _click(camera.unproject_position(staging), MOUSE_BUTTON_RIGHT)
	for tick in range(360):
		await physics_frame
		if tank.global_position.distance_to(staging) < 0.08:
			break
	assert(tank.global_position.distance_to(staging) < 0.08, "The tank reaches the approach position")

	await _key(KEY_A, true)
	assert(attack_move.text == "Click ground", "Shift+A arms attack move")
	var objective := Vector3(14, 0, -18)
	await _click(camera.unproject_position(objective), MOUSE_BUTTON_LEFT)
	assert(attack_move.text == "Attack move" and tank.get_node("SelectionRing").visible,
		"The destination click consumes the mode and preserves selection")
	var saw_combat := false
	for tick in range(200):
		await physics_frame
		if trails.visible:
			saw_combat = true
			break
	assert(saw_combat, "Attack move engaged a visible contact")
	var engagement := tank.global_position
	assert(engagement.distance_to(staging) > 1 and engagement.distance_to(objective) > 6,
		"The tank approached and engaged before reaching its destination")
	await _wait_ticks(12)
	assert(tank.global_position == engagement, "The tank holds the engagement point through cooldown")

	await _click(camera.unproject_position(objective), MOUSE_BUTTON_RIGHT)
	assert(tank.global_position.distance_to(engagement) > 0.1, "An ordinary move overrides the engagement pause")
	await _click(hold.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	held = tank.global_position
	await _wait_ticks(12)
	assert(tank.global_position == held, "The HUD hold button cancels the new route")
	await _click(attack_move.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	assert(attack_move.text == "Click ground", "The HUD button also arms attack move")

	print("Tactical order smoke checks passed.")
	quit()


func _wait_ticks(count: int) -> void:
	for tick in range(count):
		await physics_frame


func _key(key: Key, shifted := false) -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.keycode = key
		event.shift_pressed = shifted
		event.pressed = pressed
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame
	await _wait_ticks(6)


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
	await _wait_ticks(6)
