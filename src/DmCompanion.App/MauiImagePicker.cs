using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging;

namespace DmCompanion.App;

public sealed class MauiImagePicker(ILogger<MauiImagePicker> logger) : IImagePicker
{
	public async Task<PickedImage?> PickAsync()
	{
		try
		{
			var file = await FilePicker.Default.PickAsync(new PickOptions
			{
				PickerTitle = "Elige la imagen del mapa",
				FileTypes = FilePickerFileType.Images,
			});
			return file is null ? null : new PickedImage(file.FileName, file.OpenReadAsync);
		}
		catch (Exception ex) when (ex is OperationCanceledException or PermissionException or IOException)
		{
			// Cancelar o no tener permiso equivale a no elegir nada; solo el tipo, que el nombre del fichero es del usuario.
			logger.LogWarning("No se pudo elegir la imagen ({ExceptionType}).", ex.GetType().FullName);
			return null;
		}
	}
}
