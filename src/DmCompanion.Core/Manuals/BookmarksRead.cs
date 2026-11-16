namespace DmCompanion.Core.Manuals;

/// <summary>File null y Corrupt false = el fichero no existe.</summary>
public sealed record BookmarksRead(ManualBookmarkFile? File, bool Corrupt);
