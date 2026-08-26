using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Tournaments;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Tournaments;

public sealed class EfTournamentService(PlatformDbContext dbContext, TimeProvider timeProvider) : ITournamentService
{
    public async Task<IReadOnlyList<TournamentDto>> ListForClubAsync(
        Guid organizationId, Guid branchId, CancellationToken ct)
    {
        var tournaments = await dbContext.Tournaments.AsNoTracking()
            .Where(tournament => tournament.OrganizationId == organizationId && tournament.BranchId == branchId)
            .OrderByDescending(tournament => tournament.StartsAtUtc)
            .ToListAsync(ct);

        var counts = await CountRegistrationsAsync(tournaments.Select(t => t.TournamentId).ToList(), ct);
        return tournaments.Select(tournament => Project(tournament, counts.GetValueOrDefault(tournament.TournamentId))).ToList();
    }

    public async Task<TournamentResult<TournamentDto>> CreateAsync(
        Guid organizationId, Guid actorStaffUserId, CreateTournamentRequest request, CancellationToken ct)
    {
        var validation = Validate(request.Title, request.StartsAtUtc, request.EntryFeeMinorUnits, request.Capacity);
        if (validation is not null) return TournamentResult<TournamentDto>.Refused(validation);

        var branchExists = await dbContext.Branches.AsNoTracking()
            .AnyAsync(branch => branch.BranchId == request.BranchId && branch.OrganizationId == organizationId, ct);
        if (!branchExists) return TournamentResult<TournamentDto>.Missing();

        var now = timeProvider.GetUtcNow();
        var tournament = new TournamentEntity
        {
            TournamentId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = request.BranchId,
            Title = request.Title.Trim(),
            Description = (request.Description ?? string.Empty).Trim(),
            Discipline = (request.Discipline ?? string.Empty).Trim(),
            StartsAtUtc = request.StartsAtUtc,
            EntryFeeMinorUnits = request.EntryFeeMinorUnits,
            CurrencyCode = await ResolveCurrencyAsync(request.BranchId, ct),
            Capacity = request.Capacity,
            // Событие заводится черновиком: пока клуб дописывает условия, игроку его видеть рано.
            State = TournamentStateNames.Draft,
            CreatedByStaffUserId = actorStaffUserId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        dbContext.Tournaments.Add(tournament);
        await dbContext.SaveChangesAsync(ct);
        return TournamentResult<TournamentDto>.Ok(Project(tournament, registeredCount: 0));
    }

    public async Task<TournamentResult<TournamentDto>> UpdateAsync(
        Guid organizationId, Guid tournamentId, UpdateTournamentRequest request, CancellationToken ct)
    {
        var tournament = await LoadAsync(organizationId, tournamentId, ct);
        if (tournament is null) return TournamentResult<TournamentDto>.Missing();
        if (tournament.State == TournamentStateNames.Cancelled)
        {
            return TournamentResult<TournamentDto>.Refused(TournamentRefusalCodes.Cancelled);
        }

        var title = request.Title?.Trim() ?? tournament.Title;
        var startsAt = request.StartsAtUtc ?? tournament.StartsAtUtc;
        var fee = request.EntryFeeMinorUnits ?? tournament.EntryFeeMinorUnits;
        var capacity = request.Capacity ?? tournament.Capacity;

        var validation = Validate(title, startsAt, fee, capacity);
        if (validation is not null) return TournamentResult<TournamentDto>.Refused(validation);

        var registeredCount = await CountRegisteredAsync(tournamentId, ct);
        // Опустить потолок ниже числа уже записавшихся значит выгнать кого-то задним числом.
        if (capacity > 0 && capacity < registeredCount)
        {
            return TournamentResult<TournamentDto>.Refused(TournamentRefusalCodes.Full);
        }

        tournament.Title = title;
        tournament.Description = request.Description?.Trim() ?? tournament.Description;
        tournament.Discipline = request.Discipline?.Trim() ?? tournament.Discipline;
        tournament.StartsAtUtc = startsAt;
        tournament.EntryFeeMinorUnits = fee;
        tournament.Capacity = capacity;
        tournament.UpdatedAtUtc = timeProvider.GetUtcNow();

        await dbContext.SaveChangesAsync(ct);
        return TournamentResult<TournamentDto>.Ok(Project(tournament, registeredCount));
    }

    public async Task<TournamentResult<TournamentDto>> PublishAsync(
        Guid organizationId, Guid tournamentId, CancellationToken ct)
    {
        var tournament = await LoadAsync(organizationId, tournamentId, ct);
        if (tournament is null) return TournamentResult<TournamentDto>.Missing();
        if (tournament.State == TournamentStateNames.Cancelled)
        {
            return TournamentResult<TournamentDto>.Refused(TournamentRefusalCodes.Cancelled);
        }
        // Опубликовать прошедший вечер значит позвать игроков во вчера.
        if (tournament.StartsAtUtc <= timeProvider.GetUtcNow())
        {
            return TournamentResult<TournamentDto>.Refused(TournamentRefusalCodes.AlreadyStarted);
        }

        tournament.State = TournamentStateNames.Published;
        tournament.UpdatedAtUtc = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(ct);
        return TournamentResult<TournamentDto>.Ok(Project(tournament, await CountRegisteredAsync(tournamentId, ct)));
    }

    /// <summary>
    /// Клуб отменяет событие. Взносы возвращаются всем записавшимся одним заходом: отменённое
    /// клубом событие — это не решение игрока, и удерживать с него деньги не за что.
    /// </summary>
    public async Task<TournamentResult<TournamentDto>> CancelAsync(
        Guid organizationId, Guid tournamentId, Guid actorStaffUserId, string reason, CancellationToken ct)
    {
        var tournament = await LoadAsync(organizationId, tournamentId, ct);
        if (tournament is null) return TournamentResult<TournamentDto>.Missing();
        if (tournament.State == TournamentStateNames.Cancelled)
        {
            return TournamentResult<TournamentDto>.Refused(TournamentRefusalCodes.Cancelled);
        }

        var now = timeProvider.GetUtcNow();
        var registrations = await dbContext.TournamentRegistrations
            .Where(registration => registration.TournamentId == tournamentId
                && registration.State == TournamentRegistrationStateNames.Registered)
            .ToListAsync(ct);

        foreach (var registration in registrations)
        {
            registration.State = TournamentRegistrationStateNames.Cancelled;
            registration.CancelledAtUtc = now;
            if (registration.EntryFeeMinorUnits > 0)
            {
                dbContext.LedgerEntries.Add(RefundEntry(tournament, registration, actorStaffUserId, now));
            }
        }

        tournament.State = TournamentStateNames.Cancelled;
        tournament.CancelReason = (reason ?? string.Empty).Trim();
        tournament.CancelledAtUtc = now;
        tournament.UpdatedAtUtc = now;

        await dbContext.SaveChangesAsync(ct);
        return TournamentResult<TournamentDto>.Ok(Project(tournament, registeredCount: 0));
    }

    public async Task<TournamentResult<IReadOnlyList<TournamentParticipantDto>>> ListParticipantsAsync(
        Guid organizationId, Guid tournamentId, CancellationToken ct)
    {
        var tournament = await LoadAsync(organizationId, tournamentId, ct);
        if (tournament is null)
        {
            return TournamentResult<IReadOnlyList<TournamentParticipantDto>>.Missing();
        }

        var participants = await (
            from registration in dbContext.TournamentRegistrations.AsNoTracking()
            join account in dbContext.PlayerAccounts.AsNoTracking()
                on registration.PlayerAccountId equals account.PlayerAccountId
            where registration.TournamentId == tournamentId
                && registration.State == TournamentRegistrationStateNames.Registered
            orderby registration.RegisteredAtUtc
            select new TournamentParticipantDto(
                registration.TournamentRegistrationId,
                registration.PlayerAccountId,
                account.DisplayName,
                account.PhoneNumber,
                new MoneyDto(registration.CurrencyCode, registration.EntryFeeMinorUnits),
                registration.RegisteredAtUtc)).ToListAsync(ct);

        return TournamentResult<IReadOnlyList<TournamentParticipantDto>>.Ok(participants);
    }

    public async Task<IReadOnlyList<PlayerTournamentDto>> ListForPlayerAsync(
        Guid organizationId, Guid branchId, Guid playerAccountId, CancellationToken ct)
    {
        var now = timeProvider.GetUtcNow();
        // Игрок видит опубликованные события, которые ещё не начались, и — отдельно — те, на
        // которые записан: отменённое клубом событие обязано доехать до того, кто на него шёл.
        var mine = await dbContext.TournamentRegistrations.AsNoTracking()
            .Where(registration => registration.PlayerAccountId == playerAccountId
                && registration.State == TournamentRegistrationStateNames.Registered)
            .Select(registration => registration.TournamentId)
            .ToListAsync(ct);

        // Отмена клубом снимает и записи — значит «мои» по живой записи уже пусты. Чтобы весть
        // об отмене дошла до того, кто собирался, отменённые события ищем по всем его записям.
        var everMine = await dbContext.TournamentRegistrations.AsNoTracking()
            .Where(registration => registration.PlayerAccountId == playerAccountId)
            .Select(registration => registration.TournamentId)
            .ToListAsync(ct);

        var tournaments = await dbContext.Tournaments.AsNoTracking()
            .Where(tournament => tournament.OrganizationId == organizationId
                && tournament.BranchId == branchId
                && tournament.StartsAtUtc > now
                && (tournament.State == TournamentStateNames.Published
                    || (tournament.State == TournamentStateNames.Cancelled
                        && everMine.Contains(tournament.TournamentId))))
            .OrderBy(tournament => tournament.StartsAtUtc)
            .ToListAsync(ct);

        var branchName = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.BranchId == branchId)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;

        var counts = await CountRegistrationsAsync(tournaments.Select(t => t.TournamentId).ToList(), ct);

        return tournaments.Select(tournament => new PlayerTournamentDto(
            tournament.TournamentId,
            tournament.BranchId,
            branchName,
            tournament.Title,
            tournament.Description,
            tournament.Discipline,
            tournament.StartsAtUtc,
            new MoneyDto(tournament.CurrencyCode, tournament.EntryFeeMinorUnits),
            tournament.Capacity,
            counts.GetValueOrDefault(tournament.TournamentId),
            mine.Contains(tournament.TournamentId),
            tournament.State,
            tournament.CancelReason)).ToList();
    }

    public async Task<TournamentResult<PlayerTournamentDto>> RegisterAsync(
        Guid organizationId, Guid playerAccountId, Guid tournamentId, CancellationToken ct)
    {
        var tournament = await LoadAsync(organizationId, tournamentId, ct);
        if (tournament is null) return TournamentResult<PlayerTournamentDto>.Missing();

        if (tournament.State == TournamentStateNames.Cancelled)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.Cancelled);
        }
        if (tournament.State != TournamentStateNames.Published)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.NotPublished);
        }

        var now = timeProvider.GetUtcNow();
        if (tournament.StartsAtUtc <= now)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.AlreadyStarted);
        }

        var alreadyRegistered = await dbContext.TournamentRegistrations.AsNoTracking()
            .AnyAsync(registration => registration.TournamentId == tournamentId
                && registration.PlayerAccountId == playerAccountId
                && registration.State == TournamentRegistrationStateNames.Registered, ct);
        if (alreadyRegistered)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.AlreadyRegistered);
        }

        var registeredCount = await CountRegisteredAsync(tournamentId, ct);
        if (tournament.Capacity > 0 && registeredCount >= tournament.Capacity)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.Full);
        }

        if (tournament.EntryFeeMinorUnits > 0)
        {
            var wallet = await LedgerBalanceProjector.GetWalletSummaryAsync(dbContext, playerAccountId, ct);
            var available = wallet?.WalletBalance.MinorUnits ?? 0;
            if (available < tournament.EntryFeeMinorUnits)
            {
                return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.InsufficientFunds);
            }
        }

        var registration = new TournamentRegistrationEntity
        {
            TournamentRegistrationId = Guid.NewGuid(),
            TournamentId = tournamentId,
            OrganizationId = organizationId,
            PlayerAccountId = playerAccountId,
            State = TournamentRegistrationStateNames.Registered,
            // Взнос запоминается на записи: клуб может поменять его завтра, а заплачено вчерашним.
            EntryFeeMinorUnits = tournament.EntryFeeMinorUnits,
            CurrencyCode = tournament.CurrencyCode,
            RegisteredAtUtc = now
        };
        dbContext.TournamentRegistrations.Add(registration);

        if (tournament.EntryFeeMinorUnits > 0)
        {
            dbContext.LedgerEntries.Add(BillingEntryFactory.Create(
                organizationId,
                tournament.BranchId,
                playerAccountId,
                sessionId: null,
                playerPackageId: null,
                LedgerEntryTypeNames.TournamentEntryFee,
                LedgerAccountTypeNames.Wallet,
                -tournament.EntryFeeMinorUnits,
                quantitySeconds: 0,
                tournament.CurrencyCode,
                LedgerEntryTypeNames.TournamentEntryFee,
                tournament.Title,
                reversesLedgerEntryId: null,
                // Взнос списывает сам игрок из приложения — сотрудника за этой строкой нет.
                actorStaffUserId: Guid.Empty,
                now));
        }

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // Уникальный индекс на живой записи: два нажатия «Записаться» подряд пришли быстрее,
            // чем первое успело закоммититься. Второе — не ошибка, а та же запись: взнос списан
            // один раз, и человек в списке.
            dbContext.ChangeTracker.Clear();
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.AlreadyRegistered);
        }

        return TournamentResult<PlayerTournamentDto>.Ok(
            await ProjectForPlayerAsync(tournament, playerAccountId, ct));
    }

    /// <summary>
    /// Игрок снимается сам. До начала — полный возврат: место освободилось заранее, и клуб
    /// успевает продать его другому. После начала снятия уже нет — событие идёт.
    /// </summary>
    public async Task<TournamentResult<PlayerTournamentDto>> CancelRegistrationAsync(
        Guid organizationId, Guid playerAccountId, Guid tournamentId, CancellationToken ct)
    {
        var tournament = await LoadAsync(organizationId, tournamentId, ct);
        if (tournament is null) return TournamentResult<PlayerTournamentDto>.Missing();

        var registration = await dbContext.TournamentRegistrations
            .Where(candidate => candidate.TournamentId == tournamentId
                && candidate.PlayerAccountId == playerAccountId
                && candidate.State == TournamentRegistrationStateNames.Registered)
            .FirstOrDefaultAsync(ct);
        if (registration is null)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.NotRegistered);
        }

        var now = timeProvider.GetUtcNow();
        if (tournament.StartsAtUtc <= now)
        {
            return TournamentResult<PlayerTournamentDto>.Refused(TournamentRefusalCodes.AlreadyStarted);
        }

        registration.State = TournamentRegistrationStateNames.Cancelled;
        registration.CancelledAtUtc = now;
        if (registration.EntryFeeMinorUnits > 0)
        {
            dbContext.LedgerEntries.Add(RefundEntry(tournament, registration, actorStaffUserId: Guid.Empty, now));
        }

        await dbContext.SaveChangesAsync(ct);
        return TournamentResult<PlayerTournamentDto>.Ok(
            await ProjectForPlayerAsync(tournament, playerAccountId, ct));
    }

    private static LedgerEntryEntity RefundEntry(
        TournamentEntity tournament,
        TournamentRegistrationEntity registration,
        Guid actorStaffUserId,
        DateTimeOffset now) =>
        BillingEntryFactory.Create(
            tournament.OrganizationId,
            tournament.BranchId,
            registration.PlayerAccountId,
            sessionId: null,
            playerPackageId: null,
            LedgerEntryTypeNames.TournamentEntryRefund,
            LedgerAccountTypeNames.Wallet,
            registration.EntryFeeMinorUnits,
            quantitySeconds: 0,
            registration.CurrencyCode,
            LedgerEntryTypeNames.TournamentEntryRefund,
            tournament.Title,
            reversesLedgerEntryId: null,
            actorStaffUserId,
            now);

    private Task<TournamentEntity?> LoadAsync(Guid organizationId, Guid tournamentId, CancellationToken ct) =>
        dbContext.Tournaments
            .Where(tournament => tournament.TournamentId == tournamentId
                && tournament.OrganizationId == organizationId)
            .FirstOrDefaultAsync(ct);

    private async Task<PlayerTournamentDto> ProjectForPlayerAsync(
        TournamentEntity tournament, Guid playerAccountId, CancellationToken ct)
    {
        var branchName = await dbContext.Branches.AsNoTracking()
            .Where(branch => branch.BranchId == tournament.BranchId)
            .Select(branch => branch.Name)
            .FirstOrDefaultAsync(ct) ?? string.Empty;
        var isRegistered = await dbContext.TournamentRegistrations.AsNoTracking()
            .AnyAsync(registration => registration.TournamentId == tournament.TournamentId
                && registration.PlayerAccountId == playerAccountId
                && registration.State == TournamentRegistrationStateNames.Registered, ct);

        return new PlayerTournamentDto(
            tournament.TournamentId,
            tournament.BranchId,
            branchName,
            tournament.Title,
            tournament.Description,
            tournament.Discipline,
            tournament.StartsAtUtc,
            new MoneyDto(tournament.CurrencyCode, tournament.EntryFeeMinorUnits),
            tournament.Capacity,
            await CountRegisteredAsync(tournament.TournamentId, ct),
            isRegistered,
            tournament.State,
            tournament.CancelReason);
    }

    private Task<int> CountRegisteredAsync(Guid tournamentId, CancellationToken ct) =>
        dbContext.TournamentRegistrations.AsNoTracking()
            .CountAsync(registration => registration.TournamentId == tournamentId
                && registration.State == TournamentRegistrationStateNames.Registered, ct);

    private async Task<Dictionary<Guid, int>> CountRegistrationsAsync(
        IReadOnlyList<Guid> tournamentIds, CancellationToken ct)
    {
        if (tournamentIds.Count == 0) return [];

        var counted = await dbContext.TournamentRegistrations.AsNoTracking()
            .Where(registration => tournamentIds.Contains(registration.TournamentId)
                && registration.State == TournamentRegistrationStateNames.Registered)
            .GroupBy(registration => registration.TournamentId)
            .Select(group => new { TournamentId = group.Key, Count = group.Count() })
            .ToListAsync(ct);

        return counted.ToDictionary(row => row.TournamentId, row => row.Count);
    }

    /// <summary>Валюта клуба — та же, в которой считают час. «TJS», когда тарифов ещё нет.</summary>
    private async Task<string> ResolveCurrencyAsync(Guid branchId, CancellationToken ct)
    {
        var fromTariff = await dbContext.TariffVersions.AsNoTracking()
            .Where(version => version.BranchId == branchId)
            .Select(version => version.CurrencyCode)
            .FirstOrDefaultAsync(ct);
        return string.IsNullOrWhiteSpace(fromTariff) ? "TJS" : fromTariff;
    }

    private static string? Validate(string title, DateTimeOffset startsAtUtc, long entryFee, int capacity)
    {
        if (string.IsNullOrWhiteSpace(title)) return "title_required";
        if (entryFee < 0) return "entry_fee_negative";
        if (capacity < 0) return "capacity_negative";
        return null;
    }

    private static TournamentDto Project(TournamentEntity tournament, int registeredCount) =>
        new(tournament.TournamentId,
            tournament.BranchId,
            tournament.Title,
            tournament.Description,
            tournament.Discipline,
            tournament.StartsAtUtc,
            new MoneyDto(tournament.CurrencyCode, tournament.EntryFeeMinorUnits),
            tournament.Capacity,
            tournament.State,
            registeredCount,
            tournament.CreatedAtUtc,
            tournament.UpdatedAtUtc,
            tournament.CancelledAtUtc,
            tournament.CancelReason);
}
