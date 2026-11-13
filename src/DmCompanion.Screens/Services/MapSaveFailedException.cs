namespace DmCompanion.Screens.Services;

/// <summary>El mapa abierto tiene cambios que no se pudieron guardar: no se puede soltar ni cambiar de vault sin perderlos.</summary>
public sealed class MapSaveFailedException(string message) : Exception(message);
