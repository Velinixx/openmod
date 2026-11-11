using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenMod.Desktop.Models;

/// <summary>Mirror of the preset JSON consumed by openmod-core.</summary>
public sealed class PresetData
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("effects")]
    public List<PresetEffect> Effects { get; set; } = [];
}

public sealed class PresetEffect
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("params")]
    public Dictionary<string, double> Params { get; set; } = [];
}
