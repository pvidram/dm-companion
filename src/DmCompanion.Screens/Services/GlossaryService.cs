using DmCompanion.Core.Glossary;
using DmCompanion.Core.Vault;
using Microsoft.Extensions.Logging;

namespace DmCompanion.Screens.Services;

/// <summary>
/// Índice del glosario del vault abierto: SRD filtrado por el sistema activo más las notas del vault.
/// Se reconstruye de forma perezosa, solo cuando cambia el <see cref="VaultIndex"/> o el sistema.
/// </summary>
public sealed class GlossaryService : IDisposable
{
    public const string SrdUrl = "https://www.dndbeyond.com/srd";

    private const string Attribution =
        "Esta obra incluye material procedente del documento de referencia del sistema 5.2.1 (“SRD 5.2.1”) de Wizards of the Coast LLC, "
        + $"disponible en {SrdUrl}. La licencia sobre el SRD 5.2.1 se concede de acuerdo con la licencia internacional de "
        + "atribución/reconocimiento 4.0 de Creative Commons, disponible en https://creativecommons.org/licenses/by/4.0/legalcode.";

    private readonly VaultHost _host;
    private readonly GlossaryPackLoader _packs;
    private readonly ILogger<GlossaryService> _logger;
    private readonly object _gate = new();

    private GlossaryIndex _index = GlossaryIndex.Empty;
    private VaultIndex? _builtFor;
    private string? _builtSystem;
    private bool _built;

    public GlossaryService(VaultHost host, GlossaryPackLoader packs, ILogger<GlossaryService> logger)
    {
        _host = host;
        _packs = packs;
        _logger = logger;
        _host.Changed += OnHostChanged;
    }

    public event EventHandler? Changed;

    public string? AttributionText => Attribution;

    public GlossaryIndex Index
    {
        get
        {
            lock (_gate)
            {
                var vault = _host.Current;
                var vaultIndex = vault?.Index ?? VaultIndex.Empty;
                var systemId = vault?.ActiveSystem.Id;
                if (_built && ReferenceEquals(vaultIndex, _builtFor) && systemId == _builtSystem) return _index;

                var srd = vault is null ? [] : SrdRows(vault.ActiveSystem.GlossaryPacks);
                _index = GlossaryIndex.Build(srd, vaultIndex);
                _builtFor = vaultIndex;
                _builtSystem = systemId;
                _built = true;
                return _index;
            }
        }
    }

    /// <summary>
    /// Errores de paquetes (sin duplicados), listos para Diagnóstico: el índice, que siempre se carga, y los paquetes
    /// de entradas ya intentados. No carga ninguno más.
    /// </summary>
    public IReadOnlyList<string> Warnings =>
        _packs.Errors().Prepend(_packs.Listings().Error).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

    public GlossaryEntry? GetSrd(string key)
    {
        var vault = _host.Current;
        if (vault is null) return null;
        return GlossaryPacks.CategoryOfKey(key) is { } category && vault.ActiveSystem.GlossaryPacks.Contains(category)
            ? _packs.Get(key)
            : null;
    }

    public void Dispose() => _host.Changed -= OnHostChanged;

    private IReadOnlyList<GlossaryListing> SrdRows(IReadOnlyList<GlossaryCategory> categories) =>
        categories.Count == 0 ? [] : _packs.Listings().Value.Where(row => categories.Contains(row.Category)).ToList();

    private void OnHostChanged(object? sender, EventArgs e)
    {
        var handlers = Changed?.GetInvocationList();
        if (handlers is null) return;
        foreach (var handler in handlers)
        {
            try
            {
                ((EventHandler)handler)(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                _logger.LogError("Un suscriptor de GlossaryService.Changed ha lanzado una excepción ({ExceptionType})", ex.GetType().FullName);
            }
        }
    }
}
