extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	await _load_battlefield()
	await _check_infantry_queue()
	await _load_battlefield()
	await _check_tank_production()
	print("Unit production smoke checks passed.")
	quit()


func _load_battlefield() -> void:
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


func _check_infantry_queue() -> void:
	var panel := current_scene.get_node("SelectionLayer/ProductionPanel") as PanelContainer
	var infantry := panel.find_child("TrainInfantry", true, false) as Button
	var tank := panel.find_child("TrainTank", true, false) as Button
	var credits := panel.find_child("Credits", true, false) as Label
	var queue := panel.find_child("InfantryQueue", true, false) as Label
	var units := current_scene.get_node("Units")
	var exit := current_scene.get_node("BlueBarracks/ProductionExit") as Marker3D
	await process_frame

	assert(credits.text == "500 credits", "The player sees their starting balance")
	assert(not infantry.disabled and not tank.disabled, "Both units are initially affordable")
	await _click(infantry.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	await _click(infantry.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	assert(credits.text == "300 credits", "Both clicks immediately reserve their cost")
	assert(tank.disabled, "The tank becomes unaffordable")
	assert(queue.text.begins_with("2 queued"), "Both squads enter the same FIFO queue")
	assert(units.get_child_count() == 6, "Nothing spawns before training")

	for tick in range(160):
		await physics_frame
		if units.has_node("Unit11"):
			break
	var first := units.get_node("Unit11") as Node3D
	assert(first.global_position.is_equal_approx(exit.global_position), "The squad spawns at the barracks exit")

	for tick in range(165):
		await physics_frame
		if queue.text.contains("Exit blocked"):
			break
	assert(queue.text.contains("Exit blocked"), "The next squad waits at the occupied exit")
	assert(units.get_child_count() == 7, "The blocked order does not place another unit")

	var camera := root.get_camera_3d()
	await _click(camera.unproject_position(first.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	var destination := exit.global_position + Vector3(0.0, 0.0, 18.0)
	await _click(camera.unproject_position(destination), MOUSE_BUTTON_RIGHT)
	for tick in range(110):
		await physics_frame
		if units.has_node("Unit12"):
			break
	assert(first.global_position.distance_to(exit.global_position) >= 9.5, "The trained squad obeys move orders")
	var second := units.get_node("Unit12") as Node3D
	assert(second.global_position.is_equal_approx(exit.global_position), "The next squad spawns after the exit clears")
	assert(units.get_child_count() == 8, "Each training order spawns exactly one squad")
	assert(queue.text == "Idle" and credits.text == "300 credits", "Spawning empties the queue without charging again")


func _check_tank_production() -> void:
	var panel := current_scene.get_node("SelectionLayer/ProductionPanel") as PanelContainer
	var tank := panel.find_child("TrainTank", true, false) as Button
	var credits := panel.find_child("Credits", true, false) as Label
	var queue := panel.find_child("TankQueue", true, false) as Label
	var units := current_scene.get_node("Units")
	var exit := current_scene.get_node("BlueHQ/ProductionExit") as Marker3D
	await process_frame

	await _click(tank.get_global_rect().get_center(), MOUSE_BUTTON_LEFT)
	assert(credits.text == "0 credits", "The HQ reserves the entire tank cost")
	assert(queue.text.begins_with("1 queued"), "The tank enters the HQ queue")
	assert(units.get_child_count() == 6, "The tank trains before spawning")
	for tick in range(400):
		await physics_frame
		if units.has_node("Unit11"):
			break
	var trained := units.get_node("Unit11") as Node3D
	assert(trained.global_position.is_equal_approx(exit.global_position), "The tank spawns at the HQ exit")
	assert(queue.text == "Idle" and credits.text == "0 credits", "The HQ order completes without charging again")


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
