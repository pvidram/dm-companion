namespace DmCompanion.Core.Tv;

/// <summary>Puerto real del servidor de la TV y las IPv4 locales por las que se le puede llamar (primero la wifi).</summary>
public sealed record TvEndpoint(int Port, IReadOnlyList<string> Addresses);
