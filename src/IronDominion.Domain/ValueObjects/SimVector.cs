namespace IronDominion.Domain;

public readonly record struct SimVector
{
    public SimVector(float x, float z)
    {
        if (!float.IsFinite(x))
            throw new ArgumentOutOfRangeException(nameof(x), x, "The coordinate must be finite.");
        if (!float.IsFinite(z))
            throw new ArgumentOutOfRangeException(nameof(z), z, "The coordinate must be finite.");

        X = x;
        Z = z;
    }

    public float X { get; }
    public float Z { get; }
}
