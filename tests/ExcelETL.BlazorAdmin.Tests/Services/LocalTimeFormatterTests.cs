using ExcelETL.BlazorAdmin.Services;
using FluentAssertions;
using Microsoft.JSInterop;
using Moq;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Services;

// Lot 064 (64.2): proves the JS interop call receives a genuine ISO 8601 UTC representation of the
// source timestamp (i.e. one the browser's `new Date(...)` will interpret as UTC, not as its own
// local time) -- not a pre-formatted string that would defeat client-side conversion entirely.
public class LocalTimeFormatterTests
{
    [Fact]
    public async Task FormatAsync_InvokesTheExpectedJsFunction_WithAGenuineIso8601UtcRepresentation()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        object[]? capturedArgs = null;
        jsRuntimeMock
            .Setup(js => js.InvokeAsync<string>("amOxoLocalTime.format", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedArgs = args)
            .Returns(new ValueTask<string>("stubbed"));

        var formatter = new LocalTimeFormatter(jsRuntimeMock.Object);

        // Kind deliberately left Unspecified, as EF Core / a plain DateTime column would produce --
        // the formatter itself must force it to Utc before serializing, not trust the caller.
        var utcValue = new DateTime(2026, 9, 1, 20, 43, 27, DateTimeKind.Unspecified);

        var result = await formatter.FormatAsync(utcValue, "yyyy-MM-dd HH:mm:ss");

        result.Should().Be("stubbed");
        capturedArgs.Should().NotBeNull();
        capturedArgs![0].Should().Be("2026-09-01T20:43:27.0000000Z");
        capturedArgs[1].Should().Be("yyyy-MM-dd HH:mm:ss");

        // The whole point: a date-time string with no timezone designator is parsed by the browser's
        // own `new Date(...)` as LOCAL time, not UTC -- silently defeating the entire conversion.
        ((string)capturedArgs[0]).Should().EndWith("Z");
    }

    [Fact]
    public async Task FormatManyAsync_BatchesEveryValueIntoASingleJsInteropCall()
    {
        var jsRuntimeMock = new Mock<IJSRuntime>();
        object[]? capturedArgs = null;
        jsRuntimeMock
            .Setup(js => js.InvokeAsync<string[]>("amOxoLocalTime.formatMany", It.IsAny<object[]>()))
            .Callback<string, object[]>((_, args) => capturedArgs = args)
            .Returns(new ValueTask<string[]>(["first", "second"]));

        var formatter = new LocalTimeFormatter(jsRuntimeMock.Object);

        var values = new[]
        {
            new DateTime(2026, 9, 1, 8, 0, 0, DateTimeKind.Utc),
            new DateTime(2026, 9, 1, 9, 0, 0, DateTimeKind.Unspecified),
        };

        var result = await formatter.FormatManyAsync(values, "dd/MM/yyyy HH:mm:ss");

        result.Should().Equal("first", "second");
        capturedArgs.Should().NotBeNull();
        var isoValues = (string[])capturedArgs![0];
        isoValues.Should().HaveCount(2);
        isoValues.Should().OnlyContain(v => v.EndsWith("Z", StringComparison.Ordinal));
        capturedArgs[1].Should().Be("dd/MM/yyyy HH:mm:ss");

        jsRuntimeMock.Verify(
            js => js.InvokeAsync<string[]>("amOxoLocalTime.formatMany", It.IsAny<object[]>()), Times.Once);
    }
}
