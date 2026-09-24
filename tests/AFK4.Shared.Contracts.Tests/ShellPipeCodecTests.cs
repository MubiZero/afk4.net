using System.Buffers.Binary;
using AFK4.Shared.Contracts.Shell;

namespace AFK4.Shared.Contracts.Tests;

public sealed class ShellPipeCodecTests
{
    [Fact]
    public async Task RoundTrip_KeepsEveryPartOfTheEnvelope()
    {
        using var stream = new MemoryStream();
        var request = new ShellPipeMessage(
            ShellPipeMessageTypeNames.Request,
            Request: new ShellPipeRequestDto(
                Guid.Parse("11111111-2222-4333-8444-555555555555"),
                ShellPipeRequestTypeNames.Launch,
                new Dictionary<string, string> { ["appId"] = "cs2" }));
        var hello = new ShellPipeMessage(
            ShellPipeMessageTypeNames.Hello,
            Hello: new ShellPipeHelloDto(ShellPipeProtocol.Version, "2.0.0", SessionId: 1));

        await ShellPipeCodec.WriteAsync(stream, hello, CancellationToken.None);
        await ShellPipeCodec.WriteAsync(stream, request, CancellationToken.None);
        stream.Position = 0;

        var first = await ShellPipeCodec.ReadAsync(stream, CancellationToken.None);
        var second = await ShellPipeCodec.ReadAsync(stream, CancellationToken.None);
        var end = await ShellPipeCodec.ReadAsync(stream, CancellationToken.None);

        Assert.NotNull(first?.Hello);
        Assert.Equal(ShellPipeProtocol.Version, first!.Hello!.Protocol);
        Assert.Equal(ShellPipeRequestTypeNames.Launch, second?.Request?.Type);
        Assert.Equal("cs2", second!.Request!.Payload["appId"]);
        Assert.Equal(request.Request!.RequestId, second.Request.RequestId);
        Assert.Null(end);
    }

    [Fact]
    public async Task Read_AssemblesAFrameThatArrivesOneByteAtATime()
    {
        // Канал не обязан отдать кадр одним чтением: длинное состояние приходит кусками.
        using var buffer = new MemoryStream();
        await ShellPipeCodec.WriteAsync(
            buffer,
            new ShellPipeMessage(ShellPipeMessageTypeNames.Bye, Reason: "protocol"),
            CancellationToken.None);

        using var trickle = new TrickleStream(buffer.ToArray());
        var message = await ShellPipeCodec.ReadAsync(trickle, CancellationToken.None);

        Assert.Equal(ShellPipeMessageTypeNames.Bye, message?.Type);
        Assert.Equal("protocol", message!.Reason);
    }

    [Fact]
    public async Task Read_RefusesAFrameAboveTheLimit()
    {
        // Длина из чужого или сломанного потока не должна заставить агента выделить гигабайт.
        var header = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(header, ShellPipeProtocol.MaxFrameBytes + 1);
        using var stream = new MemoryStream(header);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => ShellPipeCodec.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Read_TreatsAFrameCutInTheMiddleAsABrokenPipe()
    {
        using var buffer = new MemoryStream();
        await ShellPipeCodec.WriteAsync(
            buffer,
            new ShellPipeMessage(ShellPipeMessageTypeNames.Bye, Reason: "protocol"),
            CancellationToken.None);
        var cut = buffer.ToArray()[..^3];

        using var stream = new MemoryStream(cut);

        await Assert.ThrowsAsync<EndOfStreamException>(
            () => ShellPipeCodec.ReadAsync(stream, CancellationToken.None));
    }

    [Fact]
    public async Task Write_RefusesAMessageAboveTheLimit()
    {
        var huge = new string('x', ShellPipeProtocol.MaxFrameBytes);
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<InvalidDataException>(() => ShellPipeCodec.WriteAsync(
            stream,
            new ShellPipeMessage(ShellPipeMessageTypeNames.Bye, Reason: huge),
            CancellationToken.None));
        Assert.Equal(0, stream.Length);
    }

    private sealed class TrickleStream(byte[] data) : Stream
    {
        private int position;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => data.Length;
        public override long Position { get => position; set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position >= data.Length || count == 0)
            {
                return 0;
            }

            buffer[offset] = data[position++];
            return 1;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
