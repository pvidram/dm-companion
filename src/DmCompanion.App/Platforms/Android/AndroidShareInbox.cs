using DmCompanion.Screens.Platform;

namespace DmCompanion.App;

public sealed class AndroidShareInbox : IShareInbox
{
	public string TempFolder { get; } = Path.Combine(FileSystem.CacheDirectory, "share");
}
