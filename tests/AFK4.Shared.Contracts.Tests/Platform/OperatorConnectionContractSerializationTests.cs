using System.Text.Json;
using AFK4.Shared.Contracts.Platform.Operator;

namespace AFK4.Shared.Contracts.Tests.Platform;

public sealed class OperatorConnectionContractSerializationTests
{
    [Fact]
    public void ResolveOperatorConnectionRequest_RoundTripsWithSlugPair()
    {
        var request = new ResolveOperatorConnectionRequest(
            OrganizationSlug: "demo-club",
            BranchSlug: "main",
            SetupCode: null);

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<ResolveOperatorConnectionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.OrganizationSlug, copy.OrganizationSlug);
        Assert.Equal(request.BranchSlug, copy.BranchSlug);
        Assert.Null(copy.SetupCode);
    }

    [Fact]
    public void ResolveOperatorConnectionRequest_RoundTripsWithSetupCode()
    {
        var request = new ResolveOperatorConnectionRequest(
            OrganizationSlug: null,
            BranchSlug: null,
            SetupCode: "abcd1234abcd1234abcd1234abcd1234");

        var json = JsonSerializer.Serialize(request);
        var copy = JsonSerializer.Deserialize<ResolveOperatorConnectionRequest>(json);

        Assert.NotNull(copy);
        Assert.Equal(request.SetupCode, copy.SetupCode);
        Assert.Null(copy.OrganizationSlug);
        Assert.Null(copy.BranchSlug);
    }

    [Fact]
    public void ResolveOperatorConnectionResponse_RoundTripsThroughJson()
    {
        var response = new ResolveOperatorConnectionResponse(
            OrganizationId: Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08"),
            OrganizationSlug: "demo-club",
            OrganizationName: "Demo Club",
            OrganizationStatus: "active",
            OrganizationStatusReason: null,
            BranchId: Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2"),
            BranchSlug: "main",
            BranchName: "Main Branch",
            BranchCity: "Dushanbe",
            Source: OperatorConnectionResolutionSources.Slug);

        var json = JsonSerializer.Serialize(response);
        var copy = JsonSerializer.Deserialize<ResolveOperatorConnectionResponse>(json);

        Assert.NotNull(copy);
        Assert.Equal(response.OrganizationId, copy.OrganizationId);
        Assert.Equal(response.BranchId, copy.BranchId);
        Assert.Equal(response.OrganizationSlug, copy.OrganizationSlug);
        Assert.Equal(response.BranchSlug, copy.BranchSlug);
        Assert.Equal(response.Source, copy.Source);
        Assert.Equal("active", copy.OrganizationStatus);
    }

    [Fact]
    public void OperatorConnectionResolutionSources_ExposesExpectedConstants()
    {
        Assert.Equal("slug", OperatorConnectionResolutionSources.Slug);
        Assert.Equal("setup_code", OperatorConnectionResolutionSources.SetupCode);
    }
}
