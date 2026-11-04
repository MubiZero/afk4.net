using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Shared.Contracts.Branding;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Install;
using AFK4.Shared.Contracts.Media;
using AFK4.Shared.Contracts.Tariffs;
using AFK4.SetupWizard.Core.Kiosk;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Запросы и ответы мастера к API платформы. В Native AOT нет JSON на отражении, поэтому каждый тип
/// перечислен здесь и разбирается сгенерированным кодом (docs/roadmap/windows7.md). Новый тип запроса
/// или ответа добавляют сюда же.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(StaffSignInByPhoneRequest))]
[JsonSerializable(typeof(StaffSignInByLoginRequest))]
[JsonSerializable(typeof(StaffSignInRequest))]
[JsonSerializable(typeof(StaffSignInResponse))]
[JsonSerializable(typeof(StaffSignInChooseClubResponse))]
[JsonSerializable(typeof(StaffForgotPasswordRequest))]
[JsonSerializable(typeof(StaffResetPasswordRequest))]
[JsonSerializable(typeof(StaffForgotPasswordByPhoneRequest))]
[JsonSerializable(typeof(StaffResetPasswordByPhoneRequest))]
[JsonSerializable(typeof(InstallDiscoverResponse))]
[JsonSerializable(typeof(AuthenticatedInstallCreateSeatRequest))]
[JsonSerializable(typeof(InstallCreateSeatResponse))]
[JsonSerializable(typeof(UpdateOrganizationBrandingRequest))]
[JsonSerializable(typeof(CreateStaffInviteRequest))]
[JsonSerializable(typeof(StaffInviteDto))]
[JsonSerializable(typeof(UploadedMediaDto))]
[JsonSerializable(typeof(CreateTariffRequest))]
[JsonSerializable(typeof(TariffDto))]
[JsonSerializable(typeof(CreateTariffVersionRequest))]
[JsonSerializable(typeof(TariffVersionDto))]
[JsonSerializable(typeof(AuthenticatedInstallEnrollRequest))]
[JsonSerializable(typeof(InstallEnrollResponse))]
[JsonSerializable(typeof(InstallCodeEnrollRequest))]
internal sealed partial class SetupWizardApiJson : JsonSerializerContext;

/// <summary>
/// Сообщения между страницей мастера и хостом. Пустые значения в ответах не пишутся, кроме
/// <c>payload</c> (его нельзя опускать: страница ждёт ключ).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(SetupWizardWebHostBridge.SetupWizardWebBridgeRequest))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.SetupWizardWebBridgeResponse))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardPhoneSignInPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardLoginPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardClubSignInPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardForgotByEmailPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardResetByEmailPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardForgotByPhonePayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardResetByPhonePayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardCreateSeatAuthPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardEnrollAuthPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardBrandingPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardUploadLogoPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardStaffInvitePayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardCreateSeatsPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardCreateTariffPayload))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardPhoneSignInResult))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardLoginResult))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardBrandingPresets))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardBrandingSaved))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardLogoUploaded))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardStaffInvited))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardSeatsCreated))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardTariffCreated))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardDiscoverResult))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardSeat))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardEnrollResult))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardKioskStatus))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardOk))]
[JsonSerializable(typeof(SetupWizardWebHostBridge.WizardEmpty))]
[JsonSerializable(typeof(WizardShellOutcome))]
internal sealed partial class SetupWizardBridgeJson : JsonSerializerContext;

/// <summary>
/// Файлы на диске (bootstrap.json, состояние киоска): настройки System.Text.Json по умолчанию
/// (PascalCase) и отступы, как и раньше. Формат не меняем, чтобы уже записанные файлы читались.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(Dictionary<string, object>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(KioskState))]
internal sealed partial class SetupWizardFileJson : JsonSerializerContext;
