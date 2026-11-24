using Godot;

namespace IronDominion.Game;

internal sealed class TankMotion : IDisposable
{
    private const float RestGunZ = -1.12f;
    public const float TreadMarkSpacing = 0.62f;
    private readonly Node3D _chassis;
    public MeshInstance3D Hull { get; }
    private readonly MeshInstance3D _turret;
    private readonly MeshInstance3D _gun;
    private readonly ShaderMaterial _trackMaterial;
    private readonly TankArt _art;
    private readonly int _startingHitPoints;

    private Vector2 _travel;
    private float _filteredSpeed;
    private float _travelSinceMark;
    private Vector2 _tilt;
    private Vector2 _tiltVelocity;
    private DamageStage _damageStage;

    public TankMotion(Node3D model, TankArt art, int startingHitPoints)
    {
        _art = art;
        _startingHitPoints = startingHitPoints;
        _trackMaterial = new ShaderMaterial { Shader = GD.Load<Shader>("res://Shaders/tank_tracks.gdshader") };
        TankArt.BindAtlas(_trackMaterial);
        model.AddChild(new MeshInstance3D
        {
            Name = "TrackBelts", Mesh = art.Tracks, MaterialOverride = _trackMaterial
        });
        _chassis = new Node3D { Name = "Suspension", Position = Vector3.Up * 0.80f };
        Hull = new MeshInstance3D
        {
            Name = "ArmoredHull", Mesh = art.Hull, Position = Vector3.Down * 0.80f
        };
        Hull.SetSurfaceOverrideMaterial(0, art.Intact);
        _chassis.AddChild(Hull);
        _turret = new MeshInstance3D
        {
            Name = "Turret", Mesh = art.Turret, Position = Vector3.Up * 0.63f
        };
        _turret.SetSurfaceOverrideMaterial(0, art.Intact);
        _gun = new MeshInstance3D { Name = "Gun", Mesh = art.Gun, Position = new Vector3(0f, 0.38f, RestGunZ) };
        _gun.SetSurfaceOverrideMaterial(0, art.Intact);
        _turret.AddChild(_gun);
        _chassis.AddChild(_turret);
        model.AddChild(_chassis);
    }

    public Vector3 Muzzle => _gun.ToGlobal(new Vector3(0f, 0f, -2.70f));
    public Mesh TurretMesh => _turret.Mesh;
    public Vector3? EngineSmokeOrigin => _damageStage == DamageStage.Critical
        ? Hull.ToGlobal(new Vector3(0.47f, 1.55f, 1.96f))
        : null;

    public void ShowDamage(int hitPoints)
    {
        var health = 100L * hitPoints;
        var stage = health <= 30L * _startingHitPoints ? DamageStage.Critical
            : health <= 65L * _startingHitPoints ? DamageStage.Worn
            : DamageStage.Intact;
        if (stage == _damageStage)
            return;

        _damageStage = stage;
        var material = stage switch
        {
            DamageStage.Critical => _art.Scorched,
            DamageStage.Worn => _art.Scuffed,
            _ => null
        };
        Hull.MaterialOverride = material;
        _turret.MaterialOverride = material;
    }

    public float? AdvanceTreadMarks(float distance)
    {
        _travelSinceMark += distance;
        if (_travelSinceMark < TreadMarkSpacing)
            return null;
        _travelSinceMark %= TreadMarkSpacing;
        return _travelSinceMark;
    }

    public void Fire(float hullHeading, float aimHeading)
    {
        _turret.Rotation = new Vector3(0f, aimHeading - hullHeading, 0f);
        SetRecoil(1f);
    }

    public void Update(float delta, float speed, float previousHeading, float heading, float? aimHeading, float recoil)
    {
        var turn = Mathf.AngleDifference(previousHeading, heading);
        if (speed > 0.001f || Mathf.Abs(turn) > 0.00001f)
        {
            var travel = speed * delta;
            var steering = turn * TankArt.TrackGauge;
            _travel = (_travel + new Vector2(travel - steering, travel + steering) / TankArt.TrackPitch)
                .PosMod(1f);
            _trackMaterial.SetShaderParameter("travel", _travel);
        }

        // Longitudinal acceleration pitches the hull. Lateral acceleration loads the outside track.
        var poseDelta = Mathf.Min(delta, 0.05f);
        _filteredSpeed = Mathf.Lerp(_filteredSpeed, speed, 1f - Mathf.Exp(-poseDelta * 4.8f));
        var lateralAcceleration = delta > 0f ? speed * turn / delta : 0f;
        var targetTilt = new Vector2(
            Mathf.Clamp((speed - _filteredSpeed) * 0.022f, -0.042f, 0.042f),
            Mathf.Clamp(-lateralAcceleration * 0.018f, -0.095f, 0.095f));
        _tiltVelocity = _tiltVelocity.Lerp((targetTilt - _tilt) * 13f, 1f - Mathf.Exp(-poseDelta * 15f));
        _tilt += _tiltVelocity * poseDelta;
        _chassis.Rotation = new Vector3(_tilt.X, 0f, _tilt.Y);

        var targetAngle = aimHeading is { } angle ? angle - heading : 0f;
        var traverse = 1f - Mathf.Exp(-delta * 8f);
        _turret.Rotation = new Vector3(0f, Mathf.LerpAngle(_turret.Rotation.Y, targetAngle, traverse), 0f);
        SetRecoil(recoil);
    }

    private void SetRecoil(float recoil) => _gun.Position = new Vector3(0f, 0.38f, RestGunZ + recoil * 0.29f);

    public void Dispose() => _trackMaterial.Dispose();

    private enum DamageStage { Intact, Worn, Critical }
}
