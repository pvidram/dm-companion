using DmCompanion.Screens.Platform;
using DmCompanion.Screens.Services;
using DmCompanion.Screens.Tests.Support;

namespace DmCompanion.Screens.Tests;

public sealed class UpdateNoticeTests
{
    private readonly InMemoryAppPreferences _preferences = new();
    private readonly FakeUpdateCheck _check = new();

    private UpdateNotice Notice() => new(_preferences, _check);

    [Fact]
    public async Task Refresh_sets_pending_when_a_newer_version_is_published()
    {
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r/0.3.0");
        var notice = Notice();

        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        notice.Pending.Should().Be(_check.Latest);
    }

    [Theory]
    [InlineData("0.2.0")]
    [InlineData("0.1.9")]
    public async Task Refresh_leaves_pending_empty_when_the_published_version_is_not_newer(string published)
    {
        _check.Latest = new AvailableUpdate(published, "https://example.test/r");
        var notice = Notice();

        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        notice.Pending.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_ignores_the_skipped_version()
    {
        _preferences.Set(UpdateNotice.SkipKey, "0.3.0");
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r");
        var notice = Notice();

        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        notice.Pending.Should().BeNull();
    }

    [Fact]
    public async Task Refresh_does_not_query_when_disabled()
    {
        _preferences.Set(UpdateNotice.CheckKey, "false");

        await Notice().RefreshAsync("0.2.0", CancellationToken.None);

        _check.Calls.Should().Be(0);
    }

    [Fact]
    public async Task Refresh_ignores_an_unreadable_current_version()
    {
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r");
        var notice = Notice();

        await notice.RefreshAsync("—", CancellationToken.None);

        notice.Pending.Should().BeNull();
    }

    [Fact]
    public async Task Skip_pending_stores_the_version_and_clears_the_notice()
    {
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r");
        var notice = Notice();
        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        notice.SkipPending();

        (notice.Pending, _preferences.Get(UpdateNotice.SkipKey)).Should().Be(((AvailableUpdate?)null, "0.3.0"));
    }

    [Fact]
    public async Task Dismiss_clears_the_notice_without_storing_anything()
    {
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r");
        var notice = Notice();
        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        notice.Dismiss();

        (notice.Pending, _preferences.Get(UpdateNotice.SkipKey)).Should().Be(((AvailableUpdate?)null, (string?)null));
    }

    [Fact]
    public async Task Open_pending_opens_the_release_page()
    {
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r");
        var notice = Notice();
        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        notice.OpenPending();

        _check.Opened.Should().Equal(_check.Latest);
    }

    [Fact]
    public void Enabled_defaults_to_true_and_persists_changes()
    {
        var notice = Notice();
        var before = notice.Enabled;

        notice.Enabled = false;

        (before, _preferences.Get(UpdateNotice.CheckKey)).Should().Be((true, "false"));
    }

    [Fact]
    public void Is_not_available_without_an_update_check() =>
        new UpdateNotice(_preferences, null).IsAvailable.Should().BeFalse();

    [Fact]
    public async Task Refresh_raises_changed_when_a_notice_appears()
    {
        _check.Latest = new AvailableUpdate("0.3.0", "https://example.test/r");
        var notice = Notice();
        var raised = 0;
        notice.Changed += () => raised++;

        await notice.RefreshAsync("0.2.0", CancellationToken.None);

        raised.Should().Be(1);
    }
}
