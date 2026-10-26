using BookMyDoctor.Features.Chats.Tools;
using Xunit;

namespace BookMyDoctor.Tests;

public sealed class ResolveDateTests
{
    // Sunday 25 October 2026
    private static readonly DateOnly Sunday = new(2026, 10, 25);

    [Theory]
    [InlineData("2026-10-30", "2026-10-30")]
    [InlineData("today", "2026-10-25")]
    [InlineData("Tomorrow", "2026-10-26")]
    [InlineData("tuesday", "2026-10-27")]
    [InlineData("Tue", "2026-10-27")]
    [InlineData("sunday", "2026-10-25")]        // a weekday that is today means today
    [InlineData("next sunday", "2026-11-01")]   // "next" means after today
    [InlineData("next tuesday", "2026-10-27")]
    public void UnderstandsWordsAndIsoDates(string text, string expected)
    {
        Assert.Equal(DateOnly.Parse(expected), ToolArguments.ResolveDate(text, Sunday));
    }

    [Theory]
    [InlineData("27/10/2026")]
    [InlineData("someday")]
    [InlineData("tu")]
    public void RejectsWhatItCannotUnderstand(string text)
    {
        Assert.Throws<ArgumentException>(() => ToolArguments.ResolveDate(text, Sunday));
    }
}
