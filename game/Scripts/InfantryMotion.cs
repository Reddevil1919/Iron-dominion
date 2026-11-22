using Godot;
using static IronDominion.Game.InfantryRig;

namespace IronDominion.Game;

internal sealed class InfantryMotion
{
    private const float StanceFraction = 0.58f;
    private readonly Skeleton3D _skeleton;
    private readonly Transform3D[] _pose = new Transform3D[BonesPerSoldier];

    private float _phase;
    private float _pace;
    private float _crouch;
    private float _aim;
    private Vector3 _muzzle;

    public InfantryMotion(Node3D model, InfantryArt art)
    {
        _skeleton = new Skeleton3D { Name = "InfantryRig" };
        AddBones(_skeleton);
        var skinView = new MeshInstance3D
        {
            Name = "InfantrySkin", Mesh = art.Mesh, Skin = art.Skin, Skeleton = new NodePath(".."),
            CustomAabb = new Aabb(new Vector3(-2.6f, -0.1f, -2.8f), new Vector3(5.2f, 2.3f, 5.6f))
        };
        // Apply the scale before skinning by placing the whole skeleton and its mesh in the same space.
        _skeleton.Scale = Vector3.One * DisplayScale;
        model.AddChild(_skeleton);
        _skeleton.AddChild(skinView);
        PoseSquad(0f, 0f);
    }

    public Vector3 Muzzle => _skeleton.ToGlobal(_muzzle);

    public void Update(float delta, float speed, bool aiming, float recoil, bool suppressed)
    {
        var targetPace = Mathf.Clamp(speed / 4f, 0f, 1f);
        var targetCrouch = suppressed ? 1f : 0f;
        var targetAim = aiming ? 1f : 0f;
        if (targetPace == 0f && _pace == 0f && targetCrouch == _crouch && targetAim == 0f && _aim == 0f)
            return;

        _pace = Approach(_pace, targetPace, delta, 9f);
        _crouch = Approach(_crouch, targetCrouch, delta, 11f);
        _aim = Approach(_aim, targetAim, delta, aiming ? 14f : 7f);
        var stride = Mathf.Lerp(0.99f, 1.49f, _pace) * (1f - 0.12f * _crouch);
        _phase = Mathf.PosMod(_phase + speed * delta / (DisplayScale * stride), 1f);
        PoseSquad(stride, recoil);
    }

    private static float Approach(float value, float target, float delta, float response)
    {
        var next = Mathf.Lerp(value, target, 1f - Mathf.Exp(-response * delta));
        return Mathf.Abs(target - next) <= 0.003f ? target : next;
    }

    private void PoseSquad(float stride, float recoil)
    {
        for (var soldier = 0; soldier < Formation.Length; soldier++)
        {
            var phase = Mathf.PosMod(_phase + soldier * 0.24f, 1f);
            var cycle = phase * Mathf.Tau;
            var shift = Mathf.Sin(cycle) * 0.024f * _pace;
            var bob = Mathf.Cos(cycle * 2f) * 0.020f * _pace;
            var offset = Offset(soldier);
            var pelvisPitch = 0.06f * _pace + 0.13f * _crouch;
            var pelvis = new Transform3D(new Basis(Vector3.Right, pelvisPitch), offset
                + new Vector3(shift, 0.902f - 0.105f * _pace - 0.17f * _crouch + bob, 0.09f * _crouch));
            _pose[Pelvis] = pelvis;

            var twist = Mathf.Sin(cycle) * 0.16f * _pace * (1f - 0.72f * _aim);
            var chestPitch = 0.045f + 0.17f * _pace + 0.24f * _crouch - 0.047f * recoil;
            var chest = new Transform3D(new Basis(Vector3.Right, chestPitch) * new Basis(Vector3.Up, twist),
                pelvis * (ChestPosition - Joints[Pelvis].BindPosition));
            _pose[Chest] = chest;
            _pose[Head] = new Transform3D(new Basis(Vector3.Right, chestPitch * 0.20f),
                chest * (HeadPosition - ChestPosition));

            var gearPitch = -0.43f * (1f - _aim) + 0.055f * recoil;
            var gearPosition = chest * new Vector3(0.112f, 0.095f + _aim * 0.132f, -0.19f + recoil * 0.055f);
            var gear = new Transform3D(new Basis(Vector3.Right, gearPitch), gearPosition);
            _pose[Equipment] = gear;
            if (soldier == 0)
                _muzzle = gear * new Vector3(0f, 0.005f, -0.40f);

            PoseLeg(LeftHip, -1, phase, stride, offset, pelvis);
            PoseLeg(RightHip, 1, Mathf.PosMod(phase + 0.5f, 1f), stride, offset, pelvis);
            PoseArm(LeftShoulder, -1, chest, gear, new Vector3(-0.022f, -0.036f, -0.20f));
            PoseArm(RightShoulder, 1, chest, gear, new Vector3(0.01f, -0.062f, 0.027f));

            var boneOffset = soldier * BonesPerSoldier;
            for (var bone = 0; bone < Joints.Length; bone++)
            {
                var parent = Joints[bone].Parent;
                var local = parent < 0 ? _pose[bone] : _pose[parent].Inverse() * _pose[bone];
                _skeleton.SetBonePose(boneOffset + bone, local);
            }
        }
    }

    private void PoseLeg(int bone, int side, float phase, float stride, Vector3 offset, Transform3D pelvis)
    {
        var swing = Mathf.Clamp((phase - StanceFraction) / (1f - StanceFraction), 0f, 1f);
        // During stance, backward foot travel cancels forward root motion at full pace.
        var stanceTravel = Mathf.Min(phase, StanceFraction) * stride;
        var swingReturn = Mathf.SmoothStep(0f, 1f, swing) * stride * StanceFraction;
        var strideBlend = Mathf.Clamp(_pace / 0.52f, 0f, 1f);
        var z = (stanceTravel - swingReturn - stride * StanceFraction * 0.5f) * strideBlend
            + side * 0.063f * (1f - _pace);
        var lift = Mathf.Sin(swing * Mathf.Pi) * _pace;
        var ankle = offset + new Vector3(side * (HipWidth + 0.020f + 0.016f * _crouch),
            AnkleHeight + lift * (0.16f - 0.065f * _crouch), z);
        var hip = pelvis * (Vector3.Right * (side * HipWidth));
        var foot = new Basis(Vector3.Right, 0.18f * lift * Mathf.Cos(swing * Mathf.Pi))
            * new Basis(Vector3.Up, -side * 0.07f);
        PoseChain(bone, hip, ankle, Vector3.Forward, Thigh, Shin, foot);
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
