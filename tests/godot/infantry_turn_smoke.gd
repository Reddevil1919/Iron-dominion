extends SceneTree


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	assert(change_scene_to_file("res://Scenes/Battlefield.tscn") == OK, "Battlefield loads")
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
	var model := squad.get_node("Model") as Node3D
	var rig := model.get_node("InfantryRig") as Skeleton3D
	await _click(squad.global_position + Vector3.UP, MOUSE_BUTTON_LEFT)
	for tick in range(3):
		await physics_frame
	assert(squad.get_node("SelectionRing").visible, "The infantry squad is selected")
	var origin := squad.global_position
	var corner := origin + Vector3(7.0, 0.0, 0.0)
	await _click(corner, MOUSE_BUTTON_RIGHT)
	for frame in range(170):
		if squad.global_position.distance_to(corner) < 0.04:
			break
		await process_frame
	assert(squad.global_position.distance_to(corner) < 0.04, "The squad reaches the corner")
	var turn_end := corner + Vector3(0.0, 0.0, 4.0)
	assert(await _verify_turn(squad, model, rig, turn_end, Vector3.BACK), "The squad steps around a right-angle turn")
	var return_point := turn_end + Vector3(0.0, 0.0, -6.0)
	assert(await _verify_turn(squad, model, rig, return_point, Vector3.FORWARD, true),
		"The squad steps through a reversal interrupted by a hold order")
	print("Infantry turn smoke checks passed.")
	quit()


func _verify_turn(squad: Node3D, model: Node3D, rig: Skeleton3D, goal: Vector3, direction: Vector3, stop_early := false) -> bool:
	var toe_left := rig.find_bone("S0_AnkleL")
	var toe_right := rig.find_bone("S0_AnkleR")
	var other_left := rig.find_bone("S1_AnkleL")
	var hip := rig.find_bone("S0_Pelvis")
	var stepped_sideways := false
	var alternated := false
	var different_stride := false
	var stepped_after_hold := false
	var hold_frame := -1
	var start := squad.global_position
	await _click(goal, MOUSE_BUTTON_RIGHT)
	var previous_heading := model.rotation.y
	for frame in range(165):
		await process_frame
		if stop_early and hold_frame < 0 and squad.global_position.distance_to(start) > 0.95:
			var hold := InputEventKey.new()
			hold.keycode = KEY_H
			hold.pressed = true
			Input.parse_input_event(hold)
			Input.flush_buffered_events()
			hold_frame = frame
		var rotation := angle_difference(previous_heading, model.rotation.y)
		previous_heading = model.rotation.y
		assert(absf(rotation) < 0.10, "Movement orders limit how far the squad pivots in one frame")
		var left := rig.get_bone_global_pose(toe_left).origin
		var right := rig.get_bone_global_pose(toe_right).origin
		var center := rig.get_bone_global_pose(hip).origin
		var other := rig.get_bone_global_pose(other_left).origin
		assert(left.x < right.x, "Both boots stay on their own side of the stance")
		if absf(rotation) > 0.011:
			stepped_sideways = stepped_sideways or absf(left.x - center.x) > 0.20 or absf(right.x - center.x) > 0.20
			var lifted := maxf(left.y, right.y) > 0.15 and minf(left.y, right.y) < 0.112
			alternated = alternated or lifted
			stepped_after_hold = stepped_after_hold or (hold_frame >= 0 and frame > hold_frame + 6 and lifted)
			different_stride = different_stride or absf(left.y - other.y) > 0.065
	var resting_left := rig.get_bone_global_pose(toe_left).origin
	var resting_right := rig.get_bone_global_pose(toe_right).origin
	if stop_early:
		var distance := squad.global_position.distance_to(start)
		assert(distance > 0.95 and distance < 1.5 and stepped_after_hold, "The squad finishes stepping at the halt")
	else:
		assert(squad.global_position.distance_to(goal) < 0.04, "The navigation destination is reached")
	assert((-model.basis.z).dot(direction) > 0.995, "The squad finishes facing its route")
	assert(absf(resting_left.y - 0.09) < 0.009 and absf(resting_right.y - 0.09) < 0.009, "Both boots return to the ground")
	assert(stepped_sideways and alternated and different_stride, "The turn shows lateral steps, support and staggered lifts")
	return true


func _click(site: Vector3, button: MouseButton) -> void:
	var point := root.get_camera_3d().unproject_position(site)
	var motion := InputEventMouseMotion.new()
	motion.position = point
	motion.global_position = point
	Input.parse_input_event(motion)
	Input.flush_buffered_events()
	for down in [true, false]:
		var event := InputEventMouseButton.new()
		event.position = point
		event.global_position = point
		event.button_index = button
		event.pressed = down
		if down:
			event.button_mask = MOUSE_BUTTON_MASK_LEFT if button == MOUSE_BUTTON_LEFT else MOUSE_BUTTON_MASK_RIGHT
		Input.parse_input_event(event)
		Input.flush_buffered_events()
		await process_frame
