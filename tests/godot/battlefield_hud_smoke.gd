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

	var camera := root.get_camera_3d()
	var tank := current_scene.get_node("Units/Unit3") as Node3D
	var infantry := current_scene.get_node("Units/Unit1") as Node3D
	var hud := current_scene.get_node("SelectionLayer")
	var production := hud.get_node("ProductionPanel") as PanelContainer
	var selection := hud.get_node("SelectionPanel") as PanelContainer
	var notice := hud.get_node("OrderNotice") as PanelContainer
	var title := selection.find_child("SelectionTitle", true, false) as Label
	var health := selection.find_child("SelectionHealth", true, false) as Label
	var health_bar := selection.find_child("SelectionHealthBar", true, false) as ProgressBar
	var details := selection.find_child("Composition", true, false) as Label
	var message := notice.find_child("OrderMessage", true, false) as Label
	var tracers := current_scene.get_node("CombatTracers") as MeshInstance3D
	assert(not current_scene.has_node("CombatTrails"), "Combat no longer draws a source-to-target line")
	assert(not selection.visible and not notice.visible, "Context panels start hidden")
	await _click(camera.unproject_position(tank.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	assert(selection.visible and title.text == "Tank", "Tank selection shows its details")
	assert(health.text == "600 / 600 HP" and health_bar.value == 1.0, "The tank starts at full health")
	await _click(camera.unproject_position(infantry.global_position + Vector3.UP), MOUSE_BUTTON_LEFT, true)
	assert(title.text == "2 units selected" and details.text == "1 infantry · 1 tank", "The group lists both kinds")
	assert(health.text == "1000 / 1000 HP", "The group combines the selected units' health")

	root.size = Vector2i(960, 540)
	await process_frame
	await process_frame
	assert(not selection.get_global_rect().intersects(production.get_global_rect()), "The panels fit a smaller window")
	root.size = Vector2i(1280, 720)
	await process_frame
	await _click(camera.unproject_position(tank.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	assert(title.text == "Tank", "Only the tank is selected before the move")

	var initial_position := tank.global_position
	await _click(camera.unproject_position(Vector3(0, 1.5, 0)), MOUSE_BUTTON_RIGHT)
	assert(notice.visible and message.text == "That ground is blocked.", "An obstacle click explains why the move failed")
	assert(tank.global_position.is_equal_approx(initial_position), "The blocked order does not move the tank")
	for tick in range(53):
		await physics_frame
	assert(not notice.visible, "The notice clears after its time limit")

	await _click(camera.unproject_position(Vector3(-62, 0, 0)), MOUSE_BUTTON_RIGHT)
	assert(notice.visible and message.text == "No route to that position.", "The HUD explains a rejected simulation order")

	await _click(camera.unproject_position(Vector3(17, 0, -3)), MOUSE_BUTTON_RIGHT)
	var saw_tracer := false
	for tick in range(470):
		await physics_frame
		if tracers.visible:
			saw_tracer = true
		if health_bar.value < 1.0 and saw_tracer:
			break
	assert(saw_tracer, "Cannon fire creates a brief airborne tracer")
	assert(health_bar.value < 1.0 and health.text != "600 / 600 HP", "The selection panel follows combat damage")
	var tracer_cleared := false
	for tick in range(6):
		await physics_frame
		if not tracers.visible:
			tracer_cleared = true
			break
	assert(tracer_cleared, "Airborne tracers clear between shots")

	print("Battlefield HUD smoke checks passed.")
	quit()


func _click(point: Vector2, button: MouseButton, shift_pressed := false) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	_send(motion)
	_mouse_button(point, button, true, shift_pressed)
	await process_frame
	_mouse_button(point, button, false, shift_pressed)
	for tick in range(4):
		await physics_frame


func _mouse_button(point: Vector2, button: MouseButton, pressed: bool, shift_pressed: bool) -> void:
	var event := InputEventMouseButton.new()
	event.button_index = button
	event.position = point
	event.global_position = point
	event.pressed = pressed
	event.shift_pressed = shift_pressed
	if pressed:
		event.button_mask = MOUSE_BUTTON_MASK_LEFT if button == MOUSE_BUTTON_LEFT else MOUSE_BUTTON_MASK_RIGHT
	_send(event)


func _send(event: InputEvent) -> void:
	Input.parse_input_event(event)
	Input.flush_buffered_events()
