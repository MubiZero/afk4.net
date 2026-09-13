using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Shared.Contracts.FloorMap;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Branding;

namespace AFK4.SetupWizard.Core;

public sealed class SetupWizardWebHostBridge(
    ISetupWizardApiClient apiClient,
    IDeviceKeyStore deviceKeyStore,
    ISetupWizardBootstrapWriter bootstrapWriter,
    SetupWizardMachineInfo machineInfo,
    ISetupWizardCompletionAction completionAction,
    ISetupWizardShellProvisioner shellProvisioner,
    ISetupWizardShellProvisioner operatorProvisioner,
    ISetupWizardOperatorLauncher operatorLauncher,
    ILogoFilePicker? logoFilePicker = null)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // Потолок на один заход: зал в сотню мест — это уже не установка, а импорт, и такие вещи
    // делаются в панели, а не пачкой запросов из мастера.
    private const int MaxSeatsPerRun = 60;

    private string? accessToken;

    // Организация нужна для установочных запросов: у всех организационных маршрутов канонический
    // префикс с её идентификатором. Берётся из ответа входа — другого источника у мастера нет.
    private Guid? organizationId;

    public async Task<string?> HandleAsync(string webMessageJson, CancellationToken cancellationToken)
    {
        SetupWizardWebBridgeRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<SetupWizardWebBridgeRequest>(webMessageJson, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }

        if (request is null ||
            string.IsNullOrWhiteSpace(request.Type) ||
            string.IsNullOrWhiteSpace(request.RequestId) ||
            !request.Type.StartsWith("wizard:", StringComparison.Ordinal))
        {
            return null;
        }

        try
        {
            object payload = request.Type switch
            {
                "wizard:phoneSignIn" => await PhoneSignInAsync(request.Payload, cancellationToken),
                "wizard:signInByLogin" => await LoginSignInAsync(request.Payload, cancellationToken),
                "wizard:signInToClub" => await ClubSignInAsync(request.Payload, cancellationToken),
                "wizard:forgotByEmail" => await ForgotPasswordByEmailAsync(request.Payload, cancellationToken),
                "wizard:resetByEmail" => await ResetPasswordByEmailAsync(request.Payload, cancellationToken),
                "wizard:forgotByPhone" => await ForgotPasswordByPhoneAsync(request.Payload, cancellationToken),
                "wizard:resetByPhone" => await ResetPasswordByPhoneAsync(request.Payload, cancellationToken),
                "wizard:discoverAuth" => await DiscoverAuthenticatedAsync(cancellationToken),
                "wizard:createSeatAuth" => await CreateSeatAuthenticatedAsync(request.Payload, cancellationToken),
                "wizard:enrollAuth" => await EnrollAuthenticatedAsync(request.Payload, cancellationToken),
                "wizard:brandingPresets" => BrandingPresetList(),
                "wizard:inviteStaff" => await InviteStaffAsync(request.Payload, cancellationToken),
                "wizard:createSeats" => await CreateSeatsAsync(request.Payload, cancellationToken),
                "wizard:createTariff" => await CreateTariffAsync(request.Payload, cancellationToken),
                "wizard:saveBranding" => await SaveBrandingAsync(request.Payload, cancellationToken),
                "wizard:uploadLogo" => await UploadLogoAsync(request.Payload, cancellationToken),
                "wizard:provisionShell" => FinalizeForRole(ReadProvisionRole(request.Payload)),
                _ => throw new InvalidOperationException($"Unsupported host bridge request: {request.Type}.")
            };

            return CreateResponse(request.RequestId, ok: true, payload, error: null);
        }
        catch (SetupWizardApiException exception)
        {
            // Structured reset error (e.g. invalid_code with a remaining-attempts count):
            // forward the backend code and attempts so the inline SMS reset screen can show them.
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new SetupWizardWebBridgeError(exception.Code, exception.Message, exception.RemainingAttempts));
        }
        catch (Exception exception) when (exception is InvalidOperationException or HttpRequestException or JsonException)
        {
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new SetupWizardWebBridgeError(ErrorCodeFor(request.Type), exception.Message));
        }
        catch (Exception exception)
        {
            // General fallback: the enroll path can throw types not mapped above — File I/O under
            // %ProgramData% (UnauthorizedAccessException/IOException), SetEnvironmentVariable(Machine)
            // (SecurityException), a corrupt device key (CryptographicException). Return a structured
            // bridge error so the unknown type does not escape and crash the wizard.
            SetupWizardStartupLog.Write($"Unhandled host bridge error for '{request.Type}'.", exception);
            return CreateResponse(
                request.RequestId,
                ok: false,
                payload: null,
                new SetupWizardWebBridgeError(ErrorCodeFor(request.Type), exception.Message));
        }
    }

    // For gaming_pc: install the bundled Player Shell, then start the agent only on success.
    // For other roles: just start the agent. Returns the shell outcome for the finish screen.
    // The finish-screen retry sends the role so it re-runs the right install; default to gaming_pc
    // for backward compatibility when no role is supplied.
    private static string ReadProvisionRole(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Object &&
            payload.TryGetProperty("role", out var role) &&
            role.ValueKind == JsonValueKind.String &&
            role.GetString() == DeviceRoleNames.ManagerWorkstation)
        {
            return DeviceRoleNames.ManagerWorkstation;
        }

        return DeviceRoleNames.GamingPc;
    }

    private WizardShellOutcome FinalizeForRole(string role)
    {
        // Each role installs its own bundled app: gaming PCs get the Player Shell, cashier/manager
        // workstations get the Organization Admin. Roles with no app just start the agent.
        var provisioner = role switch
        {
            DeviceRoleNames.GamingPc => shellProvisioner,
            DeviceRoleNames.ManagerWorkstation => operatorProvisioner,
            _ => null
        };

        if (provisioner is null)
        {
            completionAction.Complete();
            return new WizardShellOutcome("skipped", null, null);
        }

        // msiexec runs synchronously and can take minutes on a fresh PC — log the outcome so a
        // result that arrives after the JS bridge timeout is not silent.
        var result = provisioner.Provision();
        if (result.Status == ShellProvisionStatus.Failed)
        {
            // Do NOT start the agent / mark ready — the finish screen shows an error + retry.
            SetupWizardStartupLog.Write(
                $"{role} app install failed (exitCode={result.ExitCode}): {result.Message}");
            return new WizardShellOutcome("failed", result.ExitCode, result.Message);
        }

        completionAction.Complete();

        // Gaming PCs get their Player Shell launched by the agent service at the lock screen; the
        // Organization Admin has no such trigger, so start it here so the operator doesn't have to click
        // the Start Menu shortcut after enrolling.
        if (role == DeviceRoleNames.ManagerWorkstation)
        {
            operatorLauncher.Launch();
        }

        var status = result.Status == ShellProvisionStatus.AlreadyPresent ? "already_present" : "installed";
        SetupWizardStartupLog.Write($"{role} app install {status} (exitCode={result.ExitCode}).");
        return new WizardShellOutcome(status, result.ExitCode, null);
    }

    private async Task<WizardPhoneSignInResult> PhoneSignInAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardPhoneSignInPayload>(payload);
        var phone = (request.Phone ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (phone.Length == 0 || password.Length == 0)
        {
            throw new InvalidOperationException("Phone and password are required.");
        }

        var response = await apiClient.SignInByPhoneAsync(phone, password, cancellationToken);
        accessToken = response.AccessToken;
        organizationId = response.OrganizationId;
        return new WizardPhoneSignInResult(response.DisplayName);
    }

    private async Task<object> LoginSignInAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardLoginPayload>(payload);
        var login = (request.Login ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        if (login.Length == 0 || password.Length == 0)
        {
            throw new InvalidOperationException("Login and password are required.");
        }

        var result = await apiClient.SignInByLoginAsync(login, password, cancellationToken);
        if (result.SignedIn is not null)
        {
            accessToken = result.SignedIn.AccessToken;
            organizationId = result.SignedIn.OrganizationId;
            return new WizardLoginResult(result.SignedIn.DisplayName, RequiresClubChoice: false, []);
        }

        // Several clubs share this login — the web shows a picker and re-submits via signInToClub.
        var clubs = result.Clubs.Select(club => new WizardClubChoice(club.OrganizationId, club.Name)).ToArray();
        return new WizardLoginResult(DisplayName: null, RequiresClubChoice: true, clubs);
    }

    private async Task<object> ClubSignInAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardClubSignInPayload>(payload);
        var login = (request.Login ?? string.Empty).Trim();
        var password = request.Password ?? string.Empty;
        var organizationId = ParseGuid(request.OrganizationId, nameof(request.OrganizationId));
        if (login.Length == 0 || password.Length == 0)
        {
            throw new InvalidOperationException("Login and password are required.");
        }

        var response = await apiClient.SignInToClubAsync(organizationId, login, password, cancellationToken);
        accessToken = response.AccessToken;
        this.organizationId = response.OrganizationId;
        return new WizardPhoneSignInResult(response.DisplayName);
    }

    private async Task<object> ForgotPasswordByEmailAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardForgotByEmailPayload>(payload);
        var userNameOrEmail = (request.UserNameOrEmail ?? string.Empty).Trim();
        if (userNameOrEmail.Length == 0)
        {
            throw new InvalidOperationException("Login or email is required.");
        }

        await apiClient.ForgotPasswordByEmailAsync(userNameOrEmail, cancellationToken);
        return new { ok = true };
    }

    private async Task<object> ResetPasswordByEmailAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardResetByEmailPayload>(payload);
        var userNameOrEmail = (request.UserNameOrEmail ?? string.Empty).Trim();
        var code = (request.Code ?? string.Empty).Trim();
        var newPassword = request.NewPassword ?? string.Empty;
        if (userNameOrEmail.Length == 0 || code.Length == 0 || newPassword.Length == 0)
        {
            throw new InvalidOperationException("Login or email, code, and new password are required.");
        }

        await apiClient.ResetPasswordByEmailAsync(userNameOrEmail, code, newPassword, cancellationToken);
        return new { ok = true };
    }

    private async Task<object> ForgotPasswordByPhoneAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardForgotByPhonePayload>(payload);
        var phone = (request.PhoneNumber ?? string.Empty).Trim();
        if (phone.Length == 0)
        {
            throw new InvalidOperationException("Phone number is required.");
        }

        await apiClient.ForgotPasswordByPhoneAsync(phone, cancellationToken);
        return new { ok = true };
    }

    private async Task<object> ResetPasswordByPhoneAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardResetByPhonePayload>(payload);
        var phone = (request.PhoneNumber ?? string.Empty).Trim();
        var code = (request.Code ?? string.Empty).Trim();
        var newPassword = request.NewPassword ?? string.Empty;
        if (phone.Length == 0 || code.Length == 0 || newPassword.Length == 0)
        {
            throw new InvalidOperationException("Phone, code, and new password are required.");
        }

        await apiClient.ResetPasswordByPhoneAsync(phone, code, newPassword, cancellationToken);
        return new { ok = true };
    }

    // Картинки пресетов лежат на платформе: сюда едет их адрес, а не копия графики, чтобы у
    // мастера и у приложения игрока логотип был одним и тем же файлом.
    private static object BrandingPresetList() =>
        new WizardBrandingPresets(
            BrandingPresets.Ids
                .Select(id => new WizardBrandingPreset(id, BrandingPresets.Url(SetupWizardDefaults.PlatformBaseUrl, id)))
                .ToArray());

    // Свой логотип: окно выбора файла открывает нативный хост — у WebView2 своего нет, а тащить
    // путь к файлу из веба было бы и небезопасно, и невозможно.
    private async Task<object> UploadLogoAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        if (logoFilePicker is null)
        {
            throw new InvalidOperationException("File picker is unavailable in this host.");
        }

        var request = DeserializePayload<WizardUploadLogoPayload>(payload);
        var filePath = logoFilePicker.PickImage();
        if (string.IsNullOrWhiteSpace(filePath))
        {
            // Человек закрыл диалог — это не ошибка, экран просто остаётся как был.
            return new WizardLogoUploaded(null);
        }

        var media = await apiClient.UploadOrganizationLogoAsync(
            RequireOrganizationId(),
            ParseGuid(request.BranchId, nameof(request.BranchId)),
            RequireAccessToken(),
            filePath,
            cancellationToken);

        return new WizardLogoUploaded(media.Url);
    }

    private async Task<object> SaveBrandingAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardBrandingPayload>(payload);
        await apiClient.UpdateBrandingAsync(
            RequireOrganizationId(),
            RequireAccessToken(),
            string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim(),
            string.IsNullOrWhiteSpace(request.AccentColor) ? null : request.AccentColor.Trim(),
            cancellationToken);

        return new WizardBrandingSaved(true);
    }

    private async Task<object> InviteStaffAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardStaffInvitePayload>(payload);
        var displayName = (request.DisplayName ?? string.Empty).Trim();
        var phoneNumber = (request.PhoneNumber ?? string.Empty).Trim();
        var roleName = (request.RoleName ?? string.Empty).Trim();
        if (displayName.Length == 0 || phoneNumber.Length == 0 || roleName.Length == 0)
        {
            throw new InvalidOperationException("Name, phone and role are required.");
        }

        var invite = await apiClient.InviteStaffAsync(
            RequireOrganizationId(),
            ParseGuid(request.BranchId, nameof(request.BranchId)),
            RequireAccessToken(),
            displayName,
            phoneNumber,
            roleName,
            cancellationToken);

        return new WizardStaffInvited(displayName, roleName, invite.Code, invite.ExpiresAtUtc);
    }

    // Зал заводится пачкой: «ПК-1»…«ПК-N». Сервер создаёт места по одному и идемпотентен по имени,
    // поэтому повтор шага не плодит дубликаты, а докладывает недостающие.
    private async Task<object> CreateSeatsAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardCreateSeatsPayload>(payload);
        var prefix = (request.NamePrefix ?? string.Empty).Trim();
        if (prefix.Length == 0)
        {
            throw new InvalidOperationException("Seat name prefix is required.");
        }

        if (request.Count is not > 0 or > MaxSeatsPerRun)
        {
            throw new InvalidOperationException($"Seat count must be between 1 and {MaxSeatsPerRun}.");
        }

        var organizationId = RequireOrganizationId();
        var branchId = ParseGuid(request.BranchId, nameof(request.BranchId));
        var zoneId = ParseGuid(request.ZoneId, nameof(request.ZoneId));
        var accessToken = RequireAccessToken();

        var created = new List<string>();
        for (var number = 1; number <= request.Count; number++)
        {
            var seat = await apiClient.CreateSeatAuthenticatedAsync(
                organizationId, accessToken, branchId, zoneId, $"{prefix}-{number}", cancellationToken);
            created.Add(seat.Name);
        }

        return new WizardSeatsCreated(created);
    }

    private async Task<object> CreateTariffAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardCreateTariffPayload>(payload);
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0 || request.PricePerHourMinorUnits is not > 0)
        {
            throw new InvalidOperationException("Tariff name and price are required.");
        }

        var tariff = await apiClient.CreateTariffAsync(
            RequireOrganizationId(),
            ParseGuid(request.BranchId, nameof(request.BranchId)),
            RequireAccessToken(),
            name,
            request.PricePerHourMinorUnits.Value,
            cancellationToken);

        return new WizardTariffCreated(tariff.Name);
    }

    private Guid RequireOrganizationId() =>
        organizationId ?? throw new InvalidOperationException("Sign in before running install requests.");

    private string RequireAccessToken() =>
        string.IsNullOrEmpty(accessToken)
            ? throw new InvalidOperationException("Sign in with your phone before continuing.")
            : accessToken;

    private async Task<WizardDiscoverResult> DiscoverAuthenticatedAsync(CancellationToken cancellationToken)
    {
        var response = await apiClient.DiscoverAuthenticatedAsync(
            RequireOrganizationId(), RequireAccessToken(), cancellationToken);
        var branches = response.Branches
            .OrderBy(branch => branch.Name, StringComparer.OrdinalIgnoreCase)
            .Select(MapBranch)
            .ToArray();
        return new WizardDiscoverResult(response.OwnerDisplayName, branches);
    }

    private async Task<WizardSeat> CreateSeatAuthenticatedAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardCreateSeatAuthPayload>(payload);
        var branchId = ParseGuid(request.BranchId, nameof(request.BranchId));
        var zoneId = ParseGuid(request.ZoneId, nameof(request.ZoneId));
        var name = (request.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new InvalidOperationException("Seat name is required.");
        }

        var created = await apiClient.CreateSeatAuthenticatedAsync(
            RequireOrganizationId(), RequireAccessToken(), branchId, zoneId, name, cancellationToken);
        return new WizardSeat(
            created.SeatId,
            created.Name,
            created.ZoneId,
            ZoneName: request.ZoneName ?? string.Empty,
            created.SortOrder,
            Status: "Free",
            DeviceId: null,
            DeviceName: null,
            IsOnline: null);
    }

    private async Task<WizardEnrollResult> EnrollAuthenticatedAsync(JsonElement payload, CancellationToken cancellationToken)
    {
        var request = DeserializePayload<WizardEnrollAuthPayload>(payload);
        var branchId = ParseGuid(request.BranchId, nameof(request.BranchId));
        var role = (request.Role ?? string.Empty).Trim();
        if (role is not (DeviceRoleNames.GamingPc or DeviceRoleNames.ManagerWorkstation))
        {
            throw new InvalidOperationException("Role must be GamingPc or ManagerWorkstation.");
        }

        Guid? seatId = null;
        if (role == DeviceRoleNames.GamingPc)
        {
            if (string.IsNullOrWhiteSpace(request.SeatId))
            {
                throw new InvalidOperationException("Seat is required for a gaming PC.");
            }
            seatId = ParseGuid(request.SeatId, nameof(request.SeatId));
        }

        var displayName = string.IsNullOrWhiteSpace(request.DisplayName)
            ? machineInfo.MachineName
            : request.DisplayName.Trim();

        var publicKey = await deviceKeyStore.GetOrCreatePublicKeyPemAsync(cancellationToken);
        var response = await apiClient.EnrollAuthenticatedAsync(
            RequireOrganizationId(),
            RequireAccessToken(),
            new AuthenticatedInstallEnrollRequest(
                branchId,
                seatId,
                role,
                displayName,
                machineInfo.MachineName,
                publicKey),
            cancellationToken);

        bootstrapWriter.Write(new SetupWizardBootstrapConfig(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            response.CredentialId,
            response.CredentialSecret,
            role,
            response.ApiBaseUrl,
            response.UpdateChannel,
            response.LeaseSigningPublicKeyPem,
            response.UpdatePackageSigningPublicKeyPem));
        var shell = FinalizeForRole(role);

        return new WizardEnrollResult(
            response.OrganizationId,
            response.BranchId,
            response.DeviceId,
            role,
            displayName,
            machineInfo.MachineName,
            response.EnrollmentState,
            response.ApiBaseUrl,
            response.UpdateChannel,
            shell);
    }

    private static WizardBranch MapBranch(InstallBranchDto branch)
    {
        var zoneLookup = branch.FloorMap.Zones
            .OrderBy(zone => zone.SortOrder)
            .ToDictionary(zone => zone.ZoneId, zone => zone);

        var zones = branch.FloorMap.Zones
            .OrderBy(zone => zone.SortOrder)
            .Select(zone => new WizardZone(zone.ZoneId, zone.Name, zone.SortOrder))
            .ToArray();

        var seats = branch.FloorMap.Seats
            .OrderBy(seat => ZoneSortOrder(zoneLookup, seat.ZoneId))
            .ThenBy(seat => seat.SortOrder)
            .ThenBy(seat => seat.SeatName, StringComparer.OrdinalIgnoreCase)
            .Select(seat => new WizardSeat(
                seat.SeatId,
                seat.SeatName,
                seat.ZoneId,
                seat.ZoneName,
                seat.SortOrder,
                seat.State,
                seat.DeviceId,
                seat.DeviceName,
                seat.IsDeviceOnline))
            .ToArray();

        return new WizardBranch(
            branch.BranchId,
            branch.Slug,
            branch.Name,
            zones,
            seats,
            branch.FreeSeatIds.ToArray());
    }

    private static int ZoneSortOrder(IDictionary<Guid, FloorMapZoneDto> lookup, Guid zoneId) =>
        lookup.TryGetValue(zoneId, out var zone) ? zone.SortOrder : int.MaxValue;

    private static Guid ParseGuid(string? value, string fieldName)
    {
        if (!Guid.TryParse(value, out var parsed) || parsed == Guid.Empty)
        {
            throw new InvalidOperationException($"{fieldName} must be a valid GUID.");
        }

        return parsed;
    }

    private static T DeserializePayload<T>(JsonElement payload)
    {
        if (payload.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            throw new InvalidOperationException("Host bridge payload is required.");
        }

        return payload.Deserialize<T>(JsonOptions)
            ?? throw new InvalidOperationException("Host bridge payload is invalid.");
    }

    private static string ErrorCodeFor(string? requestType) => requestType switch
    {
        "wizard:phoneSignIn" => "wizard_phone_sign_in_failed",
        "wizard:signInByLogin" => "wizard_sign_in_failed",
        "wizard:signInToClub" => "wizard_sign_in_failed",
        "wizard:forgotByEmail" => "wizard_forgot_password_failed",
        "wizard:resetByEmail" => "wizard_reset_password_failed",
        "wizard:forgotByPhone" => "wizard_forgot_password_failed",
        "wizard:resetByPhone" => "wizard_reset_password_failed",
        "wizard:discoverAuth" => "wizard_discover_failed",
        "wizard:createSeatAuth" => "wizard_create_seat_failed",
        "wizard:enrollAuth" => "wizard_enroll_failed",
        "wizard:provisionShell" => "wizard_shell_provision_failed",
        _ => "wizard_request_failed"
    };

    private static string CreateResponse(
        string requestId,
        bool ok,
        object? payload,
        SetupWizardWebBridgeError? error)
    {
        return JsonSerializer.Serialize(
            new SetupWizardWebBridgeResponse("host:response", requestId, ok, payload, error),
            JsonOptions);
    }

    private sealed record SetupWizardWebBridgeRequest(
        string? Type,
        string? RequestId,
        JsonElement Payload);

    private sealed record SetupWizardWebBridgeResponse(
        string Type,
        string RequestId,
        bool Ok,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)]
        object? Payload,
        SetupWizardWebBridgeError? Error);

    private sealed record SetupWizardWebBridgeError(string Code, string Message, int? RemainingAttempts = null);

    private sealed record WizardPhoneSignInPayload(string? Phone, string? Password);

    private sealed record WizardLoginPayload(string? Login, string? Password);

    private sealed record WizardClubSignInPayload(string? OrganizationId, string? Login, string? Password);

    private sealed record WizardForgotByEmailPayload(string? UserNameOrEmail);

    private sealed record WizardResetByEmailPayload(string? UserNameOrEmail, string? Code, string? NewPassword);

    private sealed record WizardForgotByPhonePayload(string? PhoneNumber);

    private sealed record WizardResetByPhonePayload(string? PhoneNumber, string? Code, string? NewPassword);

    private sealed record WizardCreateSeatAuthPayload(
        string? BranchId,
        string? ZoneId,
        string? ZoneName,
        string? Name);

    private sealed record WizardEnrollAuthPayload(
        string? BranchId,
        string? SeatId,
        string? Role,
        string? DisplayName);

    private sealed record WizardPhoneSignInResult(string DisplayName);

    private sealed record WizardLoginResult(
        string? DisplayName,
        bool RequiresClubChoice,
        IReadOnlyList<WizardClubChoice> Clubs);

    private sealed record WizardClubChoice(Guid OrganizationId, string Name);

    private sealed record WizardBrandingPayload(string? LogoUrl, string? AccentColor);

    private sealed record WizardBrandingPreset(string Id, string Url);

    private sealed record WizardBrandingPresets(IReadOnlyList<WizardBrandingPreset> Presets);

    private sealed record WizardBrandingSaved(bool Saved);

    private sealed record WizardUploadLogoPayload(string? BranchId);

    private sealed record WizardLogoUploaded(string? LogoUrl);

    private sealed record WizardStaffInvitePayload(string? BranchId, string? DisplayName, string? PhoneNumber, string? RoleName);

    private sealed record WizardStaffInvited(string DisplayName, string RoleName, string Code, DateTimeOffset ExpiresAtUtc);

    private sealed record WizardCreateSeatsPayload(string? BranchId, string? ZoneId, string? NamePrefix, int? Count);

    private sealed record WizardSeatsCreated(IReadOnlyList<string> Names);

    private sealed record WizardCreateTariffPayload(string? BranchId, string? Name, long? PricePerHourMinorUnits);

    private sealed record WizardTariffCreated(string Name);

    private sealed record WizardDiscoverResult(string OwnerName, IReadOnlyList<WizardBranch> Branches);

    private sealed record WizardBranch(
        Guid BranchId,
        string BranchSlug,
        string BranchName,
        IReadOnlyList<WizardZone> Zones,
        IReadOnlyList<WizardSeat> Seats,
        IReadOnlyList<Guid> FreeSeatIds);

    private sealed record WizardZone(Guid ZoneId, string Name, int SortOrder);

    private sealed record WizardSeat(
        Guid SeatId,
        string PcName,
        Guid ZoneId,
        string ZoneName,
        int SortOrder,
        string Status,
        Guid? DeviceId,
        string? DeviceName,
        bool? IsOnline);

    private sealed record WizardEnrollResult(
        Guid OrganizationId,
        Guid BranchId,
        Guid DeviceId,
        string Role,
        string DisplayName,
        string MachineName,
        string EnrollmentState,
        string ApiBaseUrl,
        string UpdateChannel,
        WizardShellOutcome Shell);

    private sealed record WizardShellOutcome(string Status, int? ExitCode, string? Message);
}
