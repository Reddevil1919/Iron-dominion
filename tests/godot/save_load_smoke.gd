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
	var marks := (current_scene.get_node("TrackMarks") as MultiMeshInstance3D).multimesh
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
	assert(document is Dictionary and document.get("schemaVersion") == 4, "The slot uses versioned JSON")
	# Restore a tank at the critical threshold through the same save format.
	for unit in document.state.session.simulation.units:
		if unit.id == 3 or unit.id == 6:
			unit.hitPoints = unit.startingHitPoints * 0.3
		if unit.id == 6:
			unit.position = {"x": 43, "z": -55}
	var fixture := FileAccess.open(SAVE, FileAccess.WRITE)
	assert(fixture != null, "The test can write a damaged-tank checkpoint")
	fixture.store_string(JSON.stringify(_restore_integer_tokens(document), "", true, true))
	fixture.close()

	await _click(resume.get_global_rect().get_center())
	await _wait_ticks(30)
	await _escape()
	var later_position := tank.global_position
	assert(later_position.distance_to(saved_position) > 1 and queue.text != saved_queue, "The current timeline diverges")
	assert(marks.visible_instance_count > 0, "The timeline has temporary tread marks before loading")
	await _click(load_saved.get_global_rect().get_center())
	var restored := current_scene.get_node("Units/Unit3") as Node3D
	assert(paused and status.text == "Saved match loaded.", "Load returns to the saved pause point")
	assert(restored.global_position == saved_position and credits.text == saved_credits and queue.text == saved_queue,
		"Loading restores the unit, balance and production progress")
	assert(not panel.visible, "Loading clears selection from the replaced timeline")
	assert(marks.visible_instance_count == 0, "Loading clears tread marks from the replaced timeline")
	var hull := restored.get_node("Model/Suspension/ArmoredHull") as MeshInstance3D
	var armor := hull.material_override as ShaderMaterial
	assert(armor != null and armor.get_shader_parameter("damage_stage") == 2.0,
		"The critical armor finish is restored before resuming")

	await _click(resume.get_global_rect().get_center())
	await _wait_ticks(32)
	var smoke := current_scene.get_node("DamagedTankSmoke") as MeshInstance3D
	assert(smoke.visible and smoke.mesh.get_surface_count() == 1, "The moving tank resumes smoking once unpaused")
	var orange := current_scene.get_node("Units/Unit6") as Node3D
	assert(not orange.visible and smoke.get_aabb().end.x < restored.global_position.x + 8.0,
		"The unseen critical tank produces no visible smoke")
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

func _restore_integer_tokens(value: Variant) -> Variant:
	# JSON.parse maps every number to double. The save reader expects integer tokens for IDs and ticks.
	if value is Dictionary:
		for key in value:
			value[key] = _restore_integer_tokens(value[key])
	elif value is Array:
		for index in range(value.size()):
			value[index] = _restore_integer_tokens(value[index])
	elif value is float and value == floor(value):
		return int(value)
	return value


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
