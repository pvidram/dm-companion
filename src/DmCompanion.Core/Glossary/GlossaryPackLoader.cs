using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Core.Glossary;

public sealed class GlossaryPackLoader(IGlossaryPackSource source, ILogger<GlossaryPackLoader> logger)
{
    private readonly Lazy<PackLoad<IReadOnlyList<GlossaryListing>>> _listings = new(
        () => Load<GlossaryIndexFile, IReadOnlyList<GlossaryListing>>(source, logger, GlossaryPacks.IndexPack, f => f.Listings, []),
        LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly IReadOnlyDictionary<GlossaryCategory, Lazy<PackLoad<IReadOnlyDictionary<string, GlossaryEntry>>>> _entries =
        GlossaryPacks.All.ToDictionary(
            category => category,
            category => new Lazy<PackLoad<IReadOnlyDictionary<string, GlossaryEntry>>>(
                () => Load<GlossaryEntriesFile, IReadOnlyDictionary<string, GlossaryEntry>>(
                    source, logger, GlossaryPacks.PackOf(category), ByKey, new Dictionary<string, GlossaryEntry>()),
                LazyThreadSafetyMode.ExecutionAndPublication));

    public PackLoad<IReadOnlyList<GlossaryListing>> Listings() => _listings.Value;

    public PackLoad<IReadOnlyDictionary<string, GlossaryEntry>> Entries(GlossaryCategory category)
        => _entries[category].Value;

    /// <summary>Errores de los paquetes de entradas ya intentados; no carga ninguno más.</summary>
    public IReadOnlyList<string> Errors() => _entries.Values
        .Where(lazy => lazy.IsValueCreated)
        .Select(lazy => lazy.Value.Error)
        .OfType<string>()
        .ToList();

    public GlossaryEntry? Get(string key)
    {
        var category = GlossaryPacks.CategoryOfKey(key);
        if (category is null) return null;
        return Entries(category.Value).Value.TryGetValue(key, out var entry) ? entry : null;
    }

    private static IReadOnlyDictionary<string, GlossaryEntry>? ByKey(GlossaryEntriesFile file)
    {
        if (file.Entries is null) return null;
        var map = new Dictionary<string, GlossaryEntry>();
        foreach (var entry in file.Entries) map[entry.Key] = entry;
        return map;
    }

    private static PackLoad<TValue> Load<TFile, TValue>(
        IGlossaryPackSource source, ILogger logger, string packName, Func<TFile, TValue?> select, TValue empty)
        where TFile : class
        where TValue : class
    {
        try
        {
            using var stream = source.Open(packName);
            if (stream is null) return new(empty, $"No se encontró el paquete «{packName}».");
            var file = JsonSerializer.Deserialize<TFile>(stream, GlossaryJson.Options);
            // Un JSON válido pero incompleto (sin entries o listings) deserializa con null: es un paquete ilegible.
            var value = file is null ? null : select(file);
            return value is null ? new(empty, $"El paquete «{packName}» no se pudo leer.") : new(value, null);
        }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            logger.LogWarning("Paquete del glosario ilegible: {Pack} ({ExceptionType})", packName, ex.GetType().Name);
            return new(empty, $"El paquete «{packName}» no se pudo leer.");
        }
    }
}
