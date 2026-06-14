using System.Text.Json;
using DockLauncher.BuildingBlocks.Application.Contracts;

namespace DockLauncher.BuildingBlocks.Infrastructure.Serialization;

public sealed class SystemTextJsonSerializer : IJsonSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, Options);
    }

    public T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, Options);
    }
}