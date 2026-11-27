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
	var health := hud.find_child("SelectionHealth", true, false) as Label
	assert(edge.position.x < 0 and patch.get_meta("cover_facing") == "West", "The marked edge matches the protected side")
	for side in ["East", "West"]:
		var parcel := current_scene.get_node("CoverZones/" + side) as MeshInstance3D
		var paint := parcel.get_node("CoverPaint") as MeshInstance3D
		var rect := parcel.get_aabb()
		var bounds := paint.get_aabb()
		assert(bounds.position.x >= rect.position.x and bounds.end.x <= rect.end.x, "Cover paint is inside the parcel")
		assert(parcel.global_position.y + bounds.end.y < 0.08, "Cover stripes are below the fog plane")
	assert(not marker.visible, "Infantry starts exposed")
	assert(not current_scene.get_node("Units/Unit3").has_node("CoverIndicator"), "Tanks have no cover indicator")
	await _click(camera.unproject_position(squad.global_position + Vector3.UP), MOUSE_BUTTON_LEFT)
	var west := current_scene.get_node("CoverZones/West") as MeshInstance3D
	assert(await _verify_cover_stance(squad, rig, marker, west.global_position, rest_height),
		"Infantry transitions into cover and back to its ordinary stance")

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
		if health.text != "400 / 400 HP":
			break
		await physics_frame
	assert(health.text != "400 / 400 HP", "The squad takes fire in cover")
	assert(marker.text == "COVER" and status.visible and status.text == "In cover",
		"Damage and suppression keep the ordinary cover label")

	for tick in range(8):
		await physics_frame
	assert(rig.get_bone_global_pose(pelvis).origin.y < rest_height - 0.11, "Suppression lowers the skeleton")

	var aimed := false
	for tick in range(44):
		await physics_frame
		aimed = abs(rig.get_bone_global_pose(equipment).basis.z.y) < 0.17 \
			and rig.get_bone_global_pose(pelvis).origin.y < rest_height - 0.24
		if aimed:
			break
	assert(aimed, "A visible combat shot shoulders the rifle in the low stance")

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

func _verify_cover_stance(squad: Node3D, rig: Skeleton3D, marker: Label3D, parcel: Vector3, rest_height: float) -> bool:
	var camera := root.get_camera_3d()
	var center := Vector3(parcel.x - 2.0, 0.0, parcel.z)
	var front_pelvis := rig.find_bone("S0_Pelvis")
	var second_pelvis := rig.find_bone("S1_Pelvis")
	var rear_pelvis := rig.find_bone("S2_Pelvis")
	var kneeling_joint := rig.find_bone("S0_KneeR")
	var front_boot := rig.find_bone("S0_AnkleL")
	var step_dust := current_scene.get_node("FootstepDust") as MeshInstance3D
	assert(not step_dust.visible, "Idle boots produce no dust")
	await _click(camera.unproject_position(center), MOUSE_BUTTON_RIGHT)

	var walked_low := false
	var kicked_dust := false
	for tick in range(130):
		await physics_frame
		if squad.global_position.distance_to(center) < 0.08:
			break
		walked_low = walked_low or (marker.visible and marker.text == "COVER"
			and rig.get_bone_global_pose(front_pelvis).origin.y < rest_height - 0.18)
		kicked_dust = kicked_dust or (step_dust.visible and step_dust.mesh.get_surface_count() == 1)
	assert(squad.global_position.distance_to(center) < 0.08, "The squad reaches the quiet cover parcel")
	assert(walked_low and marker.text == "COVER", "Infantry moves low in cover before taking fire")
	assert(kicked_dust, "Infantry steps lift small clouds on loose surfaces")
	assert(rig.get_bone_global_pose(front_pelvis).origin.y > rest_height - 0.29, "The squad does not snap into a kneel")
	var first_kneel := -1
	var second_kneel := -1
	for tick in range(20):
		await physics_frame
		if first_kneel < 0 and rig.get_bone_global_pose(front_pelvis).origin.y < rest_height - 0.33:
			first_kneel = tick
		if second_kneel < 0 and rig.get_bone_global_pose(second_pelvis).origin.y < rest_height - 0.33:
			second_kneel = tick
	assert(first_kneel >= 0 and second_kneel > first_kneel, "The two lead soldiers settle at different times")
	var frontal := rig.get_bone_global_pose(front_pelvis).origin.y
	var rear := rig.get_bone_global_pose(rear_pelvis).origin.y
	assert(frontal < rest_height - 0.34 and rear > frontal + 0.12, "Front soldiers kneel while rear soldiers stay crouched")
	assert(rig.get_bone_global_pose(kneeling_joint).origin.y < 0.12, "The rear knee approaches the gravel")
	assert(absf(rig.get_bone_global_pose(front_boot).origin.y - 0.09) < 0.01, "The front boot is planted")
	assert(not step_dust.visible, "Footstep dust clears once the squad settles")

	var exit := center + Vector3(0.0, 0.0, 8.0)
	assert(await _verify_weight_transfer(rig, front_pelvis, front_boot, exit), "The kneeling soldier pushes off a planted foot")
	for tick in range(70):
		await physics_frame
		if squad.global_position.distance_to(exit) < 0.08:
			break
	assert(squad.global_position.distance_to(exit) < 0.08 and not marker.visible, "The squad leaves the parcel")
	for tick in range(20):
		await physics_frame
	assert(absf(rig.get_bone_global_pose(front_pelvis).origin.y - rest_height) < 0.018, "Exposed idle stance is restored")
	return true


func _verify_weight_transfer(rig: Skeleton3D, pelvis: int, boot: int, exit: Vector3) -> bool:
	var frame := rig.get_global_transform_interpolated()
	var plant := frame * rig.get_bone_global_pose(boot).origin
	var start := frame * rig.get_bone_global_pose(pelvis).origin
	var loaded := false
	var pushed := false
	var released := false
	var camera := root.get_camera_3d()
	await _click(camera.unproject_position(exit), MOUSE_BUTTON_RIGHT, 0)
	for sample in range(41):
		# Compare the pose and its interpolated root after UnitView has updated both.
		await create_timer(0.0).timeout
		frame = rig.get_global_transform_interpolated()
		var ankle := frame * rig.get_bone_global_pose(boot).origin
		var hip := frame * rig.get_bone_global_pose(pelvis).origin
		var height := hip.y - start.y
		var closer := Vector2(plant.x - hip.x, plant.z - hip.z).length() \
			< Vector2(plant.x - start.x, plant.z - start.z).length() - 0.14
		if closer and absf(height) < 0.035:
			assert(ankle.distance_to(plant) < 0.035, "The support boot stays planted as weight moves over it")
			loaded = true
		if loaded and height > 0.065 and ankle.distance_to(plant) < 0.035:
			pushed = true
		released = released or (pushed and ankle.y > plant.y + 0.043 and ankle.distance_to(plant) > 0.06)
	assert(loaded and pushed and released, "The lead soldier loads the boot at knee height, pushes up, then lifts the boot")
	return true


func _click(point: Vector2, button: MouseButton, wait_ticks: int = 4) -> void:
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	_send(motion)
	_mouse_button(point, button, true)
	await process_frame
	_mouse_button(point, button, false)
	for tick in range(wait_ticks):
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
