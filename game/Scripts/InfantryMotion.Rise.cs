using Godot;
using static IronDominion.Game.InfantryRig;

namespace IronDominion.Game;

internal sealed partial class InfantryMotion
{
    private const float KneelingThreshold = 0.82f;
    private const float RiseDuration = 0.52f;
    private const float RiseStagger = 0.043f;
    private const float RiseEnd = RiseDuration + RiseStagger;
    private readonly KneelingPose[] _kneeling = new KneelingPose[2];
    private Transform3D _kneelingFrame;
    private float _riseAge = RiseEnd;
    private bool _wasMoving;

    private bool IsRising => _riseAge < RiseEnd;

    private bool AdvanceRise(float delta, bool moving)
    {
        if (moving && !_wasMoving && !IsRising && _stationaryCover >= KneelingThreshold)
        {
            // Capture the last kneeling pose before the interpolated squad root moves away from it.
            for (var soldier = 0; soldier < _kneeling.Length; soldier++)
            {
                var first = soldier * BonesPerSoldier;
                var left = _skeleton.GetBoneGlobalPose(first + LeftHip + 2);
                var right = _skeleton.GetBoneGlobalPose(first + RightHip + 2);
                _kneeling[soldier] = new KneelingPose(_skeleton.GetBoneGlobalPose(first + Pelvis),
                    soldier == 0 ? left : right, soldier == 0 ? right : left);
            }
            _riseAge = 0f;
        }
        _wasMoving = moving;
        var wasRising = IsRising;
        _riseAge = Mathf.Min(_riseAge + delta, RiseEnd);
        return wasRising;
    }

    private RisePose SampleRise(int soldier, Transform3D worldToRig)
    {
        var kneelToRig = worldToRig * _kneelingFrame;
        var pose = _kneeling[soldier];
        var elapsed = _riseAge - soldier * RiseStagger;
        return new RisePose(
            kneelToRig * pose.Pelvis, kneelToRig * pose.FrontBoot, kneelToRig * pose.RearBoot,
            Mathf.SmoothStep(0f, 0.15f, elapsed), Mathf.SmoothStep(0.12f, 0.31f, elapsed),
            Mathf.SmoothStep(0.09f, 0.40f, elapsed), Mathf.SmoothStep(0.23f, RiseDuration, elapsed));
    }

    private readonly record struct KneelingPose(Transform3D Pelvis, Transform3D FrontBoot, Transform3D RearBoot);

    private readonly record struct RisePose(Transform3D Pelvis, Transform3D FrontBoot, Transform3D RearBoot,
        float Load, float Push, float RearSwing, float Release)
    {
        public float ForwardLean => 0.36f * Load * (1f - Push);

        public Transform3D MovePelvis(Transform3D gait)
        {
            var support = FrontBoot.Origin - Pelvis.Origin;
            // Shift over the planted boot at knee height before straightening the leg.
            var origin = Pelvis.Origin + support.Slide(Vector3.Up) * (0.69f * Load) + Vector3.Up * (0.19f * Push);
            return new Transform3D(Pelvis.Basis.Slerp(gait.Basis, Release), origin.Lerp(gait.Origin, Release));
        }

        public Transform3D MoveBoot(Transform3D gait, bool rear)
        {
            var progress = rear ? RearSwing : Release;
            var boot = (rear ? RearBoot : FrontBoot).InterpolateWith(gait, progress);
            return boot.Translated(Vector3.Up * (Mathf.Sin(progress * Mathf.Pi) * (rear ? 0.15f : 0.095f)));
        }
    }
}
