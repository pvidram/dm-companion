using DmCompanion.Core.Vault;

namespace DmCompanion.Core.Tests;

public class MarkdownCodeMaskerTests
{
    [Fact]
    public void Mask_preserves_length()
    {
        const string text = "a `b` c\n```\n[[x]]\n```\nd";
        MarkdownCodeMasker.Mask(text).Length.Should().Be(text.Length);
    }

    [Fact]
    public void Mask_blanks_fenced_code_blocks()
    {
        var masked = MarkdownCodeMasker.Mask("antes\n```js\n[[Oculto]]\n```\ndespués");
        masked.Should().NotContain("[[Oculto]]").And.Contain("antes").And.Contain("después");
    }

    [Fact]
    public void Mask_blanks_tilde_fences()
    {
        MarkdownCodeMasker.Mask("~~~\n%%x%%\n~~~\n").Should().NotContain("%%");
    }

    [Fact]
    public void Mask_blanks_inline_code()
    {
        MarkdownCodeMasker.Mask("ver `[[Nota]]` y [[Otra]]").Should().NotContain("[[Nota]]").And.Contain("[[Otra]]");
    }

    [Fact]
    public void Mask_blanks_until_end_when_fence_is_not_closed()
    {
        MarkdownCodeMasker.Mask("texto\n```\n[[Nota]]").Should().NotContain("[[Nota]]");
    }

    [Fact]
    public void Mask_does_not_treat_backtick_line_with_closing_backticks_as_fence() =>
        MarkdownCodeMasker.Mask("```foo```\n%%x%%").Should().Contain("%%x%%");

    [Fact]
    public void Mask_keeps_line_breaks()
    {
        MarkdownCodeMasker.Mask("```\nuno\ndos\n```").Count(c => c == '\n').Should().Be(3);
    }
}
