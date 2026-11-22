using System.Security.Cryptography;
using System.Text.Json;
using IronDominion.Domain;

namespace IronDominion.Infrastructure.Persistence;

public sealed class MatchSaveFile(string path, string battlefield, GameConfiguration configuration)
{
    private const int CurrentSchemaVersion = 1;
    private const int MaximumBytes = 8 * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        RespectRequiredConstructorParameters = true,
        RespectNullableAnnotations = true,
        Converters = { new EntityIdConverter(), new PlayerIdConverter(), new SimVectorConverter() }
    };
    private readonly string _path = Path.GetFullPath(path);
    private readonly string _configurationHash = Convert.ToHexString(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(configuration, Json)));

    public bool Exists => File.Exists(_path);

    public void Write<T>(T state) where T : class
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new Document<T>(CurrentSchemaVersion, battlefield,
            configuration.SchemaVersion, _configurationHash, state), Json);
        if (bytes.Length > MaximumBytes)
            throw new InvalidDataException("The match exceeds the save size limit.");

        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".match-{Guid.NewGuid():N}.tmp");
        try
        {
            using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                file.Write(bytes);
                file.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }

    public T Read<T>() where T : class
    {
        using var file = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length is <= 0 or > MaximumBytes)
            throw new InvalidDataException("The save file size is invalid.");

        var bytes = new byte[(int)file.Length];
        file.ReadExactly(bytes);
        try
        {
            using var parsed = JsonDocument.Parse(bytes);
            var root = parsed.RootElement;
            if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("schemaVersion", out var version)
                || version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number))
                throw new InvalidDataException("The save file has no valid format version.");
            if (number != CurrentSchemaVersion)
                throw new InvalidDataException($"Save format {number} is unsupported.");

            var saved = root.Deserialize<Document<T>>(Json)
                ?? throw new InvalidDataException("The save file is empty.");
            if (saved.Battlefield != battlefield || saved.ConfigurationVersion != configuration.SchemaVersion
                || saved.ConfigurationHash != _configurationHash)
                throw new InvalidDataException("This save requires a different battlefield or game configuration.");
            return saved.State ?? throw new InvalidDataException("The save file has no match state.");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("The save file is incomplete or contains invalid data.", exception);
        }
    }

    private sealed record Document<T>(int SchemaVersion, string Battlefield, int ConfigurationVersion,
        string ConfigurationHash, T State) where T : class;
}
