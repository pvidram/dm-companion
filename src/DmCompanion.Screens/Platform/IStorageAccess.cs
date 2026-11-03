namespace DmCompanion.Screens.Platform;

public interface IStorageAccess
{
    bool HasAllFilesAccess { get; }
    string DefaultBrowseRoot { get; }
    void RequestAllFilesAccess();
}
