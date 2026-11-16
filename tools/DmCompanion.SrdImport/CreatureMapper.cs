using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using DmCompanion.Core.Glossary;

namespace DmCompanion.SrdImport;

internal static partial class CreatureMapper
{
    private static readonly Dictionary<string, string> FieldLabels = new()
    {
        ["ca"] = "CA", ["ini"] = "Iniciativa", ["pg"] = "PG", ["vel"] = "Velocidad", ["hab"] = "Habilidades",
        ["res"] = "Resistencias", ["vul"] = "Vulnerabilidades", ["inm"] = "Inmunidades", ["equipo"] = "Equipo",
        ["sen"] = "Sentidos", ["idi"] = "Idiomas", ["vd"] = "VD",
    };

    public static MappedEntry Map(SourceEntry source)
    {
        var id = source.Id;
        var common = source.Comun;
        var cr = common.Str("vd") ?? throw new FormatException($"{id}: falta comun.vd");
        var typeKey = common.Str("tipo") ?? throw new FormatException($"{id}: falta comun.tipo");
        var typeLine = source.Es.Str("tipo") ?? throw new FormatException($"{id}: falta es.tipo");

        var modifiers = Ints(common, "mod", id);
        var stats = new CreatureStats(
            Ac: Required(common, "ca", id), Hp: Required(common, "pg", id), HitDice: common.Str("dados_golpe") ?? "",
            InitiativeBonus: Required(common, "iniciativa", id), InitiativeScore: Required(common, "iniciativa_puntuacion", id),
            Abilities: Ints(common, "car", id), Modifiers: modifiers,
            Saves: Ints(common, "salv", id, fallback: modifiers),
            Cr: cr, Xp: Required(common, "px", id), XpLair: common.Int("px_guarida"),
            ProficiencyBonus: Required(common, "bc", id), PassivePerception: Required(common, "percepcion_pasiva", id),
            SizeKeys: common.Items("tamano").Select(s => s.GetString() ?? "").ToList(), TypeKey: typeKey, TypeLine: typeLine,
            SpeedFeet: SpeedFeet(source),
            Fields: Fields(source),
            AbilityRows: source.Es.Items("car")
                .Select(r => new AbilityRow(r[0].CellText(), r[1].CellText(), r[2].CellText(), r[3].CellText())).ToList(),
            Sections: source.Es.Items("secciones").Select(s => Section(s, id)).ToList());

        return MappedEntry.Create(GlossaryCategory.Creature, source, null, $"VD {cr} · {typeLine}", [],
            new GlossaryFacets(Cr: cr, CreatureType: typeKey), creature: stats);
    }

    private static int Required(JsonElement element, string name, string id) =>
        element.Int(name) ?? throw new FormatException($"{id}: falta comun.{name}");

    /// <summary>The source leaves a saving throw null in one creature; the modifier is its value then.</summary>
    private static IReadOnlyList<int> Ints(JsonElement element, string name, string id, IReadOnlyList<int>? fallback = null)
    {
        var values = element.Items(name).Select((v, i) =>
            v.ValueKind == JsonValueKind.Number ? v.GetInt32()
            : fallback is not null ? fallback[i]
            : throw new FormatException($"{id}: comun.{name} con un valor no numérico")).ToList();
        if (values.Count != 6) throw new FormatException($"{id}: comun.{name} no tiene 6 valores");
        return values;
    }

    private static IReadOnlyList<GlossaryField> Fields(SourceEntry source)
    {
        var fields = new List<GlossaryField>();
        if (source.Es.Get("campos") is not { ValueKind: JsonValueKind.Object } campos) return fields;
        foreach (var property in campos.EnumerateObject())
        {
            if (!FieldLabels.TryGetValue(property.Name, out var label))
                throw new FormatException($"{source.Id}: campo de criatura desconocido «{property.Name}»");
            fields.Add(new GlossaryField(label, BlockMapper.Spans(property.Value.GetString(), source.Id)));
        }

        return fields;
    }

    private static CreatureSection Section(JsonElement section, string id) => new(
        section.Str("clave") ?? "", section.Str("titulo") ?? "",
        section.Str("intro") is { } intro ? BlockMapper.Spans(intro, id) : null,
        section.Items("items").Select(i => new CreatureFeature(
            i.Str("nombre") ?? "", BlockMapper.MapAll(i.Get("bloques") ?? default, id))).ToList());

    /// <summary>Feet from the feet text; if it is missing, metres / 0.3 rounded to a multiple of 5.</summary>
    private static int? SpeedFeet(SourceEntry source)
    {
        var feet = source.Es.Get("pies")?.Str("campos.vel");
        if (feet is not null && FirstNumber(feet) is { } direct) return (int)direct;

        var metres = source.Es.Get("campos")?.Str("vel");
        if (metres is null || FirstNumber(metres) is not { } m) return null;
        return (int)(Math.Round(m / 0.3m / 5m, MidpointRounding.AwayFromZero) * 5m);
    }

    private static decimal? FirstNumber(string text)
    {
        var match = Number().Match(text);
        return match.Success ? decimal.Parse(match.Value.Replace(',', '.'), CultureInfo.InvariantCulture) : null;
    }

    [GeneratedRegex(@"\d+(,\d+)?")]
    private static partial Regex Number();
}
