using Godot;

namespace IronDominion.Game;

internal static class InfantryRig
{
    public const int BonesPerSoldier = 16;
    public const float DisplayScale = 1.15f;
    public const float Thigh = 0.43f;
    public const float Shin = 0.42f;
    public const float UpperArmLength = 0.285f;
    public const float ForearmLength = 0.265f;
    public const float AnkleHeight = 0.09f;

    public static readonly Vector3[] Formation =
    [
        new(-1.24f, 0f, -1.28f), new(1.24f, 0f, -0.58f),
        new(-1.30f, 0f, 1.16f), new(1.27f, 0f, 1.48f)
    ];

    public readonly record struct Joint(string Name, int Parent, Vector3 BindPosition);
    public const int Pelvis = 0;
    public const int Chest = 1;
    public const int Head = 2;
    public const int LeftHip = 3;
    public const int RightHip = 6;
    public const int LeftShoulder = 9;
    public const int RightShoulder = 12;
    public const int Equipment = 15;
    public const float HipHeight = AnkleHeight + Thigh + Shin;
    public const float HipWidth = 0.098f;
    public const float ShoulderWidth = 0.208f;
    public const float ShoulderHeight = 1.457f;
    public static readonly Vector3 ChestPosition = new(0f, 1.17f, 0f);
    public static readonly Vector3 HeadPosition = new(0f, 1.545f, -0.010f);
    public static readonly Vector3 EquipmentPosition = new(0.105f, 1.35f, -0.22f);

    public static readonly Joint[] Joints =
    [
        new("Pelvis", -1, Vector3.Up * HipHeight),
        new("Chest", Pelvis, ChestPosition),
        new("Head", Chest, HeadPosition),
        new("HipL", Pelvis, new(-HipWidth, HipHeight, 0f)),
        new("KneeL", LeftHip, new(-HipWidth, AnkleHeight + Shin, 0f)),
        new("AnkleL", LeftHip + 1, new(-HipWidth, AnkleHeight, 0f)),
        new("HipR", Pelvis, new(HipWidth, HipHeight, 0f)),
        new("KneeR", RightHip, new(HipWidth, AnkleHeight + Shin, 0f)),
        new("AnkleR", RightHip + 1, new(HipWidth, AnkleHeight, 0f)),
        new("ShoulderL", Chest, new(-ShoulderWidth, ShoulderHeight, 0f)),
        new("ElbowL", LeftShoulder, new(-ShoulderWidth, ShoulderHeight - UpperArmLength, 0f)),
        new("WristL", LeftShoulder + 1, new(-ShoulderWidth, ShoulderHeight - UpperArmLength - ForearmLength, 0f)),
        new("ShoulderR", Chest, new(ShoulderWidth, ShoulderHeight, 0f)),
        new("ElbowR", RightShoulder, new(ShoulderWidth, ShoulderHeight - UpperArmLength, 0f)),
        new("WristR", RightShoulder + 1, new(ShoulderWidth, ShoulderHeight - UpperArmLength - ForearmLength, 0f)),
        new("Equipment", Chest, EquipmentPosition)
    ];

    public static Vector3 Offset(int soldier) => Formation[soldier] / DisplayScale;

    public static void AddBones(Skeleton3D skeleton)
    {
        for (var soldier = 0; soldier < Formation.Length; soldier++)
        {
            var offset = soldier * BonesPerSoldier;
            for (var i = 0; i < Joints.Length; i++)
            {
                var joint = Joints[i];
                var bone = skeleton.AddBone($"S{soldier}_{joint.Name}");
                if (joint.Parent >= 0)
                    skeleton.SetBoneParent(bone, offset + joint.Parent);
                var local = joint.Parent >= 0
                    ? joint.BindPosition - Joints[joint.Parent].BindPosition
                    : joint.BindPosition + Offset(soldier);
                skeleton.SetBoneRest(bone, new Transform3D(Basis.Identity, local));
            }
        }
        skeleton.ResetBonePoses();
    }

    public static Skin CreateSkin()
    {
        var skin = new Skin();
        for (var soldier = 0; soldier < Formation.Length; soldier++)
        for (var i = 0; i < Joints.Length; i++)
            skin.AddBind(soldier * BonesPerSoldier + i,
                new Transform3D(Basis.Identity, -(Joints[i].BindPosition + Offset(soldier))));
        return skin;
    }
}
