using System.Net.Http;
using AFK4.Agent.Service.Showcase;
using AFK4.Shared.Contracts.Ads;
using Microsoft.Extensions.Logging.Abstractions;

namespace AFK4.Agent.Service.Tests.Showcase;

public sealed class ShowcaseImpressionsTests
{
    private static readonly DateTimeOffset Start = DateTimeOffset.Parse("2026-09-25T18:00:00Z");

    [Fact]
    public async Task OnlyAds_ThatStayedOnScreen_AreCounted_AsDailySums()
    {
        var fixture = new Fixture();
        fixture.Impressions.Record("ad:1", 9000);
        fixture.Impressions.Record("ad:1", 9000);
        fixture.Impressions.Record("news:1", 9000);
        // Мелькнула на долю секунды — человек подошёл к ПК; это не показ.
        fixture.Impressions.Record("ad:2", 300);

        await fixture.Impressions.FlushIfDueAsync(CancellationToken.None);

        var item = Assert.Single(Assert.Single(fixture.Client.Sent).Items);
        Assert.Equal(new ShowcaseImpressionDto("ad:1", "2026-09-25", 2, 18000), item);
    }

    [Fact]
    public async Task AnUndeliveredBatch_IsRetriedWithTheSameKey_AndNewShowsWaitForTheNextOne()
    {
        var fixture = new Fixture();
        fixture.Impressions.Record("ad:1", 9000);
        fixture.Client.Failure = new HttpRequestException("offline");
        await fixture.Impressions.FlushIfDueAsync(CancellationToken.None);

        fixture.Impressions.Record("ad:1", 9000);
        fixture.Client.Failure = null;
        fixture.Time.Advance(ShowcaseImpressions.RetryAfterFailure);
        await fixture.Impressions.FlushIfDueAsync(CancellationToken.None);

        Assert.Equal(2, fixture.Client.Attempts.Count);
        Assert.Equal(fixture.Client.Attempts[0].BatchId, fixture.Client.Attempts[1].BatchId);
        Assert.Equal(1, Assert.Single(fixture.Client.Sent[0].Items).Impressions);

        // Второй показ уйдёт следующей пачкой, через час.
        await fixture.Impressions.FlushIfDueAsync(CancellationToken.None);
        Assert.Single(fixture.Client.Sent);
        fixture.Time.Advance(ShowcaseImpressions.FlushEvery);
        await fixture.Impressions.FlushIfDueAsync(CancellationToken.None);
        Assert.Equal(2, fixture.Client.Sent.Count);
        Assert.NotEqual(fixture.Client.Sent[0].BatchId, fixture.Client.Sent[1].BatchId);
    }

    [Fact]
    public async Task ARestartedService_SendsWhatItCountedBefore()
    {
        var store = new MemoryStore();
        var before = new Fixture(store);
        before.Impressions.Record("ad:1", 9000);

        var after = new Fixture(store);
        await after.Impressions.FlushIfDueAsync(CancellationToken.None);

        Assert.Equal("ad:1", Assert.Single(Assert.Single(after.Client.Sent).Items).CardId);
    }

    [Fact]
    public async Task ARefusedBatch_IsDropped_SoItDoesNotBlockTheNextOnes()
    {
        var fixture = new Fixture();
        fixture.Impressions.Record("ad:1", 9000);
        fixture.Client.Failure = new InvalidDataException("refused");

        await fixture.Impressions.FlushIfDueAsync(CancellationToken.None);

        Assert.Null(fixture.Store.Saved!.Pending);
    }

    private sealed class Fixture
    {
        public Fixture(MemoryStore? store = null)
        {
            Store = store ?? new MemoryStore();
            Impressions = new ShowcaseImpressions(Store, Client, Time, NullLogger<ShowcaseImpressions>.Instance);
        }

        public MemoryStore Store { get; }

        public FakeClient Client { get; } = new();

        public MovableTime Time { get; } = new(Start);

        public ShowcaseImpressions Impressions { get; }
    }

    private sealed class MemoryStore : IShowcaseImpressionStore
    {
        public ShowcaseImpressionLog? Saved { get; private set; }

        public ShowcaseImpressionLog? Load() => Saved;

        public void Save(ShowcaseImpressionLog log) => Saved = log;
    }

    private sealed class FakeClient : IShowcaseImpressionClient
    {
        public Exception? Failure { get; set; }

        public List<ShowcaseImpressionBatch> Attempts { get; } = [];

        public List<ShowcaseImpressionBatch> Sent { get; } = [];

        public Task SendAsync(ShowcaseImpressionBatch batch, CancellationToken cancellationToken)
        {
            Attempts.Add(batch);
            if (Failure is not null) return Task.FromException(Failure);
            Sent.Add(batch);
            return Task.CompletedTask;
        }
    }

    private sealed class MovableTime(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;

        public override DateTimeOffset GetUtcNow() => now;

        public void Advance(TimeSpan by) => now += by;
    }
}
