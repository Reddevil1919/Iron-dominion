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

	var production := current_scene.get_node("SelectionLayer/ProductionPanel")
	var credits := production.find_child("Credits", true, false) as Label
	var income := production.find_child("Income", true, false) as Label
	var sector_panel := current_scene.get_node("SelectionLayer/SectorPanel")
	var status := sector_panel.find_child("Alpha", true, false) as Label
	var progress := sector_panel.find_child("AlphaProgress", true, false) as ProgressBar
	assert(sector_panel.find_child("Bravo", true, false) != null, "Sector B is displayed")
	assert(sector_panel.find_child("Charlie", true, false) != null, "Sector C is displayed")
	assert(status.text == "A   Neutral" and income.text == "+5 credits/s", "The HQ earns before sectors are captured")
	var balance := _balance(credits)
	for tick in range(20):
		await physics_frame
	assert(_balance(credits) - balance == 5, "The HQ pays five credits in twenty ticks")

	var camera := root.get_camera_3d()
	var squad := current_scene.get_node("Units/Unit1") as Node3D
	var alpha := current_scene.get_node("Sectors/Alpha") as Node3D
	var ring := alpha.get_node("Ring") as MeshInstance3D
	for landmark in [["Alpha", "RELAY"], ["Bravo", "SERVICE"], ["Charlie", "CHECKPOINT"]]:
		var stencil := current_scene.get_node("Sectors/%s/GroundStencil" % landmark[0]) as Label3D
		assert(stencil.text == landmark[1] and stencil.position.y < 0.08, "Ground names stay below fog")
	assert(ring.mesh is ArrayMesh and ring.get_aabb().size.x > 11.8, "The flat outline keeps the capture radius")
	var symbol := alpha.get_node("Symbol") as Label3D
	var overview_scale := symbol.scale.x
	var initial_zoom := camera.size
	camera.size = 32.0
	await physics_frame
	await physics_frame
	assert(symbol.scale.x < overview_scale, "The floating letter shrinks at close zoom")
	camera.size = initial_zoom
	await process_frame
	var neutral_color: Color = ring.material_override.albedo_color
	await _click(camera.unproject_position(squad.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	await _click(camera.unproject_position(alpha.global_position), MOUSE_BUTTON_RIGHT)
	var saw_progress := false
	for tick in range(370):
		await physics_frame
		if status.text.contains("%") and progress.value > 0.0:
			saw_progress = true
		if status.text == "A   Blue":
			break

	assert(saw_progress, "The HUD reports capture progress")
	assert(status.text == "A   Blue", "Moving an infantry squad into the zone captures it")
	assert(progress.value == 0, "The capture bar clears when blue owns the sector")
	assert(ring.material_override.albedo_color != neutral_color, "The sector marker changes color")
	assert(income.text == "+7 credits/s", "Holding the sector adds two credits per second")
	balance = _balance(credits)
	for tick in range(20):
		await physics_frame
	assert(_balance(credits) - balance == 7, "The HUD balance receives the combined income")

	print("Sector capture smoke checks passed.")
	quit()


func _balance(label: Label) -> int:
	return int(label.text.get_slice(" ", 0))


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
