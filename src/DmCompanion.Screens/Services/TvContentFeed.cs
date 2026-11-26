using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using DmCompanion.Core.GameSystems;
using DmCompanion.Core.Journal;
using DmCompanion.Core.Glossary;
using DmCompanion.Core.Rendering;
using DmCompanion.Core.Tv;
using DmCompanion.Core.Vault;

namespace DmCompanion.Screens.Services;

/// <summary>Mensaje `content` para la TV, la lista blanca de las imágenes que cita y el título mostrado.</summary>
public sealed record TvContentMessage(string Json, IReadOnlyDictionary<string, TvFile> Files, string Title);

/// <summary>
/// Traduce una ficha de PJ o una nota de lugar al mensaje `content` de la TV: el cuerpo pasa por
/// <see cref="NoteHtmlRenderer.RenderForPlayers"/> (sin bloques secretos ni sus imágenes) y los enlaces quedan como texto.
/// Las imágenes se citan como `files/{id}` con los mismos ids que el mapa (<see cref="TvMapFeed"/>). Una ficha de PJ lleva además
/// `sheet`: el retrato y las estadísticas que muestra la ficha de la tablet (<see cref="SheetStats"/>), como texto plano.
/// </summary>
public sealed partial class TvContentFeed(VaultHost host, NoteImageResolver images, TvMapFeed files)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Fichas de PJ, notas de lugar y notas de regla que no sean secretas. Las de monstruo no: llevan datos del DM.</summary>
    public static bool CanShow(VaultEntity entity) => entity.Type is EntityType.Pc or EntityType.Location or EntityType.Rule && !entity.IsSecret;

    /// <summary>Toda entrada del SRD menos las criaturas, que son material del DM.</summary>
    public static bool CanShow(GlossaryEntry entry) => entry.Category != GlossaryCategory.Creature;

    /// <summary>Carga la lista de imágenes del vault; hay que esperarla antes de <see cref="Build"/>.</summary>
    public Task PrepareAsync() => images.EnsureLoadedAsync();

    /// <summary>Cambió una imagen del vault (la lista ya está marcada para recargar): el contenido puede citarla.</summary>
    public event EventHandler? ImagesChanged
    {
        add => images.ImagesChanged += value;
        remove => images.ImagesChanged -= value;
    }

    /// <summary>La imagen sigue en el vault (lista ya cargada con <see cref="PrepareAsync"/>).</summary>
    public bool ImageExists(string path) => images.LastWriteOf(path) is not null;

    /// <summary>Mensaje de esa nota en el vault abierto; null si no existe o no se puede mostrar.</summary>
    public TvContentMessage? Build(string notePath)
    {
        if (host.Current?.Index.Get(notePath) is not { } entity || !CanShow(entity)) return null;
        var whitelist = new Dictionary<string, TvFile>(StringComparer.Ordinal);
        // Los enlaces no llevan a ningún sitio en la TV: da igual si existen.
        var html = NoteHtmlRenderer.RenderForPlayers(entity, _ => true, target => files.UrlFor(images.ResolvePath(target), whitelist));
        html = AnchorClose().Replace(AnchorOpen().Replace(html, "<span>"), "</span>");
        // `sheet` solo existe en las fichas de PJ: en el resto de notas el campo no aparece.
        var json = entity.Type == EntityType.Pc
            ? JsonSerializer.Serialize(new { kind = "content", title = entity.Title, html, sheet = SheetOf(entity, whitelist) }, Json)
            : JsonSerializer.Serialize(new { kind = "content", title = entity.Title, html }, Json);
        return new TvContentMessage(json, whitelist, entity.Title);
    }

    /// <summary>Mensaje de una entrada del SRD: su cabecera (conjuro u objeto) y sus bloques, con todo el texto codificado.</summary>
    public TvContentMessage BuildGlossary(GlossaryEntry entry)
    {
        var header = entry.Spell?.Header ?? entry.Item?.Header;
        var html = (header is null ? "" : $"<p><em>{WebUtility.HtmlEncode(header)}</em></p>") + SpellFields(entry.Spell)
            + GlossaryHtmlRenderer.Render(entry.Blocks);
        var json = JsonSerializer.Serialize(new { kind = "content", title = entry.Name, html }, Json);
        return new TvContentMessage(json, new Dictionary<string, TvFile>(), entry.Name);
    }

    /// <summary>Mensaje de una página (o un recorte) de un manual: una sola imagen PNG, que vive solo en la lista blanca de este mensaje.</summary>
    public TvContentMessage BuildManualImage(string title, byte[] png)
    {
        var id = "manual-" + Guid.NewGuid().ToString("N");
        var files = new Dictionary<string, TvFile>(StringComparer.Ordinal)
        {
            [id] = new TvFile("image/png", _ => Task.FromResult<Stream>(new MemoryStream(png, writable: false))),
        };
        var html = $"<img src=\"files/{id}\" alt=\"\" class=\"tv-manual\">";
        var json = JsonSerializer.Serialize(new { kind = "content", title, html }, Json);
        return new TvContentMessage(json, files, title);
    }

    /// <summary>
    /// Mensaje `slide` de una diapositiva del diario: `{ kind, caption, image, text }`. La imagen se cita como `files/{id}` (mismos ids
    /// que el mapa y las notas) y vive solo en la lista blanca de este mensaje; el texto de la nota va como texto plano.
    /// </summary>
    public TvContentMessage BuildSlide(JournalSlide slide)
    {
        var whitelist = new Dictionary<string, TvFile>(StringComparer.Ordinal);
        var image = slide.ImagePath is null ? null : files.UrlFor(slide.ImagePath, whitelist);
        var json = JsonSerializer.Serialize(new { kind = "slide", caption = slide.Caption, image, text = slide.Text }, Json);
        return new TvContentMessage(json, whitelist, slide.Caption);
    }

    private static string SpellFields(SpellInfo? spell) => spell is null
        ? ""
        : "<dl>"
            + $"<dt>Tiempo de lanzamiento</dt><dd>{WebUtility.HtmlEncode(spell.CastingTime)}</dd>"
            + $"<dt>Alcance</dt><dd>{WebUtility.HtmlEncode(spell.Range)}</dd>"
            + $"<dt>Componentes</dt><dd>{WebUtility.HtmlEncode(spell.Components)}</dd>"
            + $"<dt>Duración</dt><dd>{WebUtility.HtmlEncode(spell.Duration)}</dd>"
            + "</dl>";

    private object SheetOf(VaultEntity entity, Dictionary<string, TvFile> whitelist)
    {
        var system = host.Current?.ActiveSystem ?? GameSystemRegistry.Default.Get("dnd5e");
        var stats = SheetStats.For(system, system.Evaluate(entity.Frontmatter)).Select(s => new { label = s.Label, value = s.Value }).ToList();
        // Mismo filtro que los retratos de los tokens: UrlFor solo admite imágenes con ruta segura dentro del vault.
        var portraitUrl = string.IsNullOrWhiteSpace(entity.Portrait) ? null : files.UrlFor(VaultPaths.Normalize(entity.Portrait), whitelist);
        return new { portraitUrl, stats };
    }

    // Markdig con HTML desactivado escapa el texto y los atributos: un `<a` o un `>` literal solo puede ser una etiqueta.
    [GeneratedRegex("<a\\b[^>]*>")]
    private static partial Regex AnchorOpen();

    [GeneratedRegex("</a>")]
    private static partial Regex AnchorClose();
}
