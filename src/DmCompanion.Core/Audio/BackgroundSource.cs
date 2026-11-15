namespace DmCompanion.Core.Audio;

/// <summary>Origen de un fondo: un track o una sección de la biblioteca (rutas relativas a <c>_audio/</c>).</summary>
public abstract record BackgroundSource;

public sealed record TrackBackground(string Track) : BackgroundSource;

public sealed record SectionBackground(string Section) : BackgroundSource;
