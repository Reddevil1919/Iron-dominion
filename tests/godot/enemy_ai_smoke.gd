extends SceneTree

const BATTLEFIELD := "res://Scenes/Battlefield.tscn"


func _initialize() -> void:
	_run.call_deferred()


func _run() -> void:
	assert(change_scene_to_file(BATTLEFIELD) == OK, "Battlefield loads")
	await scene_changed
	var region := current_scene.get_node("NavigationRegion") as NavigationRegion3D
	NavigationServer3D.map_set_use_async_iterations(region.get_navigation_map(), false)
	for tick in range(10):
		await physics_frame
		if NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0:
			break
	assert(NavigationServer3D.map_get_iteration_id(region.get_navigation_map()) > 0, "Navigation map is ready")

	var units := current_scene.get_node("Units")
	var orange_exit := current_scene.get_node("OrangeHQ/ProductionExit") as Marker3D
	var sector_panel := current_scene.get_node("SelectionLayer/SectorPanel")
	var charlie := sector_panel.find_child("Charlie", true, false) as Label
	assert(charlie.text == "C   Neutral", "The sector starts neutral")
	assert(units.get_child_count() == 6, "Both sides start with their original units")
	var tank_trained := false
	for tick in range(460):
		await physics_frame
		var trained := units.get_node_or_null("Unit14") as Node3D
		if trained != null:
			tank_trained = trained.global_position.distance_to(orange_exit.global_position) < 0.1
			break
	assert(tank_trained, "The first reinforcement appears at the orange HQ exit")
	assert(charlie.text == "C   Orange", "Orange infantry claims a sector without user input")

	var blue_hq := current_scene.get_node("BlueHQ") as StaticBody3D
	assert(blue_hq.has_node("HealthBar"), "The blue HQ is initially intact")
	for tick in range(2200):
		await physics_frame
		if not blue_hq.has_node("HealthBar"):
			break
	assert(not blue_hq.has_node("HealthBar"), "Orange advances and destroys the blue HQ without player input")
	var menu := current_scene.get_node("MatchMenuLayer/MatchMenu")
	var title := menu.find_child("MatchTitle", true, false) as Label
	var resume := menu.find_child("ResumeButton", true, false) as Button
	assert(paused and title.text == "Defeat" and not resume.visible, "Losing ends the game with a defeat screen")

	print("Enemy AI smoke checks passed.")
	quit()
