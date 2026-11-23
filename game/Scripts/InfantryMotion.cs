using Godot;
using static IronDominion.Game.InfantryRig;

namespace IronDominion.Game;

internal sealed partial class InfantryMotion
{
    public const int BurstShots = 3;
    public const float ShotIntervalSeconds = 0.058f;
    private const float StanceFraction = 0.58f;
    private readonly Skeleton3D _skeleton;
    private readonly MeshInstance3D _skinView;
    private readonly Transform3D[] _pose = new Transform3D[BonesPerSoldier];
    private readonly Vector3[] _muzzles = new Vector3[Formation.Length];

    private float _phase;
    private float _pace;
    private float _suppression;
    private float _cover;
    private float _stationaryCover;
    private float _aim;

    public InfantryMotion(Node3D model, InfantryArt art)
    {
        _skeleton = new Skeleton3D { Name = "InfantryRig" };
        AddBones(_skeleton);
        _skinView = new MeshInstance3D
        {
            Name = "InfantrySkin", Mesh = art.Mesh, Skin = art.Skin, Skeleton = new NodePath(".."),
            CustomAabb = new Aabb(new Vector3(-2.6f, -0.1f, -2.8f), new Vector3(5.2f, 2.3f, 5.6f))
        };
        // Apply the scale before skinning by placing the whole skeleton and its mesh in the same space.
        _skeleton.Scale = Vector3.One * DisplayScale;
        model.AddChild(_skeleton);
        _skeleton.AddChild(_skinView);
        PoseSquad(0f, null);
    }

    public Vector3 Muzzle(int soldier) => _skeleton.ToGlobal(_muzzles[soldier]);

    public static int Shooter(int shot) => shot % 2;

    public void BeginBurst()
    {
        _aim = 1f;
        PoseSquad(Stride(), 0f);
    }

    public void SetRestingStance(bool covered, bool suppressed)
    {
        if (!covered && !suppressed)
            return;

        _cover = covered ? 1f : 0f;
        _stationaryCover = _cover;
        _suppression = suppressed ? 1f : 0f;
        PoseSquad(0f, null);
    }

    public void Update(float delta, float speed, float? shotAge, bool covered, bool suppressed)
    {
        var aiming = shotAge.HasValue;
        var targetPace = Mathf.Clamp(speed / 4f, 0f, 1f);
        var targetSuppression = suppressed ? 1f : 0f;
        var targetCover = covered ? 1f : 0f;
        var targetStationaryCover = covered && targetPace == 0f ? 1f : 0f;
        var targetAim = aiming ? 1f : 0f;
        if (targetPace == 0f && _pace == 0f && targetSuppression == _suppression
            && targetCover == _cover && targetStationaryCover == _stationaryCover && targetAim == 0f && _aim == 0f)
            return;

        _pace = Approach(_pace, targetPace, delta, 9f);
        _suppression = Approach(_suppression, targetSuppression, delta, suppressed ? 13f : 6f);
        _cover = Approach(_cover, targetCover, delta, covered ? 7f : 5f);
        _stationaryCover = Approach(_stationaryCover, targetStationaryCover, delta, targetStationaryCover > 0f ? 4.6f : 10f);
        _aim = Approach(_aim, targetAim, delta, aiming ? 14f : 7f);
        var stride = Stride();
        _phase = Mathf.PosMod(_phase + speed * delta / (DisplayScale * stride), 1f);
        PoseSquad(stride, shotAge);
    }

    private float Stride() => Mathf.Lerp(0.99f, 1.49f, _pace) * (1f - 0.19f * Crouch);

    private float Crouch => Mathf.Max(_suppression, _cover * 0.82f);

    private static float Approach(float value, float target, float delta, float response)
    {
        var next = Mathf.Lerp(value, target, 1f - Mathf.Exp(-response * delta));
        return Mathf.Abs(target - next) <= 0.003f ? target : next;
    }

    private void PoseSquad(float stride, float? shotAge)
    {
        var crouch = Crouch;
        for (var soldier = 0; soldier < Formation.Length; soldier++)
        {
            var recoil = shotAge is { } age ? RifleKick(soldier, age) : 0f;
            var front = soldier < 2;
            var settled = Mathf.SmoothStep(0.05f + soldier * 0.15f, 0.98f, _stationaryCover);
            var kneeSide = front ? (soldier == 0 ? 1 : -1) : 0;
            var aim = _aim * (front ? 1f : 1f - settled * 0.52f);
            var phase = Mathf.PosMod(_phase + soldier * 0.24f, 1f);
            var cycle = phase * Mathf.Tau;
            var shift = Mathf.Sin(cycle) * 0.024f * _pace;
            var bob = Mathf.Cos(cycle * 2f) * 0.020f * _pace;
            var offset = Offset(soldier);
            var pelvisPitch = 0.06f * _pace + 0.13f * crouch;
            var hipDrop = settled * (front ? 0.26f : 0.105f);
            var pelvis = new Transform3D(new Basis(Vector3.Right, pelvisPitch), offset
                + new Vector3(shift + kneeSide * settled * 0.034f,
                    0.902f - 0.105f * _pace - 0.17f * crouch - hipDrop + bob, 0.09f * crouch));
            _pose[Pelvis] = pelvis;

            var watch = front ? 0f : (soldier == 2 ? -0.19f : 0.23f) * settled;
            var twist = (Mathf.Sin(cycle) * 0.16f * _pace + watch) * (1f - 0.72f * aim);
            var chestPitch = 0.045f + 0.17f * _pace + 0.24f * crouch
                - settled * (front ? 0.12f : 0.015f) + 0.12f * _cover * _suppression - 0.047f * recoil;
            var chest = new Transform3D(new Basis(Vector3.Right, chestPitch) * new Basis(Vector3.Up, twist),
                pelvis * (ChestPosition - Joints[Pelvis].BindPosition));
            _pose[Chest] = chest;
            _pose[Head] = new Transform3D(new Basis(Vector3.Right, chestPitch * 0.20f)
                * new Basis(Vector3.Up, watch * (1f - aim)),
                chest * (HeadPosition - ChestPosition));

            var gearPitch = -0.43f * (1f - aim) + 0.055f * recoil;
            var gearPosition = chest * new Vector3(0.112f, 0.095f + aim * 0.132f, -0.19f + recoil * 0.055f);
            var gear = new Transform3D(new Basis(Vector3.Right, gearPitch), gearPosition);
            _pose[Equipment] = gear;
            _muzzles[soldier] = gear * new Vector3(0f, 0.005f, -0.40f);

            PoseLeg(LeftHip, -1, phase, stride, offset, pelvis, crouch, settled, kneeSide);
            PoseLeg(RightHip, 1, Mathf.PosMod(phase + 0.5f, 1f), stride, offset, pelvis, crouch, settled, kneeSide);
            PoseArm(LeftShoulder, -1, chest, gear, new Vector3(-0.022f, -0.036f, -0.20f));
            PoseArm(RightShoulder, 1, chest, gear, new Vector3(0.01f, -0.062f, 0.027f));

            var boneOffset = soldier * BonesPerSoldier;
            for (var bone = 0; bone < Joints.Length; bone++)
                _skeleton.SetBonePose(boneOffset + bone, LocalPose(bone));
        }
    }

    private Transform3D LocalPose(int bone)
    {
        var parent = Joints[bone].Parent;
        return parent < 0 ? _pose[bone] : _pose[parent].Inverse() * _pose[bone];
    }

    private static float RifleKick(int soldier, float age)
    {
        for (var shot = BurstShots - 1; shot >= 0; shot--)
        {
            var sinceShot = age - shot * ShotIntervalSeconds;
            if (Shooter(shot) == soldier && sinceShot >= 0f)
                return Mathf.Max(0f, 1f - sinceShot / 0.10f);
        }
        return 0f;
    }

    private void PoseLeg(int bone, int side, float phase, float stride, Vector3 offset, Transform3D pelvis,
        float crouch, float settled, int kneeSide)
    {
        var swing = Mathf.Clamp((phase - StanceFraction) / (1f - StanceFraction), 0f, 1f);
        // During stance, backward foot travel cancels forward root motion at full pace.
        var stanceTravel = Mathf.Min(phase, StanceFraction) * stride;
        var swingReturn = Mathf.SmoothStep(0f, 1f, swing) * stride * StanceFraction;
        var strideBlend = Mathf.Clamp(_pace / 0.52f, 0f, 1f);
        var z = (stanceTravel - swingReturn - stride * StanceFraction * 0.5f) * strideBlend
            + side * 0.063f * (1f - _pace);
        var lift = Mathf.Sin(swing * Mathf.Pi) * _pace;
        var rearKnee = kneeSide == side;
        var stanceZ = kneeSide == 0 ? side * 0.16f : rearKnee ? 0.55f : -0.40f;
        // Lift the foot over the ground as it repositions between the gait and the low stance.
        var placementLift = settled * (1f - settled) * 0.27f;
        var ankle = offset + new Vector3(side * (HipWidth + 0.020f + 0.016f * crouch + 0.031f * settled),
            AnkleHeight + lift * (0.16f - 0.065f * crouch) * (1f - settled)
                + placementLift + (rearKnee ? 0.065f * settled : 0f),
            Mathf.Lerp(z, stanceZ, settled));
        var hip = pelvis * (Vector3.Right * (side * HipWidth));
        var footPitch = 0.18f * lift * Mathf.Cos(swing * Mathf.Pi) * (1f - settled)
            - (rearKnee ? 0.82f * settled : 0f);
        var foot = new Basis(Vector3.Right, footPitch)
            * new Basis(Vector3.Up, -side * 0.07f);
        var knee = rearKnee ? Vector3.Forward.Lerp(new Vector3(0f, -1f, -0.15f), settled) : Vector3.Forward;
        PoseChain(bone, hip, ankle, knee, Thigh, Shin, foot);
    }

    private void PoseArm(int bone, int side, Transform3D chest, Transform3D gear, Vector3 grip)
    {
        var shoulder = chest * (Joints[bone].BindPosition - ChestPosition);
        var elbowDirection = new Vector3(side * 0.72f, -0.77f, 0.10f);
        var wrist = gear * grip;
        var hand = AlongLimb(wrist - shoulder);
        PoseChain(bone, shoulder, wrist, elbowDirection, UpperArmLength, ForearmLength, hand);
    }

    private void PoseChain(int bone, Vector3 root, Vector3 target, Vector3 pole, float first, float second, Basis end)
    {
        var axis = (target - root).Normalized();
        var distance = Mathf.Clamp(root.DistanceTo(target), Mathf.Abs(first - second) + 0.001f, first + second - 0.001f);
        var reachable = root + axis * distance;
        var along = (first * first - second * second + distance * distance) / (2f * distance);
        var height = Mathf.Sqrt(Mathf.Max(0f, first * first - along * along));
        var joint = root + axis * along + pole.Slide(axis).Normalized() * height;
        _pose[bone] = new Transform3D(AlongLimb(joint - root), root);
        _pose[bone + 1] = new Transform3D(AlongLimb(reachable - joint), joint);
        _pose[bone + 2] = new Transform3D(end, reachable);
    }

    private static Basis AlongLimb(Vector3 direction)
    {
        var y = -direction.Normalized();
        var reference = Mathf.Abs(y.X) < 0.9f ? Vector3.Right : Vector3.Back;
        var x = reference.Slide(y).Normalized();
        return new Basis(x, y, x.Cross(y));
    }
}
