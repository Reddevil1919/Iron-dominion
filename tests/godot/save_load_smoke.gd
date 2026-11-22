extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"
const SAVE := "user://match-save.json"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	assert(not FileAccess.file_exists(SAVE), "The save smoke uses an isolated user directory")
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

	var menu := current_scene.get_node("MatchMenuLayer/MatchMenu")
	var production := current_scene.get_node("SelectionLayer/ProductionPanel")
	var panel := current_scene.get_node("SelectionLayer/SelectionPanel") as Control
	var status := menu.find_child("MatchDetail", true, false) as Label
	var save := menu.find_child("SaveButton", true, false) as Button
	var load_saved := menu.find_child("LoadButton", true, false) as Button
	var resume := menu.find_child("ResumeButton", true, false) as Button
	var train := production.find_child("TrainInfantry", true, false) as Button
	var queue := production.find_child("InfantryQueue", true, false) as Label
	var credits := production.find_child("Credits", true, false) as Label
	var tank := current_scene.get_node("Units/Unit3") as Node3D
	var camera := root.get_camera_3d()

	await _escape()
	assert(paused and load_saved.disabled, "An empty save slot cannot be loaded")
	await _click(resume.get_global_rect().get_center())
	await _click(train.get_global_rect().get_center())
	await _click(camera.unproject_position(tank.global_position + Vector3.UP))
	var start := tank.global_position
	await _click(camera.unproject_position(Vector3(-8, 0, -25)), MOUSE_BUTTON_RIGHT)
	await _wait_ticks(24)
	await _escape()
	var saved_position := tank.global_position
	var saved_credits := credits.text
	var saved_queue := queue.text
	assert(paused and saved_position.distance_to(start) > 1 and saved_queue != "Idle", "Save point has movement and production")
	await _click(save.get_global_rect().get_center())
	assert(status.text == "Match saved." and not load_saved.disabled, "Saving writes and enables the slot")
	var document = JSON.parse_string(FileAccess.get_file_as_string(SAVE))
	assert(document is Dictionary and document.get("schemaVersion") == 1, "The slot uses versioned JSON")

	await _click(resume.get_global_rect().get_center())
	await _wait_ticks(30)
	await _escape()
	var later_position := tank.global_position
	assert(later_position.distance_to(saved_position) > 1 and queue.text != saved_queue, "The current timeline diverges")
	await _click(load_saved.get_global_rect().get_center())
	var restored := current_scene.get_node("Units/Unit3") as Node3D
	assert(paused and status.text == "Saved match loaded.", "Load returns to the saved pause point")
	assert(restored.global_position == saved_position and credits.text == saved_credits and queue.text == saved_queue,
		"Loading restores the unit, balance and production progress")
	assert(not panel.visible, "Loading clears selection from the replaced timeline")

	await _click(resume.get_global_rect().get_center())
	await _wait_ticks(32)
	await _escape()
	assert(restored.global_position.distance_to(saved_position) > 1 and queue.text != saved_queue, "Saved orders continue")
	var position_after_resume := restored.global_position
	var broken := FileAccess.open(SAVE, FileAccess.WRITE)
	assert(broken != null, "The test can replace its disposable save fixture")
	broken.store_string('{"schemaVersion":99}')
	broken.close()
	await _click(load_saved.get_global_rect().get_center())
	assert(paused and status.text.begins_with("Could not load the match."), "An incompatible save reports a load error")
	assert(restored.global_position == position_after_resume and current_scene.get_node("Units/Unit3") == restored,
		"A rejected load leaves the active timeline in place")

	print("Save and load smoke checks passed.")
	quit()


func _wait_ticks(count: int) -> void:
	for tick in range(count):
		await physics_frame


func _escape() -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.keycode = KEY_ESCAPE
		event.pressed = pressed
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame


func _click(point: Vector2, button := MOUSE_BUTTON_LEFT) -> void:
	for pressed in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = button
		event.global_position = point
		event.position = point
		event.pressed = pressed
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame
	await _wait_ticks(6)
