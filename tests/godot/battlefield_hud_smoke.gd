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
	_verify_depot_details()
	await process_frame
	await process_frame
	_verify_lighting()

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
	var shells := current_scene.get_node("CannonShells") as MeshInstance3D
	assert(not current_scene.has_node("CombatTrails") and not current_scene.has_node("CombatTracers"),
		"Combat has no beam or streak geometry")
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

	await _click(camera.unproject_position(Vector3(24, 0, -3)), MOUSE_BUTTON_RIGHT)
	var saw_shell := false
	for tick in range(470):
		await physics_frame
		if shells.visible:
			saw_shell = true
		if health_bar.value < 1.0 and saw_shell:
			break
	assert(saw_shell, "Cannon fire creates a brief airborne shell")
	assert(health_bar.value < 1.0 and health.text != "600 / 600 HP", "The selection panel follows combat damage")
	var shell_cleared := false
	for tick in range(6):
		await physics_frame
		if not shells.visible:
			shell_cleared = true
			break
	assert(shell_cleared, "Airborne shells clear between shots")

	print("Battlefield HUD smoke checks passed.")
	quit()


func _verify_lighting() -> void:
	var lighting := current_scene.get_node("WorldEnvironment") as WorldEnvironment
	var sun := current_scene.get_node("Sun") as DirectionalLight3D
	assert(not lighting.camera_attributes.auto_exposure_enabled, "The exposure is fixed")
	assert(lighting.environment.ambient_light_source == Environment.AMBIENT_SOURCE_COLOR and sun.shadow_enabled,
		"The light uses a cool ambient fill and directional shadows")
	var forward_plus := RenderingServer.get_current_rendering_method() == "forward_plus"
	assert(lighting.environment.ssao_enabled == forward_plus and lighting.environment.volumetric_fog_enabled == forward_plus,
		"SSAO and atmospheric fog are scoped to Forward+")
	assert(current_scene.has_node("ApronHaze") == forward_plus and current_scene.has_node("TreelineHaze") == forward_plus,
		"Only Forward+ creates the decorative fog volumes")
	var tanks := (current_scene.get_node("TrackContactShadows") as MultiMeshInstance3D).multimesh
	var infantry := (current_scene.get_node("InfantryContactShadows") as MultiMeshInstance3D).multimesh
	var cargo := (current_scene.get_node("Roadblock/CargoContactShadows") as MultiMeshInstance3D).multimesh
	var visible_tanks := 0
	var visible_squads := 0
	for view in current_scene.get_node("Units").get_children():
		if view.is_visible_in_tree():
			if view.has_node("Model/TrackBelts"):
				visible_tanks += 1
			else:
				visible_squads += 1
	assert(tanks.visible_instance_count == visible_tanks and infantry.visible_instance_count == visible_squads * 4,
		"Only visible actors project contact shadows")
	assert(cargo.visible_instance_count == 3, "Each loaded pallet has a contact shadow")


func _verify_depot_details() -> void:
	var cargo := current_scene.get_node("Roadblock/CargoHandling") as MeshInstance3D
	var collision := current_scene.get_node("Roadblock/Collision") as CollisionShape3D
	var box := collision.shape as BoxShape3D
	assert((collision.transform * AABB(-box.size * 0.5, box.size)).encloses(cargo.transform * cargo.get_aabb()),
		"Cargo equipment fits inside the existing sight and movement blocker")
	var scenery := current_scene.get_node("Roadblock/Mesh") as MeshInstance3D
	var gates := current_scene.get_node("PerimeterGates") as MeshInstance3D
	assert(cargo.material_override == scenery.material_override and gates.material_override == scenery.material_override,
		"The depot details use the terrain visibility material")
	var bounds := collision.global_transform * AABB(-box.size * 0.5, box.size)
	var material := scenery.material_override as ShaderMaterial
	assert(material.get_shader_parameter("freight_yard") == Vector4(bounds.position.x, bounds.position.z, bounds.end.x, bounds.end.z),
		"The scenery shader uses the existing yard footprint to sample visible ground around it")
	var vertices: PackedVector3Array = gates.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
	assert(not vertices.is_empty(), "Perimeter details have geometry")
	for vertex in vertices:
		var x := absf((gates.global_transform * vertex).x)
		assert(x > 59.0 and x < 64.0, "The gates and booths stay beyond the navigable edge")


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
