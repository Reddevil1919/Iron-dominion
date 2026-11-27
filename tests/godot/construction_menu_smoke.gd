extends SceneTree

const SAVE := "user://match-save.json"
const SITE := Vector3(-17, 0, -7)
const DESTINATION := Vector3(-17, 0, -22)

func _initialize() -> void:
	_run.call_deferred()

func _run() -> void:
	assert(not FileAccess.file_exists(SAVE))
	assert(change_scene_to_file("res://Scenes/Battlefield.tscn") == OK)
	await scene_changed
	root.size = Vector2i(1280, 720)
	var region := current_scene.get_node("NavigationRegion") as NavigationRegion3D
	NavigationServer3D.map_set_use_async_iterations(region.get_navigation_map(), false)
	for tick in range(10):
		if NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0:
			break
		await physics_frame
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0)

	var hud := current_scene.get_node("SelectionLayer")
	var panel := hud.get_node("ConstructionPanel") as Control
	var toggle := hud.get_node("ConstructionButton") as Button
	var status := panel.find_child("PlacementStatus", true, false) as Label
	var barracks := panel.find_child("PlaceBarracks", true, false) as Button
	var camera := root.get_camera_3d()
	await _key(KEY_B)
	assert(panel.visible)
	for kind in ["Headquarters", "Barracks", "TankFactory", "Airport", "ResearchCenter"]:
		var entry := panel.find_child("Place" + kind, true, false) as Button
		assert(entry != null and entry.text.contains(" cr") and entry.text.ends_with(" s"))
	assert((panel.find_child("PlaceHeadquarters", true, false) as Button).disabled)
	assert(panel.find_child("AvailabilityHeadquarters", true, false).text == "Insufficient credits")

	await _click(barracks.get_global_rect().get_center())
	var preview := current_scene.get_node("PlacementPreview") as Node3D
	var blocked := camera.unproject_position(Vector3(-32, 0, 24))
	await _move_pointer(blocked)
	assert(preview.visible and status.text.begins_with("Placement refused"))
	assert(status.text.contains("occupied"))
	await _click(blocked)
	assert(panel.visible and not current_scene.has_node("ConstructionSite"))
	await _key(KEY_R)
	assert(absf(preview.rotation.y - PI / 2) < 0.05)
	await _key(KEY_ESCAPE)
	assert(not paused and not panel.visible and not preview.visible)

	var tank := _tank()
	await _click(camera.unproject_position(tank.global_position + Vector3.UP))
	await _click(camera.unproject_position(DESTINATION), MOUSE_BUTTON_RIGHT)
	await _ticks(5)
	var pre_build := tank.global_position
	assert(pre_build.z < 6.5, "The tank is moving before construction")
	await _click(toggle.get_global_rect().get_center())
	await _click(barracks.get_global_rect().get_center())
	var valid := camera.unproject_position(SITE)
	await _move_pointer(valid)
	assert(preview.visible and status.text.begins_with("Placement valid"))
	await _click(valid)
	await _ticks(3)
	assert(current_scene.has_node("ConstructionSite") and not panel.visible)
	assert((current_scene.get_node("ConstructionSite") as Node3D).global_position.distance_to(SITE) < 0.15)

	await _ticks(20)
	var construction := current_scene.get_node("ConstructionSite/Progress") as Label3D
	var paused_at := ""
	await _key(KEY_ESCAPE)
	assert(paused)
	paused_at = construction.text
	await _click(_menu_button("SaveButton").get_global_rect().get_center())
	await _click(_menu_button("ResumeButton").get_global_rect().get_center())
	await _ticks(12)
	await _key(KEY_ESCAPE)
	await _click(_menu_button("LoadButton").get_global_rect().get_center())
	assert(current_scene.get_node("ConstructionSite/Progress").text == paused_at and paused)
	await _click(_menu_button("ResumeButton").get_global_rect().get_center())

	tank = _tank()
	var detoured := false
	for tick in range(560):
		await physics_frame
		var position := tank.global_position
		var on_footprint := absf(position.x - SITE.x) < 5.49 and absf(position.z - SITE.z) < 4.49
		assert(not on_footprint, "The active route cannot cross the reserved footprint")
		detoured = detoured or absf(position.x - SITE.x) > 8
		if position.distance_to(DESTINATION) < 0.1 and current_scene.has_node("Building14"):
			break
	assert(detoured and tank.global_position.distance_to(DESTINATION) < 0.1)
	assert(current_scene.has_node("Building14") and not current_scene.has_node("ConstructionSite"))

	await _key(KEY_ESCAPE)
	await _click(_menu_button("SaveButton").get_global_rect().get_center())
	await _click(_menu_button("LoadButton").get_global_rect().get_center())
	assert(paused and current_scene.has_node("Building14"), "A completed building survives reload")

	# Stage a one-hit building and an enemy in range, then let normal combat destroy it.
	var document = JSON.parse_string(FileAccess.get_file_as_string(SAVE))
	for building in document.state.session.simulation.buildings:
		if building.id == 14:
			building.hitPoints = 1
	for unit in document.state.session.simulation.units:
		if unit.id == 6:
			unit.position = {"x": -27, "z": -8}
	var fixture := FileAccess.open(SAVE, FileAccess.WRITE)
	fixture.store_string(JSON.stringify(_integers(document), "", true, true))
	fixture.close()
	await _click(_menu_button("LoadButton").get_global_rect().get_center())
	assert(paused and current_scene.has_node("Building14"))
	await _click(_menu_button("ResumeButton").get_global_rect().get_center())
	for tick in range(80):
		if not current_scene.has_node("Building14"):
			break
		await physics_frame
	assert(not current_scene.has_node("Building14"), "Combat removes the constructed building")

	tank = _tank()
	await _click(camera.unproject_position(tank.global_position + Vector3.UP))
	var finish := Vector3(-17, 0, 7)
	await _click(camera.unproject_position(finish), MOUSE_BUTTON_RIGHT)
	var crossed := false
	for tick in range(260):
		await physics_frame
		var position := tank.global_position
		crossed = crossed or (absf(position.x - SITE.x) < 0.25 and absf(position.z - SITE.z) < 1)
		if position.distance_to(finish) < 0.1:
			break
	assert(crossed and tank.global_position.distance_to(finish) < 0.1, "Destroying the building reopens its passage")
	print("Construction menu smoke checks passed.")
	quit()

func _tank() -> Node3D:
	return current_scene.get_node("Units/Unit3") as Node3D

func _menu_button(button_name: String) -> Button:
	return current_scene.get_node("MatchMenuLayer/MatchMenu").find_child(button_name, true, false) as Button

func _integers(value: Variant) -> Variant:
	if value is Dictionary:
		for key in value:
			value[key] = _integers(value[key])
	elif value is Array:
		for i in range(value.size()):
			value[i] = _integers(value[i])
	elif value is float and value == floor(value):
		return int(value)
	return value

func _move_pointer(point: Vector2) -> void:
	var move := InputEventMouseMotion.new()
	move.position = point
	move.global_position = point
	Input.parse_input_event(move)
	Input.flush_buffered_events()
	await process_frame
	await process_frame

func _click(point: Vector2, button := MOUSE_BUTTON_LEFT) -> void:
	await _move_pointer(point)
	for pressed in [true, false]:
		var event := InputEventMouseButton.new()
		event.button_index = button
		event.pressed = pressed
		event.position = point
		event.global_position = point
		if pressed:
			event.button_mask = MOUSE_BUTTON_MASK_LEFT if button == MOUSE_BUTTON_LEFT else MOUSE_BUTTON_MASK_RIGHT
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame
	await _ticks(2)

func _key(keycode: Key) -> void:
	for pressed in [true, false]:
		var event := InputEventKey.new()
		event.keycode = keycode
		event.pressed = pressed
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame

func _ticks(count: int) -> void:
	for tick in range(count):
		await physics_frame
