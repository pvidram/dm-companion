using DmCompanion.UI.Components;
using DmCompanion.UI.Tests.Support;
using Microsoft.AspNetCore.Components;

namespace DmCompanion.UI.Tests;

public class ContentTests : UiTestContext
{
    [Fact]
    public void Note_content_renders_the_given_html()
    {
        var cut = Render<NoteContent>(p => p.Add(x => x.Html, new MarkupString("<h2>Taberna</h2>")));
        cut.Find(".ds-note h2").TextContent.Should().Be("Taberna");
    }

    [Fact]
    public void Secret_note_shows_the_dm_only_label()
    {
        var cut = Render<NoteContent>(p => p.Add(x => x.Html, new MarkupString("<p>x</p>")).Add(x => x.IsSecret, true));
        cut.Find(".ds-note__dm-label.dm-only").TextContent.Should().Be("Solo DM");
    }

    [Fact]
    public void Public_note_has_no_dm_only_label()
    {
        var cut = Render<NoteContent>(p => p.Add(x => x.Html, new MarkupString("<p>x</p>")));
        cut.FindAll(".dm-only").Should().BeEmpty();
    }

    [Theory]
    [InlineData(NoticeKind.Error, "alert")]
    [InlineData(NoticeKind.Warning, "status")]
    [InlineData(NoticeKind.Info, "status")]
    public void Notice_role_depends_on_its_kind(NoticeKind kind, string expectedRole)
    {
        var cut = Render<InlineNotice>(p => p.Add(x => x.Kind, kind).AddChildContent("Enlace roto"));
        cut.Find(".ds-notice").GetAttribute("role").Should().Be(expectedRole);
    }

    [Fact]
    public void Empty_state_shows_its_title()
    {
        var cut = Render<EmptyState>(p => p.Add(x => x.Title, "Sin notas"));
        cut.Find(".ds-empty__title").TextContent.Should().Be("Sin notas");
    }
}
