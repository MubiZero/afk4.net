using AFK4.Operator.App.Auth;
using AFK4.Operator.App.Shell;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Tests;

public sealed class SignInViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public async Task SignInAsync_WithValidInputs_RaisesSignedInContext()
    {
        var authClient = new RecordingOperatorAuthApiClient();
        var viewModel = new SignInViewModel(authClient)
        {
            OrganizationIdText = OrganizationId.ToString("D"),
            UserName = "cashier",
            Password = "password"
        };
        OperatorUserContext? signedInContext = null;
        viewModel.SignedIn += context => signedInContext = context;

        await viewModel.SignInAsync(CancellationToken.None);

        Assert.Equal(OrganizationId, authClient.LastOrganizationId);
        Assert.Equal("cashier", authClient.LastUserName);
        Assert.NotNull(signedInContext);
        Assert.Equal(BranchId, signedInContext.BranchId);
        Assert.Contains(StaffPermissionNames.ViewFloorMap, signedInContext.Permissions);
        Assert.Equal("Вход выполнен: Cashier One.", viewModel.StatusMessage);
        Assert.Null(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task SignInAsync_WithInvalidOrganizationId_DoesNotCallBackendAndShowsError()
    {
        var authClient = new RecordingOperatorAuthApiClient();
        var viewModel = new SignInViewModel(authClient)
        {
            OrganizationIdText = "not-a-guid",
            UserName = "cashier",
            Password = "password"
        };

        await viewModel.SignInAsync(CancellationToken.None);

        Assert.Equal(0, authClient.SignInCallCount);
        Assert.Equal("OrganizationId должен быть корректным GUID.", viewModel.ErrorMessage);
    }

    private sealed class RecordingOperatorAuthApiClient : IOperatorAuthApiClient
    {
        public int SignInCallCount { get; private set; }

        public Guid LastOrganizationId { get; private set; }

        public string LastUserName { get; private set; } = string.Empty;

        public string LastPassword { get; private set; } = string.Empty;

        public Task<StaffSignInResponse> SignInAsync(
            Guid organizationId,
            string userName,
            string password,
            CancellationToken cancellationToken)
        {
            SignInCallCount++;
            LastOrganizationId = organizationId;
            LastUserName = userName;
            LastPassword = password;

            return Task.FromResult(new StaffSignInResponse(
                StaffUserId,
                OrganizationId,
                "Cashier One",
                "access-token",
                DateTimeOffset.Parse("2026-05-14T10:00:00Z"),
                "refresh-token",
                DateTimeOffset.Parse("2026-05-15T10:00:00Z"),
                [BranchId],
                [StaffPermissionNames.ViewFloorMap]));
        }

        public Task<StaffSignInResponse> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task ForgotPasswordByEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task ResetPasswordByEmailAsync(string userNameOrEmail, string code, string newPassword, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task ForgotPasswordByPhoneAsync(string phoneNumber, CancellationToken cancellationToken)
            => throw new NotSupportedException();

        public Task ResetPasswordByPhoneAsync(string phoneNumber, string code, string newPassword, CancellationToken cancellationToken)
            => throw new NotSupportedException();
    }
}
