using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using IronDominion.Infrastructure.Configuration;
using Xunit;

namespace IronDominion.Domain.Tests;

public sealed class GameConfigurationTests
{
    [Fact]
    public void LoadDefault_UsesDocumentedMvpValues()
    {
        var configuration = GameConfigurationLoader.LoadDefault();

        Assert.Equal(1, configuration.SchemaVersion);
        Assert.Equal(20, configuration.TickRate);
        Assert.Equal(500, configuration.StartingCredits);
        Assert.Equal(5, configuration.HeadquartersIncomePerSecond);
        Assert.Equal(2, configuration.SectorIncomePerSecond);
        Assert.Equal(10, configuration.CaptureSeconds);
        Assert.Equal(6f, configuration.CaptureRadiusMeters);
        Assert.Equal(0.7m, configuration.InfantryCoverMultiplier);
        Assert.Collection(configuration.Units,
            unit => Assert.Equal(new UnitDefinition(UnitKind.InfantrySquad, 400, 100, 160, 4, 12, 10, 20), unit),
            unit => Assert.Equal(new UnitDefinition(UnitKind.Tank, 600, 500, 400, 3, 16, 45, 40), unit));
        Assert.Collection(configuration.Buildings,
            building => Assert.Equal(new BuildingDefinition(BuildingKind.Headquarters, 1500), building),
            building => Assert.Equal(new BuildingDefinition(BuildingKind.Barracks, 700), building));
    }

    [Theory]
    [InlineData("schemaVersion", "2")]
    [InlineData("tickRate", "60")]
    [InlineData("startingCredits", "-1")]
    [InlineData("headquartersIncomePerSecond", "-1")]
    [InlineData("sectorIncomePerSecond", "-1")]
    [InlineData("captureSeconds", "-1")]
    [InlineData("captureRadiusMeters", "-1")]
    [InlineData("captureRadiusMeters", "1e1000")]
    [InlineData("infantryCoverMultiplier", "-0.01")]
    [InlineData("infantryCoverMultiplier", "1.01")]
    public void Load_RejectsInvalidSettings(string field, string value)
    {
        var json = DefaultJson();
        json[field] = JsonNode.Parse(value);

        AssertInvalid(json.ToJsonString(), field);
    }

    [Theory]
    [InlineData("units", "maxHp", "0")]
    [InlineData("units", "cost", "-1")]
    [InlineData("units", "buildTicks", "-1")]
    [InlineData("units", "speedMetersPerSecond", "-1")]
    [InlineData("units", "attackRangeMeters", "-1")]
    [InlineData("units", "attackRangeMeters", "1e1000")]
    [InlineData("units", "damagePerShot", "-1")]
    [InlineData("units", "cooldownTicks", "-1")]
    [InlineData("units", "kind", "\"aircraft\"")]
    [InlineData("units", "kind", "0")]
    [InlineData("buildings", "maxHp", "-1")]
    public void Load_RejectsInvalidDefinitions(string collection, string field, string value)
    {
        var json = DefaultJson();
        json[collection]![0]![field] = JsonNode.Parse(value);

        AssertInvalid(json.ToJsonString(), field);
    }

    [Theory]
    [InlineData("units")]
    [InlineData("buildings")]
    public void Load_RequiresOneDefinitionPerKind(string collection)
    {
        var missing = DefaultJson();
        missing[collection]!.AsArray().RemoveAt(1);
        AssertInvalid(missing.ToJsonString(), collection);

        var duplicate = DefaultJson();
        duplicate[collection]![1] = duplicate[collection]![0]!.DeepClone();
        AssertInvalid(duplicate.ToJsonString(), "duplicate");

        var nullDefinition = DefaultJson();
        nullDefinition[collection]![0] = null;
        AssertInvalid(nullDefinition.ToJsonString(), collection);
    }

    [Fact]
    public void Load_RejectsMissingUnexpectedAndDuplicateProperties()
    {
        var missing = DefaultJson();
        missing["units"]![0]!.AsObject().Remove("cost");
        AssertInvalid(missing.ToJsonString(), "cost");

        var unexpected = DefaultJson();
        unexpected["tickrate"] = 20;
        AssertInvalid(unexpected.ToJsonString(), "tickrate");

        var duplicate = "{\"tickRate\":20," + DefaultJson().ToJsonString()[1..];
        AssertInvalid(duplicate, "tickRate");
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[20]")]
    [InlineData("{")]
    public void Load_RejectsInvalidRoot(string json)
    {
        AssertInvalid(json, "configuration");
    }

    [Fact]
    public void Load_AllowsZeroForCostRateRangeAndDuration()
    {
        var json = DefaultJson();
        json["headquartersIncomePerSecond"] = 0;
        json["captureSeconds"] = 0;
        json["captureRadiusMeters"] = 0;
        json["units"]![0]!["cost"] = 0;
        json["units"]![0]!["attackRangeMeters"] = 0;

        var configuration = Read(json.ToJsonString());

        Assert.Equal(0, configuration.HeadquartersIncomePerSecond);
        Assert.Equal(0, configuration.CaptureSeconds);
        Assert.Equal(0f, configuration.CaptureRadiusMeters);
        Assert.Equal(0, configuration.Units[0].Cost);
        Assert.Equal(0f, configuration.Units[0].AttackRangeMeters);
    }

    private static JsonObject DefaultJson()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        };

        return JsonSerializer.SerializeToNode(GameConfigurationLoader.LoadDefault(), options)!.AsObject();
    }

    private static void AssertInvalid(string json, string expectedField)
    {
        var exception = Assert.Throws<InvalidDataException>(() => Read(json));
        Assert.Contains(expectedField, exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GameConfiguration Read(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return GameConfigurationLoader.Load(stream);
    }
}
