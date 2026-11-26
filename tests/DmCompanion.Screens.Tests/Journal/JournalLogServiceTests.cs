using DmCompanion.Core.Journal;
using DmCompanion.Screens.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace DmCompanion.Screens.Tests;

public sealed class JournalLogServiceTests : JournalUiTestBase
{
    private const string Ankheg = LyraDiary + "/2026-11-16 Ankheg.md";
    private const string Lobo = LyraDiary + "/2026-11-10 Lobo.md";
    private const string Faro = TorvaldDiary + "/2026-11-12 Faro.md";

    public JournalLogServiceTests()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 17, 7, 0, 0, TimeSpan.Zero));
        clock.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("mas-uno", TimeSpan.FromHours(1), "mas-uno", "mas-uno"));
        Services.AddSingleton<TimeProvider>(clock);
        Services.AddSingleton<JournalLogService>();
    }

    protected override Task SeedAsync()
    {
        WriteFile(Ankheg, EntryText("2026-11-16T21:34:00+01:00", ["2026-11-16 Ankheg-1.jpg"], note: "Salió del suelo."));
        WriteImage(LyraDiary + "/2026-11-16 Ankheg-1.jpg");
        WriteFile(Lobo, EntryText("2026-11-10T20:00:00+01:00", ["2026-11-10 Lobo-1.png"]));
        WriteImage(LyraDiary + "/2026-11-10 Lobo-1.png");
        WriteFile(Faro, EntryText("2026-11-12T19:00:00+01:00", ["falta.png"], note: "Nota del faro."));
        return Task.CompletedTask;
    }

    private JournalLogService Service => Services.GetRequiredService<JournalLogService>();

    private string LogText => File.ReadAllText(Vault.PathOf(JournalPaths.LogPath));

    [Fact]
    public async Task Generate_creates_the_note_in_the_vault_root_and_reports_the_count()
    {
        var result = await Service.GenerateAsync();

        (result, File.Exists(Vault.PathOf("Diario de la campaña.md"))).Should().Be((new JournalLogResult(JournalLogStatus.Generated, 3, true), true));
    }

    [Fact]
    public async Task The_note_is_marked_as_generated_and_says_when()
    {
        await Service.GenerateAsync();

        LogText.Should().StartWith("---\ntype: journal-log\ngenerated: true\n---\nGenerado por DM Companion el 17/11/2026 08:00. Se sobrescribe al regenerar\n");
    }

    [Fact]
    public async Task Sections_follow_the_order_of_the_log_with_owner_title_and_local_date()
    {
        await Service.GenerateAsync();

        var headings = LogText.Split('\n').Where(line => line.StartsWith("## ", StringComparison.Ordinal)).ToArray();

        headings.Should().Equal("## Lyra · Lobo · 10/11/2026", "## Torvald · Faro · 12/11/2026", "## Lyra · Ankheg · 16/11/2026");
    }

    [Fact]
    public async Task Images_are_embedded_with_their_vault_path_and_followed_by_the_note()
    {
        await Service.GenerateAsync();

        LogText.Should().Contain("![[Personajes/Lyra/Diario/2026-11-16 Ankheg-1.jpg]]\n\nSalió del suelo.\n");
    }

    [Fact]
    public async Task A_missing_image_is_left_out_but_the_entry_and_its_note_stay()
    {
        await Service.GenerateAsync();

        (LogText.Contains("falta.png", StringComparison.Ordinal), LogText.Contains("Nota del faro.", StringComparison.Ordinal)).Should().Be((false, true));
    }

    [Fact]
    public async Task Generate_indexes_the_note_right_away()
    {
        await Service.GenerateAsync();

        Host.Current!.Index.Get(JournalPaths.LogPath).Should().NotBeNull();
    }

    [Fact]
    public async Task Generating_again_overwrites_the_generated_note_with_the_new_entries()
    {
        await Service.GenerateAsync();
        WriteFile(LyraDiary + "/2026-11-17 Nueva.md", EntryText("2026-11-17T09:00:00+01:00", ["2026-11-17 Nueva-1.png"]));
        WriteImage(LyraDiary + "/2026-11-17 Nueva-1.png");
        await Host.OpenAsync(Vault.Root);

        var result = await Service.GenerateAsync();

        (result, LogText.Contains("## Lyra · Nueva · 17/11/2026", StringComparison.Ordinal)).Should().Be((new JournalLogResult(JournalLogStatus.Generated, 4, false), true));
    }

    [Fact]
    public async Task A_note_that_the_app_did_not_generate_is_never_touched()
    {
        const string mine = "---\ntype: note\n---\nMi propia bitácora.\n";
        WriteFile(JournalPaths.LogPath, mine);

        var result = await Service.GenerateAsync();

        (result.Status, LogText).Should().Be((JournalLogStatus.ForeignNote, mine));
    }

    [Fact]
    public async Task A_note_without_frontmatter_is_foreign_too()
    {
        WriteFile(JournalPaths.LogPath, "Sin frontmatter.\n");

        var result = await Service.GenerateAsync();

        (result.Status, LogText).Should().Be((JournalLogStatus.ForeignNote, "Sin frontmatter.\n"));
    }

    [Fact]
    public async Task A_note_with_generated_false_is_foreign()
    {
        WriteFile(JournalPaths.LogPath, "---\ntype: journal-log\ngenerated: false\n---\nMía.\n");

        var result = await Service.GenerateAsync();

        result.Status.Should().Be(JournalLogStatus.ForeignNote);
    }

    [Fact]
    public async Task Without_an_open_vault_nothing_is_generated()
    {
        await Host.ForgetAsync();

        var result = await Service.GenerateAsync();

        result.Status.Should().Be(JournalLogStatus.NoVault);
    }

    [Fact]
    public async Task A_vault_without_entries_generates_an_empty_log()
    {
        using var empty = TempVault.Empty();
        await Host.OpenAsync(empty.Root);

        var result = await Service.GenerateAsync();

        (result, File.ReadAllText(Path.Combine(empty.Root, JournalPaths.LogPath))).Should().Be(
            (new JournalLogResult(JournalLogStatus.Generated, 0, true),
             "---\ntype: journal-log\ngenerated: true\n---\nGenerado por DM Companion el 17/11/2026 08:00. Se sobrescribe al regenerar\n"));
    }

    [Theory]
    [InlineData(0, "Bitácora generada: 0 entradas")]
    [InlineData(1, "Bitácora generada: 1 entrada")]
    [InlineData(2, "Bitácora generada: 2 entradas")]
    public void The_message_uses_the_singular_for_one_entry(int entries, string expected) =>
        JournalLogService.GeneratedText(entries).Should().Be(expected);

    [Fact]
    public void The_foreign_note_message_is_the_one_of_the_spec() =>
        JournalLogService.ForeignNoteText.Should().Be("Ya existe una nota «Diario de la campaña» que no generó la app. Renómbrala para generar la bitácora");
}
