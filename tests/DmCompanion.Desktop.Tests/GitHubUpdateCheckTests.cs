using System.Net;
using DmCompanion.Desktop.Updates;
using DmCompanion.Screens.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace DmCompanion.Desktop.Tests;

public sealed class GitHubUpdateCheckTests
{
    private readonly FakeBrowserLauncher _launcher = new();

    private GitHubUpdateCheck Check(StubHttpHandler handler) =>
        new(new HttpClient(handler), _launcher, NullLogger<GitHubUpdateCheck>.Instance);

    [Fact]
    public async Task Get_latest_reads_the_tag_and_the_page()
    {
        var handler = StubHttpHandler.Json("""{"tag_name":"v0.3.0","html_url":"https://github.com/o/r/releases/tag/v0.3.0"}""");

        var latest = await Check(handler).GetLatestAsync(CancellationToken.None);

        latest.Should().Be(new AvailableUpdate("0.3.0", "https://github.com/o/r/releases/tag/v0.3.0"));
    }

    [Fact]
    public async Task Get_latest_sends_only_the_expected_headers()
    {
        var handler = StubHttpHandler.Json("""{"tag_name":"v0.3.0","html_url":"https://github.com/o/r"}""");

        await Check(handler).GetLatestAsync(CancellationToken.None);

        var request = handler.Requests.Single();
        (request.RequestUri!.ToString(), request.Headers.UserAgent.ToString(), request.Headers.Accept.ToString())
            .Should().Be((GitHubUpdateCheck.LatestReleaseUrl, $"DMCompanion/{AppVersion.Current}", "application/vnd.github+json"));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Get_latest_returns_null_for_non_success_status(HttpStatusCode status) =>
        (await Check(StubHttpHandler.Json("{}", status)).GetLatestAsync(CancellationToken.None)).Should().BeNull();

    [Theory]
    [InlineData("{ no es json")]
    [InlineData("""{"tag_name":"v0.3.0"}""")]
    [InlineData("""{"tag_name":"latest","html_url":"https://github.com/o/r"}""")]
    [InlineData("""{"tag_name":"v0.3.0","html_url":"javascript:alert(1)"}""")]
    public async Task Get_latest_returns_null_for_unusable_bodies(string body) =>
        (await Check(StubHttpHandler.Json(body)).GetLatestAsync(CancellationToken.None)).Should().BeNull();

    [Fact]
    public async Task Get_latest_returns_null_without_network() =>
        (await Check(StubHttpHandler.Throws(new HttpRequestException("sin red"))).GetLatestAsync(CancellationToken.None))
            .Should().BeNull();

    [Fact]
    public async Task Get_latest_returns_null_on_timeout()
    {
        var handler = new StubHttpHandler(async (_, ct) =>
        {
            await Task.Delay(Timeout.Infinite, ct);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var check = new GitHubUpdateCheck(new HttpClient(handler) { Timeout = TimeSpan.FromMilliseconds(50) }, _launcher,
            NullLogger<GitHubUpdateCheck>.Instance);

        (await check.GetLatestAsync(CancellationToken.None)).Should().BeNull();
    }

    [Fact]
    public void Open_release_page_uses_the_browser_launcher()
    {
        Check(StubHttpHandler.Json("{}")).OpenReleasePage(new AvailableUpdate("0.3.0", "https://github.com/o/r"));

        _launcher.Opened.Should().Equal("https://github.com/o/r");
    }

    [Fact]
    public async Task Get_latest_logs_a_line_when_the_body_has_no_usable_version()
    {
        var logger = new ListLogger<GitHubUpdateCheck>();
        var check = new GitHubUpdateCheck(new HttpClient(StubHttpHandler.Json("""{"tag_name":"latest","html_url":"https://github.com/o/r"}""")), _launcher, logger);

        await check.GetLatestAsync(CancellationToken.None);

        logger.Entries.Should().ContainSingle();
    }
}
