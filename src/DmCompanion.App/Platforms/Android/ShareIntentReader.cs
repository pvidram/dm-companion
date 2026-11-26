using Android.Content;
using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.Logging;
using Uri = Android.Net.Uri;

namespace DmCompanion.App;

internal static class ShareIntentReader
{
	public static bool IsShare(Intent? intent) => intent?.Action is Intent.ActionSend or Intent.ActionSendMultiple;

	// Copia cada flujo content:// a una subcarpeta nueva de la caché y entrega la lista a IncomingShare. Nada va al vault.
	public static async Task HandleAsync(Context context, Intent intent, IncomingShare share, IShareInbox inbox, ILogger logger)
	{
		var files = new List<SharedFile>();
		var unreadable = 0;
		try
		{
			var batch = Path.Combine(inbox.TempFolder, Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(batch);
			var number = 0;
			foreach (var uri in UrisOf(intent))
			{
				number++;
				try
				{
					var extension = ExtensionFor(context, intent, uri);
					if (extension is null)
					{
						unreadable++;
						continue;
					}
					var name = $"imagen {number}{extension}";
					var target = Path.Combine(batch, $"{number}{extension}");
					await using var input = context.ContentResolver!.OpenInputStream(uri) ?? throw new IOException("Sin flujo.");
					await using var output = File.Create(target);
					await input.CopyToAsync(output);
					files.Add(new SharedFile(name, target));
				}
				catch (Exception ex)
				{
					logger.LogWarning("No se pudo copiar una imagen compartida ({ExceptionType}).", ex.GetType().FullName);
					unreadable++;
				}
			}
		}
		catch (Exception ex)
		{
			logger.LogError("No se pudo preparar el «Compartir» ({ExceptionType}).", ex.GetType().FullName);
		}
		finally
		{
			// Entrega siempre lo ya copiado, una sola vez, para que no quede huérfano en la caché.
			share.Receive(files, unreadable);
		}
	}

	// Algunos proveedores devuelven null u octet-stream: respaldo en el tipo del intent (si es de imagen concreto) y en la extensión del nombre.
	private static string? ExtensionFor(Context context, Intent intent, Uri uri)
	{
		var extension = IncomingShare.ExtensionForMime(context.ContentResolver?.GetType(uri));
		if (extension is not null) return extension;
		extension = IncomingShare.ExtensionForMime(intent.Type);
		if (extension is not null) return extension;
		return ExtensionForName(DisplayNameOf(context, uri));
	}

	private static string? DisplayNameOf(Context context, Uri uri)
	{
		using var cursor = context.ContentResolver?.Query(uri, new[] { Android.Provider.IOpenableColumns.DisplayName }, null, null, null);
		if (cursor is null || !cursor.MoveToFirst()) return null;
		var index = cursor.GetColumnIndex(Android.Provider.IOpenableColumns.DisplayName);
		return index < 0 ? null : cursor.GetString(index);
	}

	private static string? ExtensionForName(string? name) => Path.GetExtension(name)?.ToLowerInvariant() switch
	{
		".jpg" or ".jpeg" => ".jpg",
		".png" => ".png",
		".webp" => ".webp",
		".gif" => ".gif",
		".heic" or ".heif" => ".heic",
		_ => null,
	};

	// ClipData lo rellenan tanto ACTION_SEND como ACTION_SEND_MULTIPLE; EXTRA_STREAM queda como respaldo de clientes antiguos.
	private static List<Uri> UrisOf(Intent intent)
	{
		var uris = new List<Uri>();
		if (intent.ClipData is { } clip)
		{
			for (var i = 0; i < clip.ItemCount; i++)
				if (clip.GetItemAt(i)?.Uri is { } uri) uris.Add(uri);
		}
		if (uris.Count > 0) return uris;
#pragma warning disable CA1422 // Obsoleto desde la API 33; solo es el respaldo cuando ClipData viene vacío.
		if (intent.Action == Intent.ActionSend)
		{
			if (intent.GetParcelableExtra(Intent.ExtraStream) is Uri single) uris.Add(single);
		}
		else if (intent.GetParcelableArrayListExtra(Intent.ExtraStream) is { } many)
		{
			foreach (var item in many)
				if (item is Uri uri) uris.Add(uri);
		}
#pragma warning restore CA1422
		return uris;
	}
}
