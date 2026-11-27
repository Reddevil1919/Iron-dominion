using System.Collections.Immutable;

namespace IronDominion.Domain;

public sealed class GameConfiguration
{
    public const int CurrentSchemaVersion = 1;
    public const int FixedTickRate = 20;

    public GameConfiguration(
        int schemaVersion,
        int tickRate,
        int startingCredits,
        int headquartersIncomePerSecond,
        int sectorIncomePerSecond,
        int captureSeconds,
        float captureRadiusMeters,
        decimal infantryCoverMultiplier,
        ImmutableArray<UnitDefinition> units,
        ImmutableArray<BuildingDefinition> buildings,
        SuppressionRules? suppression = null)
    {
        if (schemaVersion != CurrentSchemaVersion)
            throw new ArgumentOutOfRangeException(nameof(schemaVersion), schemaVersion, "Unsupported configuration version.");
        if (tickRate != FixedTickRate)
            throw new ArgumentOutOfRangeException(nameof(tickRate), tickRate, $"The simulation runs at {FixedTickRate} Hz.");

        ArgumentOutOfRangeException.ThrowIfNegative(startingCredits);
        ArgumentOutOfRangeException.ThrowIfNegative(headquartersIncomePerSecond);
        ArgumentOutOfRangeException.ThrowIfNegative(sectorIncomePerSecond);
        ArgumentOutOfRangeException.ThrowIfNegative(captureSeconds);
        RequireNonnegativeFinite(captureRadiusMeters, nameof(captureRadiusMeters));
        if (infantryCoverMultiplier is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(infantryCoverMultiplier), infantryCoverMultiplier, "The multiplier must be between 0 and 1.");

        ValidateUnits(units);
        ValidateBuildings(buildings);

        SchemaVersion = schemaVersion;
        TickRate = tickRate;
        StartingCredits = startingCredits;
        HeadquartersIncomePerSecond = headquartersIncomePerSecond;
        SectorIncomePerSecond = sectorIncomePerSecond;
        CaptureSeconds = captureSeconds;
        CaptureRadiusMeters = captureRadiusMeters;
        InfantryCoverMultiplier = infantryCoverMultiplier;
        Units = units;
        Buildings = buildings;
        Suppression = suppression;
    }

    public int SchemaVersion { get; }
    public int TickRate { get; }
    public int StartingCredits { get; }
    public int HeadquartersIncomePerSecond { get; }
    public int SectorIncomePerSecond { get; }
    public int CaptureSeconds { get; }
    public float CaptureRadiusMeters { get; }
    public decimal InfantryCoverMultiplier { get; }
    public ImmutableArray<UnitDefinition> Units { get; }
    public ImmutableArray<BuildingDefinition> Buildings { get; }
    public SuppressionRules? Suppression { get; }

    private static void ValidateUnits(ImmutableArray<UnitDefinition> units)
    {
        if (units.IsDefaultOrEmpty)
            throw new ArgumentException("Unit definitions are required.", nameof(units));

        var kinds = new HashSet<UnitKind>();
        for (var i = 0; i < units.Length; i++)
        {
            var unit = units[i] ?? throw new ArgumentException($"Null definition at units[{i}].", nameof(units));
            if (!Enum.IsDefined(unit.Kind) || !kinds.Add(unit.Kind))
                throw new ArgumentException($"Unknown or duplicate unit kind at units[{i}].kind: {unit.Kind}.", nameof(units));

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unit.MaxHp, $"units[{i}].maxHp");
            ArgumentOutOfRangeException.ThrowIfNegative(unit.Cost, $"units[{i}].cost");
            ArgumentOutOfRangeException.ThrowIfNegative(unit.BuildTicks, $"units[{i}].buildTicks");
            ArgumentOutOfRangeException.ThrowIfNegative(unit.DamagePerShot, $"units[{i}].damagePerShot");
            ArgumentOutOfRangeException.ThrowIfNegative(unit.CooldownTicks, $"units[{i}].cooldownTicks");
            RequireNonnegativeFinite(unit.SpeedMetersPerSecond, $"units[{i}].speedMetersPerSecond");
            RequireNonnegativeFinite(unit.AttackRangeMeters, $"units[{i}].attackRangeMeters");
        }

        if (kinds.Count != Enum.GetValues<UnitKind>().Length)
            throw new ArgumentException("A definition is required for every unit kind.", nameof(units));
    }

    private static void ValidateBuildings(ImmutableArray<BuildingDefinition> buildings)
    {
        if (buildings.IsDefaultOrEmpty)
            throw new ArgumentException("Building definitions are required.", nameof(buildings));

        var kinds = new HashSet<BuildingKind>();
        for (var i = 0; i < buildings.Length; i++)
        {
            var building = buildings[i] ?? throw new ArgumentException($"Null definition at buildings[{i}].", nameof(buildings));
            if (!Enum.IsDefined(building.Kind) || !kinds.Add(building.Kind))
                throw new ArgumentException($"Unknown or duplicate building kind at buildings[{i}].kind: {building.Kind}.", nameof(buildings));

            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(building.MaxHp, $"buildings[{i}].maxHp");
            if (building.Construction is { } construction)
            {
                ArgumentOutOfRangeException.ThrowIfNegative(construction.Cost, $"buildings[{i}].construction.cost");
                ArgumentOutOfRangeException.ThrowIfNegativeOrZero(construction.BuildTicks, $"buildings[{i}].construction.buildTicks");
                if (!float.IsFinite(construction.Width) || !float.IsFinite(construction.Depth)
                    || construction.Width <= 0 || construction.Depth <= 0)
                    throw new ArgumentException($"Invalid construction footprint at buildings[{i}].", nameof(buildings));
            }
        }

        if (!kinds.Contains(BuildingKind.Headquarters) || !kinds.Contains(BuildingKind.Barracks))
            throw new ArgumentException("A definition is required for every building kind.", nameof(buildings));
    }

    private static void RequireNonnegativeFinite(float value, string name)
    {
        if (!float.IsFinite(value) || value < 0)
            throw new ArgumentOutOfRangeException(name, value, "The value must be finite and nonnegative.");
    }
}
