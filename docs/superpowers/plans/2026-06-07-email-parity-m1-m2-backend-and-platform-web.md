# Email parity M1+M2 — backend login-by-email + Platform.Web reset screens — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let staff log in by email wherever they log in by login (M1, backend), and replace the Platform.Web password-reset placeholder with a real channel-aware screen offering both email and SMS reset (M2).

**Architecture:** M1 is a surgical change to `PasswordHashingStaffCredentialService`: the org-scoped `SignInAsync` resolves a user by username **or** email (username first, mirroring `EfStaffPasswordResetService`), and `SignInByLoginAsync` adds the email branch to its candidate query — both reusing the existing club-picker resolution. No DTO, no migration. M2 is pure Platform.Web (a browser SPA that fetches the API directly): four new `StaffAuthApiClient` methods over existing endpoints, two new auth screens (`ForgotPassword`, `ResetPassword`), a "Forgot password?" link, a relabel of the login field, and i18n keys in `locales/{ru,en,tg}.json`.

**Tech Stack:** .NET 10 minimal API + EF Core + xunit (backend); React 19 + TypeScript + Vite + `bun test` (happy-dom + @testing-library/react) + the in-repo `@afk4/i18n` catalog (frontend).

**Scope boundary:** This plan delivers M1 + M2 only. M3 (Operator.App.Web), M4 (SetupWizard.Web), and M5 (parity check) are separate plans written after M2 lands, because the WebView2 reset screens mirror the Platform.Web screen built here. The email-reset email currently sends a **code** (an opaque token), not a clickable link — so the reset page accepts the token in a field (prefilled from `?token=` when present). Turning the email into a clickable link (template + base-URL config) is a deliberate future enhancement, out of scope here.

---

## File map

**M1 (backend):**
- Modify: `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs` — email-or-username resolution.
- Modify: `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs` — email login tests + seed helpers.

**M2 (Platform.Web):**
- Modify: `src/AFK4.Platform.Web/src/api/staffAuthApi.ts` — 4 reset methods + `remainingAttempts` parsing.
- Modify: `src/AFK4.Platform.Web/src/api/staffAuthApi.test.ts` — tests for the new methods.
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` — new auth keys; then regenerate `packages/i18n/src/messages.ts`.
- Create: `src/AFK4.Platform.Web/src/components/ForgotPassword.tsx` + `ForgotPassword.test.tsx`.
- Create: `src/AFK4.Platform.Web/src/components/ResetPassword.tsx` + `ResetPassword.test.tsx`.
- Modify: `src/AFK4.Platform.Web/src/components/StaffSignIn.tsx` — "Forgot password?" link.
- Modify: `src/AFK4.Platform.Web/src/App.tsx` — render the new screens, carry `?token=` on the reset route, remove `ReservedAuthPage`.

---

## M1 — Backend: login by email

### Task 1: Email-or-username resolution in the credential service

**Files:**
- Modify: `src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs`

- [ ] **Step 1: Add a seed helper + the first failing test**

In `StaffAuthenticationEndpointTests.cs`, add this seed helper next to the other `private static async Task Seed…` helpers. It adds a user to the existing org A (`TestIds.OrganizationId`) whose **username differs from its email**, so a successful email sign-in can only come from the email branch:

```csharp
// Adds a user to org A whose UserName differs from its Email, so an email login
// must resolve via the email branch (not the username branch).
private static async Task SeedEmailUserInOrgAAsync(
    PlatformApiFactory factory, string userName, string email, string password)
{
    await using var scope = factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var hasher = new PasswordHasher<StaffUserEntity>();
    var user = new StaffUserEntity
    {
        StaffUserId = Guid.NewGuid(),
        OrganizationId = TestIds.OrganizationId,
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = email,
        DisplayName = "Email User",
        IsActive = true,
        CreatedAtUtc = DateTimeOffset.Parse("2026-05-12T00:00:00Z")
    };
    user.PasswordHash = hasher.HashPassword(user, password);
    dbContext.StaffUsers.Add(user);
    dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
    {
        StaffRoleAssignmentId = Guid.NewGuid(),
        StaffUserId = user.StaffUserId,
        OrganizationId = TestIds.OrganizationId,
        BranchId = TestIds.BranchId,
        RoleName = StaffRoleNames.Owner
    });
    await dbContext.SaveChangesAsync();
}
```

Add the failing test:

```csharp
[Fact]
public async Task PostStaffSignInByLogin_WithEmail_SingleClub_ReturnsAccessToken()
{
    await using var factory = new PlatformApiFactory();
    await SeedTechnicianAsync(factory); // creates org A
    await SeedEmailUserInOrgAAsync(factory, "owner-login", "owner@afk4.test", "Passw0rd!");
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync(
        "/api/auth/staff/sign-in-by-login",
        new StaffSignInByLoginRequest("owner@afk4.test", "Passw0rd!"));
    var body = await response.Content.ReadFromJsonAsync<StaffSignInResponse>();

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    Assert.NotNull(body);
    Assert.Equal(TestIds.OrganizationId, body.OrganizationId);
}
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PostStaffSignInByLogin_WithEmail_SingleClub"`
Expected: FAIL — status is `Unauthorized` (the email branch doesn't exist yet, so resolution returns null).

- [ ] **Step 3: Implement email-or-username resolution**

In `PasswordHashingStaffCredentialService.cs`, replace the body of `SignInAsync` (the resolution + verify block) so it uses a shared resolver. Replace lines 26–43 (the `var normalizedUserName = …` through the `return result == …` statement) with:

```csharp
        var user = await ResolveOrgUserAsync(request.OrganizationId, request.UserName, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        return result == PasswordVerificationResult.Failed
            ? null
            : await tokenService.IssueAsync(user, cancellationToken);
```

Add this private helper at the end of the class (before the closing brace):

```csharp
    // Resolves an active staff user in the org by username first, then by email
    // (case-insensitive). Username wins on the pathological collision. Mirrors
    // EfStaffPasswordResetService.ResolveStaffAsync.
    private async Task<StaffUserEntity?> ResolveOrgUserAsync(
        Guid organizationId, string loginOrEmail, CancellationToken cancellationToken)
    {
        var normalizedUserName = loginOrEmail.Trim().ToUpperInvariant();
        var byUserName = await dbContext.StaffUsers.SingleOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.NormalizedUserName == normalizedUserName &&
                candidate.IsActive,
            cancellationToken);
        if (byUserName is not null)
        {
            return byUserName;
        }

        var loweredEmail = loginOrEmail.Trim().ToLowerInvariant();
        return await dbContext.StaffUsers.FirstOrDefaultAsync(
            candidate =>
                candidate.OrganizationId == organizationId &&
                candidate.Email != null &&
                candidate.Email.ToLower() == loweredEmail &&
                candidate.IsActive,
            cancellationToken);
    }
```

In `SignInByLoginAsync`, replace the candidate query (lines 87–92, from `var normalizedLogin = …` through `.ToListAsync(cancellationToken);`) with an email-aware version:

```csharp
        var normalizedLogin = request.Login.Trim().ToUpperInvariant();
        var loweredLogin = request.Login.Trim().ToLowerInvariant();
        var candidates = await dbContext.StaffUsers
            .AsNoTracking()
            .Where(candidate => candidate.IsActive &&
                (candidate.NormalizedUserName == normalizedLogin ||
                 (candidate.Email != null && candidate.Email.ToLower() == loweredLogin)))
            .Select(candidate => new { candidate.OrganizationId, candidate.StaffUserId, candidate.PasswordHash })
            .ToListAsync(cancellationToken);
```

Still in `SignInByLoginAsync`, dedupe the matched orgs so two matching rows in one org (a username row and an email row) don't fake a multi-club conflict. Replace the line `if (matchedOrgIds.Count == 0)` … through the single-club block by first deduping. Concretely, immediately after the `foreach` loop that fills `matchedOrgIds`, insert:

```csharp
        matchedOrgIds = matchedOrgIds.Distinct().ToList();
```

(The `matchedOrgIds` variable is declared with `var … = new List<Guid>();`; reassigning it to the deduped list is fine. The single/multi-club branches below are unchanged — the single-club branch re-calls `SignInAsync` with `request.Login`, which now resolves an email too.)

- [ ] **Step 4: Run the test to verify it passes**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~PostStaffSignInByLogin_WithEmail_SingleClub"`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Identity/PasswordHashingStaffCredentialService.cs tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs
git commit -m "feat(identity): resolve staff login by email or username"
```

### Task 2: Email login edge cases (org-scoped, multi-club, wrong/unknown)

**Files:**
- Test: `tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs`

- [ ] **Step 1: Add the edge-case tests**

Add a second seed helper that creates **org B** with an email user (distinct username, same email + password as an org-A user, to drive the club picker):

```csharp
// Creates org B and adds an email user there (username differs from email).
private static async Task SeedEmailUserInSecondOrgAsync(
    PlatformApiFactory factory, string userName, string email, string password)
{
    await using var scope = factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
    var hasher = new PasswordHasher<StaffUserEntity>();
    var createdAt = DateTimeOffset.Parse("2026-05-12T00:00:00Z");
    dbContext.Organizations.Add(new OrganizationEntity
    {
        OrganizationId = SecondOrgId,
        Slug = "second-club",
        Name = "Second Org",
        CreatedAtUtc = createdAt
    });
    dbContext.Branches.Add(new BranchEntity
    {
        BranchId = SecondBranchId,
        OrganizationId = SecondOrgId,
        Slug = "main",
        Name = "Second Branch",
        CreatedAtUtc = createdAt
    });
    var user = new StaffUserEntity
    {
        StaffUserId = Guid.NewGuid(),
        OrganizationId = SecondOrgId,
        UserName = userName,
        NormalizedUserName = userName.ToUpperInvariant(),
        Email = email,
        DisplayName = "Email User B",
        IsActive = true,
        CreatedAtUtc = createdAt
    };
    user.PasswordHash = hasher.HashPassword(user, password);
    dbContext.StaffUsers.Add(user);
    dbContext.StaffRoleAssignments.Add(new StaffRoleAssignmentEntity
    {
        StaffRoleAssignmentId = Guid.NewGuid(),
        StaffUserId = user.StaffUserId,
        OrganizationId = SecondOrgId,
        BranchId = SecondBranchId,
        RoleName = StaffRoleNames.Owner
    });
    await dbContext.SaveChangesAsync();
}
```

Add the tests:

```csharp
[Fact]
public async Task PostStaffSignIn_WithEmailInsteadOfUserName_ReturnsAccessToken()
{
    await using var factory = new PlatformApiFactory();
    await SeedTechnicianAsync(factory);
    await SeedEmailUserInOrgAAsync(factory, "owner-login", "owner@afk4.test", "Passw0rd!");
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync(
        "/api/auth/staff/sign-in",
        new StaffSignInRequest(TestIds.OrganizationId, "owner@afk4.test", "Passw0rd!"));

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
}

[Fact]
public async Task PostStaffSignInByLogin_WithEmailWrongPassword_ReturnsUnauthorized()
{
    await using var factory = new PlatformApiFactory();
    await SeedTechnicianAsync(factory);
    await SeedEmailUserInOrgAAsync(factory, "owner-login", "owner@afk4.test", "Passw0rd!");
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync(
        "/api/auth/staff/sign-in-by-login",
        new StaffSignInByLoginRequest("owner@afk4.test", "wrong-password"));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task PostStaffSignInByLogin_WithUnknownEmail_ReturnsUnauthorized()
{
    await using var factory = new PlatformApiFactory();
    await SeedTechnicianAsync(factory);
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync(
        "/api/auth/staff/sign-in-by-login",
        new StaffSignInByLoginRequest("ghost@afk4.test", "Passw0rd!"));

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
}

[Fact]
public async Task PostStaffSignInByLogin_SameEmailTwoClubsSamePassword_ReturnsChooseClub()
{
    await using var factory = new PlatformApiFactory();
    await SeedTechnicianAsync(factory);
    await SeedEmailUserInOrgAAsync(factory, "owner-a", "shared@afk4.test", "Same-pass");
    await SeedEmailUserInSecondOrgAsync(factory, "owner-b", "shared@afk4.test", "Same-pass");
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync(
        "/api/auth/staff/sign-in-by-login",
        new StaffSignInByLoginRequest("shared@afk4.test", "Same-pass"));
    var body = await response.Content.ReadFromJsonAsync<StaffSignInChooseClubResponse>();

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    Assert.NotNull(body);
    Assert.Equal(2, body.Clubs.Count);
    Assert.Contains(body.Clubs, c => c.OrganizationId == TestIds.OrganizationId);
    Assert.Contains(body.Clubs, c => c.OrganizationId == SecondOrgId);
}
```

- [ ] **Step 2: Run the M1 tests to verify they pass**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~StaffAuthenticationEndpointTests"`
Expected: PASS — all sign-in tests (existing username tests + the new email tests) green.

- [ ] **Step 3: Commit**

```bash
git add tests/AFK4.Platform.Api.Tests/StaffAuthenticationEndpointTests.cs
git commit -m "test(identity): cover email login edge cases"
```

---

## M2 — Platform.Web: channel-aware reset screens + email login label

### Task 3: Reset API methods on `StaffAuthApiClient`

**Files:**
- Modify: `src/AFK4.Platform.Web/src/api/staffAuthApi.ts`
- Test: `src/AFK4.Platform.Web/src/api/staffAuthApi.test.ts`

- [ ] **Step 1: Write failing tests for the new methods**

Append these tests inside the `describe('StaffAuthApiClient', …)` block in `staffAuthApi.test.ts`:

```typescript
  it('requests an email reset through forgot-password', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, { message: 'ok' }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.forgotPasswordByEmail('owner@demo.test');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/forgot-password');
    expect(JSON.parse(call[1].body as string)).toEqual({ userNameOrEmail: 'owner@demo.test' });
  });

  it('completes a token reset through reset-password', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, { message: 'ok' }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.resetPasswordByToken('tok.en', 'Passw0rd!New');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/reset-password');
    expect(JSON.parse(call[1].body as string)).toEqual({ token: 'tok.en', newPassword: 'Passw0rd!New' });
  });

  it('requests an SMS reset through forgot-password-by-phone', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, { expiresInSeconds: 300, resendAfterSeconds: 60 }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.forgotPasswordByPhone('+992937380070');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/forgot-password-by-phone');
    expect(JSON.parse(call[1].body as string)).toEqual({ phoneNumber: '+992937380070' });
  });

  it('completes an SMS reset through reset-password-by-phone', async () => {
    const fetchImpl = mock(async () => jsonResponse(200, { message: 'ok' }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await client.resetPasswordByPhone('+992937380070', '123456', 'Passw0rd!New');

    const call = fetchImpl.mock.calls[0] as unknown as [string, RequestInit];
    expect(call[0]).toBe('http://localhost/api/auth/staff/reset-password-by-phone');
    expect(JSON.parse(call[1].body as string)).toEqual({
      phoneNumber: '+992937380070',
      code: '123456',
      newPassword: 'Passw0rd!New'
    });
  });

  it('surfaces remainingAttempts from a bad SMS reset code', async () => {
    const fetchImpl = mock(async () => jsonResponse(400, { error: 'invalid_code', remainingAttempts: 2 }));
    const client = new StaffAuthApiClient({
      baseUrl: 'http://localhost',
      fetchImpl: fetchImpl as unknown as typeof fetch,
      session: null,
      onSessionChanged: () => {}
    });

    await expect(client.resetPasswordByPhone('+992937380070', '000000', 'Passw0rd!New'))
      .rejects.toMatchObject({ status: 400, errorCode: 'invalid_code', remainingAttempts: 2 });
  });
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test src/api/staffAuthApi.test.ts`
Expected: FAIL — `client.forgotPasswordByEmail is not a function` (and the other new methods).

- [ ] **Step 3: Implement the methods and `remainingAttempts` parsing**

In `staffAuthApi.ts`, add these four methods to `StaffAuthApiClient` (place them after `signInToClub`, before `signOutLocal`):

```typescript
  public async forgotPasswordByEmail(userNameOrEmail: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/forgot-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ userNameOrEmail })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset request failed.');
    }
  }

  public async resetPasswordByToken(token: string, newPassword: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/reset-password`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ token, newPassword })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset failed.');
    }
  }

  public async forgotPasswordByPhone(phoneNumber: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/forgot-password-by-phone`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneNumber })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset request failed.');
    }
  }

  public async resetPasswordByPhone(phoneNumber: string, code: string, newPassword: string): Promise<void> {
    const response = await this.fetchImpl(`${this.baseUrl}/api/auth/staff/reset-password-by-phone`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ phoneNumber, code, newPassword })
    });
    if (!response.ok) {
      throw await toApiError(response, 'Reset failed.');
    }
  }
```

Then update the module-level `toApiError` so it parses `remainingAttempts` (mirroring `platformApi.ts`). Replace its body with:

```typescript
async function toApiError(response: Response, fallbackMessage: string): Promise<PlatformApiError> {
  let message = fallbackMessage;
  let code: string | null = null;
  let remainingAttempts: number | null = null;
  try {
    const text = await response.text();
    if (text.length > 0) {
      const parsed = JSON.parse(text) as { error?: string; status?: string; remainingAttempts?: number };
      if (typeof parsed.error === 'string' && parsed.error.length > 0) {
        message = parsed.error;
        code = parsed.error;
      }
      if (typeof parsed.status === 'string' && parsed.status.length > 0) {
        message = `${message} (${parsed.status})`;
      }
      if (typeof parsed.remainingAttempts === 'number') {
        remainingAttempts = parsed.remainingAttempts;
      }
    }
  } catch {
    // Preserve the fallback when the API returns a non-JSON error body.
  }
  return new PlatformApiError(response.status, message, code, remainingAttempts);
}
```

- [ ] **Step 4: Run the tests to verify they pass**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test src/api/staffAuthApi.test.ts`
Expected: PASS — all StaffAuthApiClient tests green.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/api/staffAuthApi.ts src/AFK4.Platform.Web/src/api/staffAuthApi.test.ts
git commit -m "feat(platform-web): add staff password-reset api methods"
```

### Task 4: i18n keys

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Regenerate: `packages/i18n/src/messages.ts` (via `bun run gen`)

- [ ] **Step 1: Relabel the login field and replace the reset placeholder keys**

In `locales/ru.json`: change `"auth.field.login"` to `"Логин или email"`. Delete the line `"auth.reset.message": …` (the "coming soon" placeholder — it is removed with `ReservedAuthPage` in Task 7). Keep `"auth.reset.title"` and `"auth.reset.back"`.

In `locales/en.json`: change `"auth.field.login"` to `"Login or email"`. Delete `"auth.reset.message"`. Keep `"auth.reset.title"` and `"auth.reset.back"`.

In `locales/tg.json`: change `"auth.field.login"` to `"Логин или email"`. Delete `"auth.reset.message"`. Keep `"auth.reset.title"` and `"auth.reset.back"`.

- [ ] **Step 2: Add the new keys**

Append these key/value lines to the auth block of each locale file (mind trailing commas — keep valid JSON). The `tg` values intentionally mirror `ru`, matching the existing convention for the auth section (tg also falls back to ru at runtime).

`locales/ru.json` (and `locales/tg.json` — identical Russian values):

```json
  "auth.forgot.link": "Забыли пароль?",
  "auth.forgot.title": "Восстановление доступа",
  "auth.forgot.subtitle": "Выберите, как сбросить пароль.",
  "auth.forgot.channel.email": "По email",
  "auth.forgot.channel.phone": "По SMS",
  "auth.forgot.back": "Вернуться ко входу",
  "auth.forgot.email.field": "Логин или email",
  "auth.forgot.email.submit": "Отправить код",
  "auth.forgot.email.submitting": "Отправка…",
  "auth.forgot.email.sent": "Если аккаунт существует, мы отправили код для сброса на привязанную почту.",
  "auth.forgot.email.openReset": "Ввести код",
  "auth.forgot.email.error": "Не удалось отправить письмо. Попробуйте ещё раз.",
  "auth.forgot.phone.field": "Номер телефона",
  "auth.forgot.phone.submit": "Получить код",
  "auth.forgot.phone.submitting": "Отправка…",
  "auth.forgot.phone.sent": "Мы отправили код на ваш телефон.",
  "auth.forgot.phone.codeField": "Код из SMS",
  "auth.forgot.phone.newPassword": "Новый пароль",
  "auth.forgot.phone.reset": "Сменить пароль",
  "auth.forgot.phone.resetting": "Сохранение…",
  "auth.forgot.phone.done": "Пароль изменён. Войдите с новым паролем.",
  "auth.forgot.phone.toSignIn": "Перейти ко входу",
  "auth.forgot.phone.error.fields": "Введите код и новый пароль (не короче 8 символов).",
  "auth.forgot.phone.error.invalidPhone": "Проверьте номер телефона.",
  "auth.forgot.phone.error.invalidCode": "Неверный код.",
  "auth.forgot.phone.error.remaining": "Осталось попыток",
  "auth.forgot.phone.error.expired": "Срок действия кода истёк. Запросите новый.",
  "auth.forgot.phone.error.tooMany": "Слишком много попыток. Попробуйте позже.",
  "auth.forgot.phone.error.generic": "Не удалось сбросить пароль.",
  "auth.reset.subtitle": "Вставьте код из письма и задайте новый пароль.",
  "auth.reset.field.token": "Код из письма",
  "auth.reset.field.newPassword": "Новый пароль",
  "auth.reset.action.submit": "Сменить пароль",
  "auth.reset.action.submitting": "Сохранение…",
  "auth.reset.success": "Пароль изменён. Войдите с новым паролем.",
  "auth.reset.error.fields": "Введите код и новый пароль (не короче 8 символов).",
  "auth.reset.error.invalid": "Ссылка для сброса недействительна или устарела.",
  "auth.reset.toSignIn": "Перейти ко входу",
```

`locales/en.json`:

```json
  "auth.forgot.link": "Forgot password?",
  "auth.forgot.title": "Account recovery",
  "auth.forgot.subtitle": "Choose how to reset your password.",
  "auth.forgot.channel.email": "By email",
  "auth.forgot.channel.phone": "By SMS",
  "auth.forgot.back": "Back to sign in",
  "auth.forgot.email.field": "Login or email",
  "auth.forgot.email.submit": "Send code",
  "auth.forgot.email.submitting": "Sending…",
  "auth.forgot.email.sent": "If the account exists, we've emailed a reset code to the address on file.",
  "auth.forgot.email.openReset": "Enter the code",
  "auth.forgot.email.error": "Couldn't send the email. Try again.",
  "auth.forgot.phone.field": "Phone number",
  "auth.forgot.phone.submit": "Get a code",
  "auth.forgot.phone.submitting": "Sending…",
  "auth.forgot.phone.sent": "We've sent a code to your phone.",
  "auth.forgot.phone.codeField": "Code from SMS",
  "auth.forgot.phone.newPassword": "New password",
  "auth.forgot.phone.reset": "Change password",
  "auth.forgot.phone.resetting": "Saving…",
  "auth.forgot.phone.done": "Password changed. Sign in with your new password.",
  "auth.forgot.phone.toSignIn": "Go to sign in",
  "auth.forgot.phone.error.fields": "Enter the code and a new password (at least 8 characters).",
  "auth.forgot.phone.error.invalidPhone": "Check the phone number.",
  "auth.forgot.phone.error.invalidCode": "Wrong code.",
  "auth.forgot.phone.error.remaining": "Attempts left",
  "auth.forgot.phone.error.expired": "The code has expired. Request a new one.",
  "auth.forgot.phone.error.tooMany": "Too many attempts. Try again later.",
  "auth.forgot.phone.error.generic": "Couldn't reset the password.",
  "auth.reset.subtitle": "Paste the code from the email and set a new password.",
  "auth.reset.field.token": "Code from the email",
  "auth.reset.field.newPassword": "New password",
  "auth.reset.action.submit": "Change password",
  "auth.reset.action.submitting": "Saving…",
  "auth.reset.success": "Password changed. Sign in with your new password.",
  "auth.reset.error.fields": "Enter the code and a new password (at least 8 characters).",
  "auth.reset.error.invalid": "The reset link is invalid or has expired.",
  "auth.reset.toSignIn": "Go to sign in",
```

- [ ] **Step 3: Regenerate the message catalog**

Run: `cd packages/i18n && ~/.bun/bin/bun run gen`
Expected: `generated …/src/messages.ts from 3 locales`.

- [ ] **Step 4: Verify i18n tests pass**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS (messages + voice guard tests green — no CAPS-only values, no «компьютер»).

- [ ] **Step 5: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "feat(i18n): add staff password-reset and email-login strings"
```

### Task 5: `ForgotPassword` screen (channel-aware: email + SMS)

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ForgotPassword.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ForgotPassword.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ForgotPassword.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformApi';
import { ForgotPassword } from './ForgotPassword';

function fakeClient(overrides: Partial<{
  forgotPasswordByEmail: (login: string) => Promise<void>;
  forgotPasswordByPhone: (phone: string) => Promise<void>;
  resetPasswordByPhone: (phone: string, code: string, password: string) => Promise<void>;
}> = {}) {
  return {
    forgotPasswordByEmail: mock(overrides.forgotPasswordByEmail ?? (async () => {})),
    forgotPasswordByPhone: mock(overrides.forgotPasswordByPhone ?? (async () => {})),
    resetPasswordByPhone: mock(overrides.resetPasswordByPhone ?? (async () => {}))
  };
}

function renderScreen(client: ReturnType<typeof fakeClient>) {
  return render(
    <I18nProvider>
      <ForgotPassword client={client as never} onBackToSignIn={() => {}} onOpenReset={() => {}} />
    </I18nProvider>
  );
}

it('requests an email reset and shows the sent confirmation', async () => {
  const client = fakeClient();
  renderScreen(client);
  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'owner@demo.test' } });
  fireEvent.click(screen.getByRole('button', { name: 'Отправить код' }));
  await waitFor(() => expect(client.forgotPasswordByEmail).toHaveBeenCalledWith('owner@demo.test'));
  expect(await screen.findByText(/мы отправили код/i)).toBeInTheDocument();
});

it('runs the SMS reset flow: request code then set a new password', async () => {
  const client = fakeClient();
  renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
  fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  await waitFor(() => expect(client.forgotPasswordByPhone).toHaveBeenCalledWith('+992937380070'));

  fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '123456' } });
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  await waitFor(() => expect(client.resetPasswordByPhone)
    .toHaveBeenCalledWith('+992937380070', '123456', 'Passw0rd!New'));
  expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
});

it('shows remaining attempts on a bad SMS code', async () => {
  const client = fakeClient({
    resetPasswordByPhone: async () => { throw new PlatformApiError(400, 'invalid_code', 'invalid_code', 2); }
  });
  renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'По SMS' }));
  fireEvent.change(screen.getByLabelText('Номер телефона'), { target: { value: '+992937380070' } });
  fireEvent.click(screen.getByRole('button', { name: 'Получить код' }));
  fireEvent.change(await screen.findByLabelText('Код из SMS'), { target: { value: '000000' } });
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  expect(await screen.findByText(/Осталось попыток: 2/)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test src/components/ForgotPassword.test.tsx`
Expected: FAIL — cannot resolve `./ForgotPassword`.

- [ ] **Step 3: Implement the component**

Create `ForgotPassword.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import { PlatformApiError } from '../api/platformApi';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { useI18n, type MessageKey } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

type Channel = 'email' | 'phone';
type PhoneStep = 'request' | 'verify' | 'done';

export interface ForgotPasswordProps {
  client: Pick<StaffAuthApiClient, 'forgotPasswordByEmail' | 'forgotPasswordByPhone' | 'resetPasswordByPhone'>;
  onBackToSignIn: () => void;
  onOpenReset: () => void;
}

export function ForgotPassword({ client, onBackToSignIn, onOpenReset }: ForgotPasswordProps) {
  const { t } = useI18n();
  const [channel, setChannel] = useState<Channel>('email');
  const [emailLogin, setEmailLogin] = useState('');
  const [emailSent, setEmailSent] = useState(false);
  const [phone, setPhone] = useState('');
  const [phoneStep, setPhoneStep] = useState<PhoneStep>('request');
  const [code, setCode] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  function selectChannel(next: Channel) {
    setChannel(next);
    setError(null);
  }

  async function submitEmail(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (emailLogin.trim().length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.forgotPasswordByEmail(emailLogin.trim());
      setEmailSent(true);
    } catch {
      setError(t('auth.forgot.email.error'));
    } finally {
      setSubmitting(false);
    }
  }

  async function submitPhoneRequest(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (phone.trim().length === 0) {
      setError(t('auth.error.required'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.forgotPasswordByPhone(phone.trim());
      setPhoneStep('verify');
    } catch (cause) {
      setError(projectPhoneError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  async function submitPhoneReset(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (code.trim().length === 0 || newPassword.length < 8) {
      setError(t('auth.forgot.phone.error.fields'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.resetPasswordByPhone(phone.trim(), code.trim(), newPassword);
      setPhoneStep('done');
    } catch (cause) {
      setError(projectPhoneError(cause, t));
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.forgot.title')}</h1>
      <p className="muted">{t('auth.forgot.subtitle')}</p>

      <div className="actions" role="tablist">
        <button
          type="button"
          className={channel === 'email' ? 'primary' : ''}
          aria-pressed={channel === 'email'}
          onClick={() => selectChannel('email')}
        >
          {t('auth.forgot.channel.email')}
        </button>
        <button
          type="button"
          className={channel === 'phone' ? 'primary' : ''}
          aria-pressed={channel === 'phone'}
          onClick={() => selectChannel('phone')}
        >
          {t('auth.forgot.channel.phone')}
        </button>
      </div>

      <ErrorBanner message={error} onDismiss={() => setError(null)} />

      {channel === 'email' && (emailSent ? (
        <section className="section">
          <p>{t('auth.forgot.email.sent')}</p>
          <div className="actions actions-stack">
            <button type="button" className="primary" onClick={onOpenReset}>{t('auth.forgot.email.openReset')}</button>
            <button type="button" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
          </div>
        </section>
      ) : (
        <form className="form" onSubmit={submitEmail}>
          <Field label={t('auth.forgot.email.field')} htmlFor="forgot-email">
            <input
              id="forgot-email"
              type="text"
              autoComplete="username"
              value={emailLogin}
              onChange={(event) => setEmailLogin(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.forgot.email.submitting') : t('auth.forgot.email.submit')}
          </button>
        </form>
      ))}

      {channel === 'phone' && (phoneStep === 'done' ? (
        <section className="section">
          <p>{t('auth.forgot.phone.done')}</p>
          <button type="button" className="primary" onClick={onBackToSignIn}>{t('auth.forgot.phone.toSignIn')}</button>
        </section>
      ) : phoneStep === 'verify' ? (
        <form className="form" onSubmit={submitPhoneReset}>
          <p className="muted">{t('auth.forgot.phone.sent')}</p>
          <Field label={t('auth.forgot.phone.codeField')} htmlFor="forgot-code">
            <input
              id="forgot-code"
              type="text"
              inputMode="numeric"
              autoComplete="one-time-code"
              value={code}
              onChange={(event) => setCode(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <Field label={t('auth.forgot.phone.newPassword')} htmlFor="forgot-new-password">
            <input
              id="forgot-new-password"
              type="password"
              autoComplete="new-password"
              value={newPassword}
              onChange={(event) => setNewPassword(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.forgot.phone.resetting') : t('auth.forgot.phone.reset')}
          </button>
        </form>
      ) : (
        <form className="form" onSubmit={submitPhoneRequest}>
          <Field label={t('auth.forgot.phone.field')} htmlFor="forgot-phone">
            <input
              id="forgot-phone"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              value={phone}
              onChange={(event) => setPhone(event.target.value)}
              disabled={isSubmitting}
              required
            />
          </Field>
          <button type="submit" className="primary" disabled={isSubmitting}>
            {isSubmitting ? t('auth.forgot.phone.submitting') : t('auth.forgot.phone.submit')}
          </button>
        </form>
      ))}

      <button type="button" className="linklike" onClick={onBackToSignIn}>{t('auth.forgot.back')}</button>
    </div>
  );
}

function projectPhoneError(cause: unknown, t: (key: MessageKey) => string): string {
  if (cause instanceof PlatformApiError) {
    switch (cause.errorCode) {
      case 'invalid_phone':
        return t('auth.forgot.phone.error.invalidPhone');
      case 'invalid_code':
        return cause.remainingAttempts === null
          ? t('auth.forgot.phone.error.invalidCode')
          : `${t('auth.forgot.phone.error.invalidCode')} ${t('auth.forgot.phone.error.remaining')}: ${cause.remainingAttempts}`;
      case 'code_expired':
        return t('auth.forgot.phone.error.expired');
      case 'too_many_attempts':
        return t('auth.forgot.phone.error.tooMany');
      default:
        return t('auth.forgot.phone.error.generic');
    }
  }
  return t('auth.forgot.phone.error.generic');
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test src/components/ForgotPassword.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/components/ForgotPassword.tsx src/AFK4.Platform.Web/src/components/ForgotPassword.test.tsx
git commit -m "feat(platform-web): add channel-aware forgot-password screen"
```

### Task 6: `ResetPassword` screen (token from email)

**Files:**
- Create: `src/AFK4.Platform.Web/src/components/ResetPassword.tsx`
- Test: `src/AFK4.Platform.Web/src/components/ResetPassword.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `ResetPassword.test.tsx`:

```tsx
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { PlatformApiError } from '@/api/platformApi';
import { ResetPassword } from './ResetPassword';

function fakeClient(reset: (token: string, password: string) => Promise<void> = async () => {}) {
  return { resetPasswordByToken: mock(reset) };
}

function renderScreen(client: ReturnType<typeof fakeClient>, initialToken: string | null = null) {
  return render(
    <I18nProvider>
      <ResetPassword client={client as never} initialToken={initialToken} onBackToSignIn={() => {}} />
    </I18nProvider>
  );
}

it('prefills the token from the URL and completes the reset', async () => {
  const client = fakeClient();
  renderScreen(client, 'tok.en');
  expect((screen.getByLabelText('Код из письма') as HTMLInputElement).value).toBe('tok.en');
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  await waitFor(() => expect(client.resetPasswordByToken).toHaveBeenCalledWith('tok.en', 'Passw0rd!New'));
  expect(await screen.findByText(/Пароль изменён/)).toBeInTheDocument();
});

it('shows an invalid-link error when the token is rejected', async () => {
  const client = fakeClient(async () => { throw new PlatformApiError(400, 'invalid', 'invalid'); });
  renderScreen(client, 'bad-token');
  fireEvent.change(screen.getByLabelText('Новый пароль'), { target: { value: 'Passw0rd!New' } });
  fireEvent.click(screen.getByRole('button', { name: 'Сменить пароль' }));
  expect(await screen.findByText(/недействительна или устарела/)).toBeInTheDocument();
});
```

- [ ] **Step 2: Run the test to verify it fails**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test src/components/ResetPassword.test.tsx`
Expected: FAIL — cannot resolve `./ResetPassword`.

- [ ] **Step 3: Implement the component**

Create `ResetPassword.tsx`:

```tsx
import { useState, type FormEvent } from 'react';
import type { StaffAuthApiClient } from '../api/staffAuthApi';
import { useI18n } from '../i18n/I18nProvider';
import { ErrorBanner, Field } from './ui';

export interface ResetPasswordProps {
  client: Pick<StaffAuthApiClient, 'resetPasswordByToken'>;
  initialToken: string | null;
  onBackToSignIn: () => void;
}

export function ResetPassword({ client, initialToken, onBackToSignIn }: ResetPasswordProps) {
  const { t } = useI18n();
  const [token, setToken] = useState(initialToken ?? '');
  const [newPassword, setNewPassword] = useState('');
  const [done, setDone] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setSubmitting] = useState(false);

  async function handleSubmit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (token.trim().length === 0 || newPassword.length < 8) {
      setError(t('auth.reset.error.fields'));
      return;
    }
    setSubmitting(true);
    setError(null);
    try {
      await client.resetPasswordByToken(token.trim(), newPassword);
      setDone(true);
    } catch {
      setError(t('auth.reset.error.invalid'));
    } finally {
      setSubmitting(false);
    }
  }

  if (done) {
    return (
      <div className="page page-narrow">
        <h1>{t('auth.reset.title')}</h1>
        <section className="section">
          <p>{t('auth.reset.success')}</p>
          <button type="button" className="primary" onClick={onBackToSignIn}>{t('auth.reset.toSignIn')}</button>
        </section>
      </div>
    );
  }

  return (
    <div className="page page-narrow">
      <h1>{t('auth.reset.title')}</h1>
      <p className="muted">{t('auth.reset.subtitle')}</p>
      <form className="form" onSubmit={handleSubmit}>
        <ErrorBanner message={error} onDismiss={() => setError(null)} />
        <Field label={t('auth.reset.field.token')} htmlFor="reset-token">
          <input
            id="reset-token"
            type="text"
            value={token}
            onChange={(event) => setToken(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <Field label={t('auth.reset.field.newPassword')} htmlFor="reset-new-password">
          <input
            id="reset-new-password"
            type="password"
            autoComplete="new-password"
            value={newPassword}
            onChange={(event) => setNewPassword(event.target.value)}
            disabled={isSubmitting}
            required
          />
        </Field>
        <button type="submit" className="primary" disabled={isSubmitting}>
          {isSubmitting ? t('auth.reset.action.submitting') : t('auth.reset.action.submit')}
        </button>
      </form>
      <button type="button" className="linklike" onClick={onBackToSignIn}>{t('auth.reset.back')}</button>
    </div>
  );
}
```

- [ ] **Step 4: Run the test to verify it passes**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test src/components/ResetPassword.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Web/src/components/ResetPassword.tsx src/AFK4.Platform.Web/src/components/ResetPassword.test.tsx
git commit -m "feat(platform-web): add token-based reset-password screen"
```

### Task 7: Wire the screens into routing + sign-in link

**Files:**
- Modify: `src/AFK4.Platform.Web/src/components/StaffSignIn.tsx`
- Modify: `src/AFK4.Platform.Web/src/App.tsx`

- [ ] **Step 1: Add the "Forgot password?" link to `StaffSignIn`**

`StaffSignIn` already accepts `onOpenAcceptInvite`. Add a sibling prop `onOpenForgotPassword` and a link. In `StaffSignIn.tsx`, change the props interface:

```tsx
export interface StaffSignInProps {
  client: StaffAuthApiClient;
  onSignedIn: () => void;
  onOpenAcceptInvite: () => void;
  onOpenForgotPassword: () => void;
}
```

Update the function signature to destructure it: `export function StaffSignIn({ client, onSignedIn, onOpenAcceptInvite, onOpenForgotPassword }: StaffSignInProps) {`.

Add the link directly after the sign-in `</form>` (before the existing `auth.haveCode` button):

```tsx
      <button type="button" className="linklike" onClick={onOpenForgotPassword}>
        {t('auth.forgot.link')}
      </button>
```

- [ ] **Step 2: Carry the `?token=` value on the reset route**

In `App.tsx`, the `resetPassword` route is a member of the `AuthRoute` union declared at `App.tsx:50-54` (the member is on line 54). Change that member from `{ kind: 'resetPassword' }` to `{ kind: 'resetPassword'; token: string | null }`.

In `resolvePlatformRoute`, replace the reset-password branch (currently `return { route: { kind: 'resetPassword' } };` at the `/auth/reset-password` check) with:

```tsx
  if (path === '/auth/reset-password') {
    return { route: { kind: 'resetPassword', token: readQueryValue(search, 'token') } };
  }
```

- [ ] **Step 3: Render the new screens and pass the forgot link to `StaffSignIn`**

In `App.tsx`, add the imports near the other component imports:

```tsx
import { ForgotPassword } from './components/ForgotPassword';
import { ResetPassword } from './components/ResetPassword';
```

Add navigation callbacks next to `navigateToStaffSignIn` (mirroring the existing `navigateTo…` helpers):

```tsx
  const navigateToForgotPassword = useCallback(
    () => navigate({ kind: 'forgotPassword' }, '/auth/forgot-password'),
    [navigate]
  );
  const navigateToResetPassword = useCallback(
    () => navigate({ kind: 'resetPassword', token: null }, '/auth/reset-password'),
    [navigate]
  );
```

Replace the placeholder render block:

```tsx
  if (route.kind === 'forgotPassword' || route.kind === 'resetPassword') {
    return <ReservedAuthPage onSignIn={navigateToStaffSignIn} />;
  }
```

with:

```tsx
  if (route.kind === 'forgotPassword') {
    return (
      <ForgotPassword
        client={staffClient}
        onBackToSignIn={navigateToStaffSignIn}
        onOpenReset={navigateToResetPassword}
      />
    );
  }

  if (route.kind === 'resetPassword') {
    return (
      <ResetPassword
        client={staffClient}
        initialToken={route.token}
        onBackToSignIn={navigateToStaffSignIn}
      />
    );
  }
```

Pass the new prop everywhere `StaffSignIn` is rendered. There are two render sites (the `route.kind === 'staffSignIn'` branch and the `isClubRoute` unauthenticated branch); add `onOpenForgotPassword={navigateToForgotPassword}` to both:

```tsx
      <StaffSignIn
        client={staffClient}
        onSignedIn={navigateToClubDashboard}
        onOpenAcceptInvite={navigateToAcceptInvite}
        onOpenForgotPassword={navigateToForgotPassword}
      />
```

- [ ] **Step 4: Remove the dead `ReservedAuthPage`**

Delete the `ReservedAuthPage` function definition (the `function ReservedAuthPage({ onSignIn }…)` block). Confirm no references remain:

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun x tsc -b`
Expected: no errors (no remaining `ReservedAuthPage` or `auth.reset.message` references; the `resetPassword` route now requires `token`).

If `App.test.tsx` references `ReservedAuthPage`, `auth.reset.message`, or constructs a `resetPassword` route without a `token`, update those references (add `token: null`, assert on the new screen text instead of the placeholder).

- [ ] **Step 5: Run the Platform.Web suite to verify wiring**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test`
Expected: PASS — all tests green (including `App.test.tsx`).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Web/src/App.tsx src/AFK4.Platform.Web/src/components/StaffSignIn.tsx
git commit -m "feat(platform-web): wire forgot/reset screens and email-login label"
```

### Task 8: Full M1+M2 verification

**Files:** none (verification only)

- [ ] **Step 1: Backend suite**

Run: `dotnet test tests/AFK4.Platform.Api.Tests`
Expected: PASS — full suite green (≥1080 + the new email tests).

- [ ] **Step 2: i18n package**

Run: `cd packages/i18n && ~/.bun/bin/bun test`
Expected: PASS.

- [ ] **Step 3: Platform.Web tests + typecheck + build**

Run: `cd src/AFK4.Platform.Web && ~/.bun/bin/bun test && ~/.bun/bin/bun x tsc -b && ~/.bun/bin/bun run build`
Expected: tests PASS, `tsc -b` clean, `vite build` succeeds.

- [ ] **Step 4: Commit (only if any fixups were needed in Steps 1–3)**

```bash
git add -A
git commit -m "test(email-parity): green M1+M2 across backend, i18n, and platform-web"
```

---

## Self-review notes (for the executor)

- **Anti-enumeration is preserved.** `forgot-password` always returns 200; the email screen always shows the same "if the account exists…" confirmation. Login failures stay 401 with no account-existence hints.
- **No new backend contracts or migrations.** M1 only edits resolution logic; all reset DTOs/endpoints already exist.
- **Type consistency.** Client methods used in screens/tests: `forgotPasswordByEmail`, `forgotPasswordByPhone`, `resetPasswordByPhone`, `resetPasswordByToken` — defined in Task 3, consumed in Tasks 5–6. `PlatformApiError.errorCode` / `.remainingAttempts` are the real property names (verified in `platformApi.ts`). Backend error codes (`invalid_phone`, `invalid_code`, `code_expired`, `too_many_attempts`) match `AuthEndpoints.cs`.
- **Password rule parity.** FE enforces `newPassword.length < 8`, matching backend `ValidateStaffPassword` (min 8).
- **i18n source of truth** is `locales/*.json`; `messages.ts` is regenerated, never hand-edited.
```
