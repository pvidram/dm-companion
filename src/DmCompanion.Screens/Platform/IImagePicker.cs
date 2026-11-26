namespace DmCompanion.Screens.Platform;

/// <summary>Imagen elegida por el usuario; <c>OpenAsync</c> abre un flujo nuevo cada vez.</summary>
public sealed record PickedImage(string FileName, Func<Task<Stream>> OpenAsync);

/// <summary>
/// Selector nativo de imágenes. Solo se registra en la App; en el DevHost no existe y el diálogo usa un InputFile del navegador.
/// </summary>
public interface IImagePicker
{
    /// <summary>null si el usuario cancela.</summary>
    Task<PickedImage?> PickAsync();

    /// <summary>Varias imágenes; lista vacía si el usuario cancela. Por defecto, la de <see cref="PickAsync"/>.</summary>
    async Task<IReadOnlyList<PickedImage>> PickManyAsync() => await PickAsync() is { } one ? [one] : [];
}
