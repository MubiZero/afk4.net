using System.Net;
using AFK4.Agent.Service;
using AFK4.Agent.Service.Updates;
using AFK4.Shared.Contracts.Updates;
using Microsoft.Extensions.Options;

namespace AFK4.Agent.Service.Tests;

public sealed class HttpUpdateArtifactDownloaderTests : IDisposable
{
    private readonly string tempRoot = Path.Combine(Path.GetTempPath(), $"afk4-update-download-{Guid.NewGuid():N}");

    [Fact]
    public async Task DownloadAsync_WritesCompleteArtifactToFinalPath()
    {
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        using var handler = new StaticContentHandler(bytes);
        var downloader = CreateDownloader(handler);
        var instruction = CreateInstruction(sizeBytes: bytes.Length);

        var artifact = await downloader.DownloadAsync(instruction, CancellationToken.None);

        Assert.True(File.Exists(artifact.FilePath));
        Assert.Equal(bytes.Length, artifact.SizeBytes);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(artifact.FilePath));
        Assert.Empty(Directory.EnumerateFiles(tempRoot, "*.tmp"));
    }

    // Соединение, которое замолчало посреди пакета, раньше вешало весь цикл обновлений до
    // перезапуска службы: предела у загрузки не было. Теперь тишина кончается временным сбоем —
    // координатор повторит на следующей проверке — и недокачанный файл не остаётся.
    [Fact]
    public async Task DownloadAsync_WhenTheBodyGoesSilent_GivesUpAsTransientAndLeavesNothing()
    {
        using var handler = new SilentAfterHandler(new byte[] { 1, 2 });
        var downloader = CreateDownloader(handler, stallTimeout: TimeSpan.FromMilliseconds(200));
        var instruction = CreateInstruction(sizeBytes: 5);

        await Assert.ThrowsAsync<TimeoutException>(() => downloader.DownloadAsync(instruction, CancellationToken.None));

        Assert.Empty(Directory.EnumerateFiles(tempRoot, "*.tmp"));
        Assert.DoesNotContain(
            Directory.EnumerateFiles(tempRoot),
            path => path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task DownloadAsync_WhenNetworkFailsDuringBody_DoesNotLeaveFinalOrTempArtifact()
    {
        using var handler = new FailingContentHandler(new byte[] { 1, 2, 3, 4, 5 }, failAfterBytes: 2);
        var downloader = CreateDownloader(handler);
        var instruction = CreateInstruction(sizeBytes: 5);

        await Assert.ThrowsAsync<IOException>(() => downloader.DownloadAsync(instruction, CancellationToken.None));

        Assert.DoesNotContain(
            Directory.EnumerateFiles(tempRoot),
            path => path.EndsWith(".msi", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(Directory.EnumerateFiles(tempRoot, "*.tmp"));
    }

    [Fact]
    public async Task DownloadAsync_WhenStaleZeroByteArtifactExists_RemovesItBeforeFailedRetry()
    {
        Directory.CreateDirectory(tempRoot);
        var stalePath = Path.Combine(tempRoot, "agent-service-1.2.3-aaaaaaaaaaaa4aaaaaaaaaaaaaaaaaaa.msi");
        await File.WriteAllBytesAsync(stalePath, []);
        using var handler = new FailingContentHandler(new byte[] { 1, 2, 3, 4, 5 }, failAfterBytes: 2);
        var downloader = CreateDownloader(handler);
        var instruction = CreateInstruction(sizeBytes: 5);

        await Assert.ThrowsAsync<IOException>(() => downloader.DownloadAsync(instruction, CancellationToken.None));

        Assert.False(File.Exists(stalePath));
        Assert.Empty(Directory.EnumerateFiles(tempRoot, "*.tmp"));
    }

    [Fact]
    public async Task DownloadAsync_WhenCompleteArtifactAlreadyExists_ReusesItWithoutNetworkCall()
    {
        Directory.CreateDirectory(tempRoot);
        var bytes = new byte[] { 1, 2, 3, 4, 5 };
        var finalPath = Path.Combine(tempRoot, "agent-service-1.2.3-aaaaaaaaaaaa4aaaaaaaaaaaaaaaaaaa.msi");
        await File.WriteAllBytesAsync(finalPath, bytes);
        using var handler = new ThrowingHandler();
        var downloader = CreateDownloader(handler);
        var instruction = CreateInstruction(sizeBytes: bytes.Length);

        var artifact = await downloader.DownloadAsync(instruction, CancellationToken.None);

        Assert.Equal(finalPath, artifact.FilePath);
        Assert.Equal(bytes.Length, artifact.SizeBytes);
        Assert.Equal(0, handler.CallCount);
    }

    public void Dispose()
    {
        if (Directory.Exists(tempRoot))
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    private HttpUpdateArtifactDownloader CreateDownloader(HttpMessageHandler handler, TimeSpan? stallTimeout = null)
    {
        return new HttpUpdateArtifactDownloader(
            new TestHttpClientFactory(new HttpClient(handler)),
            Options.Create(new AgentOptions
            {
                UpdateStagingDirectory = tempRoot,
                UpdateDownloadStallTimeout = stallTimeout ?? TimeSpan.FromSeconds(60)
            }));
    }

    private static ComponentUpdateInstructionDto CreateInstruction(long sizeBytes)
    {
        return new ComponentUpdateInstructionDto(
            UpdateRolloutId: Guid.Parse("bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb"),
            UpdatePackageId: Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa"),
            Component: UpdateComponentNames.AgentService,
            Version: "1.2.3",
            Channel: UpdateChannelNames.Internal,
            ArtifactUri: "https://updates.afk4.test/agent-service/internal/1.2.3/agent.msi",
            Sha256: "unused",
            Signature: "unused",
            SignatureAlgorithm: UpdatePackageSignatureAlgorithmNames.EcdsaP256Sha256IeeeP1363,
            SizeBytes: sizeBytes,
            ReleaseNotes: "test");
    }

    private sealed class TestHttpClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            return client;
        }
    }

    private sealed class StaticContentHandler(byte[] bytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(bytes)
            });
        }
    }

    private sealed class FailingContentHandler(byte[] bytes, int failAfterBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new FailingReadStream(bytes, failAfterBytes))
            });
        }
    }

    private sealed class SilentAfterHandler(byte[] firstBytes) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StreamContent(new SilentAfterStream(firstBytes))
            });
        }
    }

    /// <summary>Отдаёт первые байты и замолкает, пока чтение не отменят.</summary>
    private sealed class SilentAfterStream(byte[] firstBytes) : Stream
    {
        private bool sent;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!sent)
            {
                sent = true;
                firstBytes.CopyTo(buffer);
                return firstBytes.Length;
            }

            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            throw new InvalidOperationException("Network should not be used.");
        }
    }

    private sealed class FailingReadStream(byte[] bytes, int failAfterBytes) : Stream
    {
        private int position;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => bytes.Length;

        public override long Position
        {
            get => position;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (position >= failAfterBytes)
            {
                throw new IOException("Simulated network loss.");
            }

            var readable = Math.Min(count, Math.Min(failAfterBytes - position, bytes.Length - position));
            Array.Copy(bytes, position, buffer, offset, readable);
            position += readable;

            return readable;
        }

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (position >= failAfterBytes)
            {
                throw new IOException("Simulated network loss.");
            }

            var readable = Math.Min(buffer.Length, Math.Min(failAfterBytes - position, bytes.Length - position));
            bytes.AsMemory(position, readable).CopyTo(buffer);
            position += readable;

            return ValueTask.FromResult(readable);
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }
    }
}
