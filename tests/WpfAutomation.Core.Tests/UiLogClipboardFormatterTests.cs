using FluentAssertions;
using WpfAutomation.App.Models;

namespace WpfAutomation.Core.Tests;

public sealed class UiLogClipboardFormatterTests
{
    [Fact]
    public void Format_IncludesTimestampLevelAndMessage()
    {
        var item = new UiLogItem
        {
            TimestampUtc = new DateTime(2026, 4, 9, 12, 34, 56, DateTimeKind.Utc),
            Level = "INFO",
            Message = "Flow action complete.",
        };

        var text = UiLogClipboardFormatter.Format(item);

        text.Should().Contain("[2026-04-09T12:34:56.0000000Z] [INFO] Flow action complete.");
    }

    [Fact]
    public void FormatAll_JoinsEntriesAndIncludesScreenshotWhenPresent()
    {
        var items = new[]
        {
            new UiLogItem
            {
                TimestampUtc = new DateTime(2026, 4, 9, 12, 0, 0, DateTimeKind.Utc),
                Level = "INFO",
                Message = "Run started.",
            },
            new UiLogItem
            {
                TimestampUtc = new DateTime(2026, 4, 9, 12, 1, 0, DateTimeKind.Utc),
                Level = "ERROR",
                Message = "Flow action failed.",
                ScreenshotPath = @"C:\temp\shot.png",
            },
        };

        var text = UiLogClipboardFormatter.FormatAll(items);

        text.Should().Contain("Run started.");
        text.Should().Contain("Flow action failed.");
        text.Should().Contain(@"Screenshot: C:\temp\shot.png");
        text.Should().Contain($"{Environment.NewLine}{Environment.NewLine}");
    }
}
