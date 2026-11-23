using Godot;
using static IronDominion.Game.InfantryRig;

namespace IronDominion.Game;

internal sealed partial class InfantryMotion
{
    public const float FallSeconds = 0.65f;
    private Transform3D[] _atHit = [];
    private Transform3D[] _buckled = [];
    private Transform3D[] _grounded = [];

    public Vector3[] BeginFall()
    {
        var boneCount = _skeleton.GetBoneCount();
        _atHit = new Transform3D[boneCount];
        _buckled = new Transform3D[boneCount];
        _grounded = new Transform3D[boneCount];
        var contacts = new Vector3[Formation.Length];
        for (var bone = 0; bone < boneCount; bone++)
            _atHit[bone] = _skeleton.GetBonePose(bone);

        for (var soldier = 0; soldier < Formation.Length; soldier++)
        {
            BuildFallPose(soldier, grounded: false);
            StorePose(_buckled, soldier);
            BuildFallPose(soldier, grounded: true);
            StorePose(_grounded, soldier);
            contacts[soldier] = _skeleton.ToGlobal(_pose[Chest].Origin);
        }

        if (_skinView.Mesh.SurfaceGetMaterial(0) is ShaderMaterial uniform)
        {
            using var stained = (ShaderMaterial)uniform.Duplicate();
            stained.SetShaderParameter("casualty_stains", true);
            _skinView.MaterialOverride = stained;
        }

        _skinView.CustomAabb = new Aabb(new Vector3(-3f, -0.2f, -3.8f), new Vector3(6f, 2.6f, 7.2f));
        return contacts;
    }

    public void PoseFall(float age)
    {
        for (var soldier = 0; soldier < Formation.Length; soldier++)
        {
            var buckleAt = 0.20f + soldier * 0.035f;
            var buckling = age < buckleAt;
            var elapsed = buckling ? age / buckleAt : (age - buckleAt) / (FallSeconds - buckleAt);
            var progress = Mathf.Clamp(elapsed, 0f, 1f);
            // The knees give way first, then the torso accelerates down before settling.
            var weight = buckling ? Mathf.SmoothStep(0f, 1f, progress)
                : progress * progress * (1.45f - progress * 0.45f);
            var start = buckling ? _atHit : _buckled;
            var finish = buckling ? _buckled : _grounded;
            var firstBone = soldier * BonesPerSoldier;
            for (var bone = firstBone; bone < firstBone + BonesPerSoldier; bone++)
                _skeleton.SetBonePose(bone, start[bone].InterpolateWith(finish[bone], weight));
        }
    }

    private void StorePose(Transform3D[] frame, int soldier)
    {
        for (var bone = 0; bone < BonesPerSoldier; bone++)
            frame[soldier * BonesPerSoldier + bone] = LocalPose(bone);
    }

    private void BuildFallPose(int soldier, bool grounded)
    {
        var side = soldier % 2 == 0 ? -1f : 1f;
        var yaw = side * (0.20f + soldier * 0.075f);
        var facing = new Basis(Vector3.Up, yaw);
        var tilt = new Basis(Vector3.Right, grounded ? -1.48f : -0.45f);
        var offset = Offset(soldier);
        var pelvis = new Transform3D(facing * tilt, offset
            + new Vector3(side * 0.07f, grounded ? 0.22f : 0.57f, grounded ? -0.29f : 0.03f));
        var chest = pelvis.TranslatedLocal(ChestPosition - Joints[Pelvis].BindPosition);
        _pose[Pelvis] = pelvis;
        _pose[Chest] = chest;
        _pose[Head] = new Transform3D(chest.Basis * new Basis(Vector3.Up, side * 0.15f),
            chest * (HeadPosition - ChestPosition));
        _pose[Equipment] = new Transform3D(chest.Basis * new Basis(Vector3.Right, -0.42f),
            chest * new Vector3(0.27f * side, 0.18f, -0.025f));

        for (var limb = -1; limb <= 1; limb += 2)
        {
            var hipBone = limb < 0 ? LeftHip : RightHip;
            var shoulderBone = limb < 0 ? LeftShoulder : RightShoulder;
            var ankle = offset + facing * new Vector3(limb * (grounded ? 0.17f : 0.12f),
                AnkleHeight, grounded ? 0.43f + limb * 0.065f : limb * 0.10f);
            var hip = pelvis * (Vector3.Right * (limb * HipWidth));
            var foot = grounded ? facing * new Basis(Vector3.Right, -0.62f) : facing;
            PoseChain(hipBone, hip, ankle, grounded ? Vector3.Up : facing * Vector3.Forward, Thigh, Shin, foot);

            var shoulder = chest * (Joints[shoulderBone].BindPosition - ChestPosition);
            var wrist = chest * new Vector3(limb * (grounded ? 0.38f : 0.36f),
                grounded ? 0.47f - limb * side * 0.13f : -0.04f, grounded ? -0.02f : -0.28f);
            if (grounded)
                wrist.Y = 0.088f;
            PoseChain(shoulderBone, shoulder, wrist, facing * new Vector3(limb, -0.22f, 0.15f),
                UpperArmLength, ForearmLength, AlongLimb(wrist - shoulder));
        }
    }
}
