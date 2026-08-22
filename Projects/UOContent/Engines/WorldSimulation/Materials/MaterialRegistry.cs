using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Server.Json;
using Server.Logging;

namespace Server.Engines.WorldSimulation;

public static class MaterialRegistry
{
    private static readonly ILogger _logger = LogFactory.GetLogger(typeof(MaterialRegistry));
    private static readonly JsonSerializerOptions _serializerOptions = new(JsonConfig.DefaultOptions)
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly Dictionary<MaterialId, MaterialDefinition> _definitions = [];
    private static bool _loaded;

    public static void Configure() => EnsureLoaded();

    public static MaterialDefinition Get(MaterialId id)
    {
        EnsureLoaded();
        return _definitions.GetValueOrDefault(id) ?? _definitions[MaterialId.Unknown];
    }

    public static void Load(string path)
    {
        var definitions = JsonConfig.Deserialize<MaterialDefinition[]>(path, _serializerOptions);
        _definitions.Clear();

        if (definitions != null)
        {
            foreach (var definition in definitions)
            {
                if (definition != null)
                {
                    _definitions[definition.Id] = definition;
                }
            }
        }

        AddUnknownFallback();
        _loaded = true;
    }

    internal static void ResetForTesting(params MaterialDefinition[] definitions)
    {
        _definitions.Clear();

        foreach (var definition in definitions)
        {
            _definitions[definition.Id] = definition;
        }

        AddUnknownFallback();
        _loaded = true;
    }

    private static void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        var path = Path.Combine(Core.BaseDirectory, "Data", "world-simulation", "materials.json");

        try
        {
            Load(path);
        }
        catch (Exception ex)
        {
            _logger.Warning(ex, "Unable to load world simulation materials from {Path}", path);
            _definitions.Clear();
            AddUnknownFallback();
            _loaded = true;
        }
    }

    private static void AddUnknownFallback()
    {
        _definitions.TryAdd(
            MaterialId.Unknown,
            new MaterialDefinition
            {
                Id = MaterialId.Unknown,
                Name = "Unknown",
                HeatCapacity = 1.0,
                ThermalConductivity = 0.1,
                WaterResistance = 0.5
            }
        );
    }
}
