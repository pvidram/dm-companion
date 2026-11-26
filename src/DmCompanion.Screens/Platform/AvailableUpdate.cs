namespace DmCompanion.Screens.Platform;

/// <summary>Una release publicada: versión sin «v» y URL de su página.</summary>
public sealed record AvailableUpdate(string Version, string Url);
