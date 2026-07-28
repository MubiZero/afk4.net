using AFK4.OrganizationAdmin.App.Audit;
using AFK4.Shared.Contracts.Audit;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class AuditSearchWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid AuditRecordId = Guid.Parse("aaaaaaaa-aaaa-4aaa-aaaa-aaaaaaaaaaaa");

    [Fact]
    public async Task RefreshAsync_WithFilters_LoadsAuditRecords()
    {
        var apiClient = new RecordingOperatorAuditApiClient();
        var viewModel = new AuditSearchWorkspaceViewModel(apiClient)
        {
            ActionFilter = "sessions.start",
            OutcomeFilter = "Succeeded",
            TargetTypeFilter = "Session",
            LimitText = "25"
        };
        viewModel.ApplyContext(OrganizationId, BranchId);

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(BranchId, apiClient.LastRequest?.BranchId);
        Assert.Equal("sessions.start", apiClient.LastRequest?.Action);
        Assert.Equal("Succeeded", apiClient.LastRequest?.Outcome);
        Assert.Equal("Session", apiClient.LastRequest?.TargetType);
        Assert.Equal(25, apiClient.LastRequest?.Limit);
        var record = Assert.Single(viewModel.Records);
        Assert.Equal("sessions.start", record.Action);
        Assert.Equal("Session bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb", record.TargetSummary);
        Assert.Equal("1 audit record loaded.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task RefreshAsync_WithInvalidLimit_DoesNotCallBackendAndShowsError()
    {
        var apiClient = new RecordingOperatorAuditApiClient();
        var viewModel = new AuditSearchWorkspaceViewModel(apiClient)
        {
            LimitText = "many"
        };

        await viewModel.RefreshAsync(CancellationToken.None);

        Assert.Equal(0, apiClient.CallCount);
        Assert.Equal("Limit must be a number between 1 and 200.", viewModel.ErrorMessage);
    }

    private sealed class RecordingOperatorAuditApiClient : IOperatorAuditApiClient
    {
        public int CallCount { get; private set; }

        public OperatorAuditSearchRequest? LastRequest { get; private set; }

        public Task<AuditSearchResultDto> SearchAsync(
            OperatorAuditSearchRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;

            return Task.FromResult(new AuditSearchResultDto(
                [
                    new AuditRecordDto(
                        AuditRecordId,
                        OrganizationId,
                        request.BranchId,
                        ActorStaffUserId: Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134"),
                        "sessions.start",
                        "Session",
                        "bbbbbbbb-bbbb-4bbb-bbbb-bbbbbbbbbbbb",
                        "Succeeded",
                        "PlatformApi",
                        """{"seatId":"PC-001"}""",
                        DateTimeOffset.Parse("2026-05-14T09:00:00Z"))
                ],
                request.Limit ?? 50));
        }
    }
}
