using System.Text.Json;
using System.Text.Json.Serialization;
using IronDominion.Domain;

namespace IronDominion.Infrastructure.Configuration;

public static class GameConfigurationLoader
{
    private const string DefaultResourceName = "IronDominion.DefaultGameConfiguration.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowDuplicateProperties = false,
        Converters =
        {
            new JsonStringEnumConverter<UnitKind>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<BuildingKind>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    public static GameConfiguration LoadDefault()
    {
        using var json = typeof(GameConfigurationLoader).Assembly.GetManifestResourceStream(DefaultResourceName)
            ?? throw new InvalidDataException($"Missing embedded configuration: {DefaultResourceName}.");

        return Load(json);
    }

    public static GameConfiguration Load(Stream json)
    {
        ArgumentNullException.ThrowIfNull(json);

        try
        {
            return JsonSerializer.Deserialize<GameConfiguration>(json, Options)
                ?? throw new InvalidDataException("The game configuration must be a JSON object.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"Invalid game configuration JSON: {exception.Message}", exception);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException($"Invalid game configuration value: {exception.Message}", exception);
        }
    }
}
