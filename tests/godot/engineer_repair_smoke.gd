extends SceneTree

const SAVE := "user://match-save.json"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	assert(not FileAccess.file_exists(SAVE), "The repair smoke uses an isolated save slot")
	assert(change_scene_to_file("res://Scenes/Battlefield.tscn") == OK)
	await scene_changed
	root.size = Vector2i(1280, 720)
	var region := current_scene.get_node("NavigationRegion") as NavigationRegion3D
	NavigationServer3D.map_set_use_async_iterations(region.get_navigation_map(), false)
	for tick in range(10):
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0:
			break
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0)

	var canvas := current_scene.get_node("SelectionLayer")
	var train := canvas.find_child("TrainEngineers", true, false) as Button
	var queue := canvas.find_child("EngineersQueue", true, false) as Label
	var status := canvas.find_child("EngineeringStatus", true, false) as Label
	var readout := canvas.find_child("SelectionHealth", true, false) as Label
	var title := canvas.find_child("SelectionTitle", true, false) as Label
	var orders := canvas.get_node("UnitSelection")
	var units := current_scene.get_node("Units")
	var camera := root.get_camera_3d()
	await process_frame
	assert(not train.disabled and train.text.contains("150"))
	await _click(train.get_global_rect().get_center())
	assert(queue.text.begins_with("1 queued"), "The engineer squad enters the barracks queue")
	for tick in range(215):
		if units.has_node("Unit14"):
			break
		await physics_frame
	assert(units.has_node("Unit14") and queue.text == "Idle", "Training creates one engineer squad")

	assert(await _set_damage_fixture(), "A damaged friendly tank is loaded from a real save")
	var squad := units.get_node("Unit14") as Node3D
	var tank := units.get_node("Unit3") as Node3D
	var rig := squad.get_node("Model/InfantryRig") as Skeleton3D
	var gear := rig.find_bone("S0_Equipment")
	var bar := tank.get_node("HealthBar") as Sprite3D
	assert(bar.texture.get_size() == Vector2(48, 5) and bar.pixel_size < 0.1, "The overhead health bar is shorter and thinner")
	var target := camera.unproject_position(tank.global_position + Vector3.UP)
	await _hover(target)
	assert(not orders.HasRepairCursor(), "The wrench requires a selected engineer")
	await _click(target)
	assert(readout.text == "300 / 600 HP", "The tank starts at half health")
	await _click(camera.unproject_position(squad.global_position + Vector3.UP))
	assert(title.text == "Engineers" and status.visible, "Engineer selection shows repair controls")
	await _hover(target)
	assert(orders.HasRepairCursor(), "A living friendly tank shows the wrench")
	await _hover(camera.unproject_position(Vector3(-10, 0, 22)))
	assert(not orders.HasRepairCursor(), "Leaving the tank restores the cursor")

	var start := squad.global_position
	await _click(target, MOUSE_BUTTON_RIGHT)
	var sparks_seen := false
	for tick in range(275):
		await physics_frame
		var sparks := current_scene.get_node_or_null("WeldingSparks") as MeshInstance3D
		sparks_seen = sparks_seen or (sparks != null and sparks.visible and sparks.mesh.get_surface_count() > 0)
		if sparks_seen and status.text == "Repairing tank":
			break
	assert(squad.global_position.distance_to(start) > 4.0, "Engineers approach the target using navigation")
	assert(status.text == "Repairing tank" and sparks_seen, "The active repair produces a visible arc and sparks")
	await _wait_ticks(12)
	var torch := rig.get_bone_global_pose(gear)
	var tip := rig.to_global(torch * Vector3(0, 0, -0.40))
	assert(tip.distance_to(tank.global_position + Vector3.UP) < 3.2, "The welder reaches the hull with the torch")

	await _key(KEY_H)
	await _wait_ticks(4)
	assert(status.text != "Repairing tank", "Hold interrupts repair")
	await _click(target)
	var interrupted_at := int(readout.text.get_slice(" ", 0))
	assert(title.text == "Tank" and interrupted_at > 300 and interrupted_at < 600, "Welding restores some tank health")
	await _wait_ticks(18)
	assert(int(readout.text.get_slice(" ", 0)) == interrupted_at, "Tank health stays put after hold")
	assert(not current_scene.get_node("WeldingSparks").visible, "The arc and sparks clear after the order stops")

	await _click(camera.unproject_position(squad.global_position + Vector3.UP))
	await _click(target, MOUSE_BUTTON_RIGHT)
	await _wait_ticks(225)
	assert(status.text != "Repairing tank", "Repair leaves the squad idle at full health")
	await _click(target)
	assert(title.text == "Tank" and readout.text == "600 / 600 HP", "The repair ends at maximum tank health")

	print("Engineer repair smoke checks passed.")
	quit()


func _set_damage_fixture() -> bool:
	var menu := current_scene.get_node("MatchMenuLayer/MatchMenu")
	var save := menu.find_child("SaveButton", true, false) as Button
	var load_saved := menu.find_child("LoadButton", true, false) as Button
	var resume := menu.find_child("ResumeButton", true, false) as Button
	var detail := menu.find_child("MatchDetail", true, false) as Label
	await _key(KEY_ESCAPE)
	await _click(save.get_global_rect().get_center())
	var document = JSON.parse_string(FileAccess.get_file_as_string(SAVE))
	assert(document is Dictionary, "The genuine save is readable")
	for unit in document.state.session.simulation.units:
		if unit.id == 3:
			unit.hitPoints = 300
	var fixture := FileAccess.open(SAVE, FileAccess.WRITE)
	assert(fixture != null)
	fixture.store_string(JSON.stringify(_integer_tokens(document), "", true, true))
	fixture.close()
	await _click(load_saved.get_global_rect().get_center())
	assert(detail.text == "Saved match loaded.")
	await _click(resume.get_global_rect().get_center())
	return not paused


func _integer_tokens(value: Variant) -> Variant:
	if value is Dictionary:
		for key in value:
			value[key] = _integer_tokens(value[key])
	elif value is Array:
		for index in range(value.size()):
			value[index] = _integer_tokens(value[index])
	elif value is float and value == floor(value):
		return int(value)
	return value


func _hover(point: Vector2) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	_send(motion)
	await _wait_ticks(3)


func _click(point: Vector2, button := MOUSE_BUTTON_LEFT) -> void:
	await _hover(point)
	for pressed in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = button
		event.global_position = point
		event.position = point
		event.pressed = pressed
		_send(event)
		await process_frame
	await _wait_ticks(6)


func _key(code: Key) -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.keycode = code
		event.pressed = pressed
		_send(event)
		await process_frame


func _wait_ticks(count: int) -> void:
	for tick in range(count):
		await physics_frame


func _send(event: InputEvent) -> void:
	Input.parse_input_event(event)
	Input.flush_buffered_events()
