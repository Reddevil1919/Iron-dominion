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

	var squad := current_scene.get_node("Units/Unit1") as Node3D
	var marker := squad.get_node("CoverIndicator") as Label3D
	var patch := current_scene.get_node("CoverZones/East") as MeshInstance3D
	var camera := root.get_camera_3d()
	var edge := patch.get_node("ProtectedEdge") as MeshInstance3D
	var hud := current_scene.get_node("SelectionLayer/SelectionPanel")
	var status := hud.find_child("SelectionCover", true, false) as Label
	assert(edge.position.x < 0 and patch.get_meta("cover_facing") == "West", "The marked edge matches the protected side")
	assert(not marker.visible, "Infantry starts exposed")
	assert(not current_scene.get_node("Units/Unit3").has_node("CoverIndicator"), "Tanks have no cover indicator")
	await _click(camera.unproject_position(squad.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)

	var covered := Vector3(patch.global_position.x, 0.0, patch.global_position.z)
	await _click(camera.unproject_position(covered), MOUSE_BUTTON_RIGHT)
	for tick in range(470):
		await physics_frame
		if squad.global_position.distance_to(covered) < 0.08:
			break
	assert(squad.global_position.distance_to(covered) < 0.08, "Cover ground remains navigable")
	assert(marker.visible and marker.text.begins_with("COVER"), "The indicator shows the squad is in cover")
	for tick in range(44):
		if marker.text.contains("SUPPRESSED"):
			break
		await physics_frame
	assert(marker.text == "COVER · SUPPRESSED", "Incoming hits suppress infantry in cover")
	assert(status.visible and status.text.contains("Suppressed"), "The selection HUD reports suppression")

	var exposed := covered + Vector3(0.0, 0.0, 6.0)
	await _click(camera.unproject_position(exposed), MOUSE_BUTTON_RIGHT)
	for tick in range(65):
		await physics_frame
		if squad.global_position.distance_to(exposed) < 0.08:
			break
	assert(squad.global_position.distance_to(exposed) < 0.08, "The squad moves out of cover")
	assert(not marker.visible or not marker.text.contains("COVER"), "The cover indicator clears after leaving")

	print("Infantry cover smoke checks passed.")
	quit()


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
