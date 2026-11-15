namespace DmCompanion.Core.Tv;

/// <summary>Fichero que la TV puede pedir por su id; se abre solo cuando lo pide.</summary>
public sealed record TvFile(string ContentType, Func<CancellationToken, Task<Stream>> Open);
