namespace DmCompanion.Infrastructure.Tests;

public sealed class UserFoldersTests
{
    [Fact]
    public void From_keeps_existing_distinct_folders_in_order()
    {
        var existing = new HashSet<string> { "/home/u", "/home/u/Documents", "/home/u/Downloads" };
        UserFolders.From(["/home/u", "", null, "/home/u/Desktop", "/home/u/Documents", "/home/u", "/home/u/Downloads"], existing.Contains)
            .Should().Equal("/home/u", "/home/u/Documents", "/home/u/Downloads");
    }
}
