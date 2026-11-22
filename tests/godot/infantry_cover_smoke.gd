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
	var rig := squad.get_node("Model/InfantryRig") as Skeleton3D
	var skin_view := rig.get_node("InfantrySkin") as MeshInstance3D
	assert(skin_view.skin.get_bind_count() == rig.get_bone_count(), "Every joint has a skin bind")
	assert((skin_view.mesh.surface_get_format(0) & Mesh.ARRAY_FORMAT_WEIGHTS) != 0, "The mesh has vertex weights")
	var ankle_l := rig.find_bone("S0_AnkleL")
	var ankle_r := rig.find_bone("S0_AnkleR")
	var pelvis := rig.find_bone("S0_Pelvis")
	var equipment := rig.find_bone("S0_Equipment")
	var rest_height := rig.get_bone_global_pose(pelvis).origin.y
	var idle_ankle := rig.get_bone_global_pose(ankle_l).origin.y
	assert(abs(idle_ankle - 0.09) < 0.008, "The idle foot rests on the terrain")
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
	var lifted_left := false
	var lifted_right := false
	for tick in range(470):
		await physics_frame
		var left_height := rig.get_bone_global_pose(ankle_l).origin.y
		var right_height := rig.get_bone_global_pose(ankle_r).origin.y
		lifted_left = lifted_left or (left_height > 0.16 and right_height < 0.12)
		lifted_right = lifted_right or (right_height > 0.16 and left_height < 0.12)
		if squad.global_position.distance_to(covered) < 0.08:
			break
	assert(squad.global_position.distance_to(covered) < 0.08, "Cover ground remains navigable")
	assert(lifted_left and lifted_right, "Both boots alternate between ground contact and swing")
	assert(marker.visible and marker.text.begins_with("COVER"), "The indicator shows the squad is in cover")
	for tick in range(44):
		if marker.text.contains("SUPPRESSED"):
			break
		await physics_frame
	assert(marker.text == "COVER · SUPPRESSED", "Incoming hits suppress infantry in cover")
	assert(status.visible and status.text.contains("Suppressed"), "The selection HUD reports suppression")

	for tick in range(8):
		await physics_frame
	assert(rig.get_bone_global_pose(pelvis).origin.y < rest_height - 0.11, "Suppression lowers the skeleton")

	var aimed := false
	for tick in range(44):
		await physics_frame
		aimed = abs(rig.get_bone_global_pose(equipment).basis.z.y) < 0.17
		if aimed:
			break
	assert(aimed, "A visible combat shot blends the equipment into the aiming pose")

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
