using System.Text;
using ExcelETL.BlazorAdmin.Excel;
using FluentAssertions;
using Xunit;

namespace ExcelETL.BlazorAdmin.Tests.Excel;

public class BrowserFileStreamBufferingTests
{
    [Fact]
    public async Task BufferToSeekableStreamAsync_WithAStreamThatOnlySupportsAsyncReads_BuffersContentWithoutThrowing()
    {
        var expectedBytes = Encoding.UTF8.GetBytes("dummy xlsx content");
        await using var source = new AsyncOnlyReadStream(expectedBytes);

        await using var buffered = await BrowserFileStreamBuffering.BufferToSeekableStreamAsync(source);

        buffered.Position.Should().Be(0);
        buffered.ToArray().Should().BeEquivalentTo(expectedBytes);
    }

    // Mirrors the real stream IBrowserFile.OpenReadStream() returns in an actual browser: data
    // streams live over the Interactive Server circuit, so only the async Read overloads are
    // supported -- calling either synchronous overload throws exactly like the reported
    // production bug ("Synchronous reads are not supported.").
    private sealed class AsyncOnlyReadStream(byte[] content) : Stream
    {
        private readonly MemoryStream _inner = new(content);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException("Synchronous reads are not supported.");

        public override long Position
        {
            get => throw new NotSupportedException("Synchronous reads are not supported.");
            set => throw new NotSupportedException("Synchronous reads are not supported.");
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException("Synchronous reads are not supported.");

        public override int ReadByte() =>
            throw new NotSupportedException("Synchronous reads are not supported.");

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _inner.ReadAsync(buffer, offset, count, cancellationToken);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _inner.ReadAsync(buffer, cancellationToken);

        public override void Flush() => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
