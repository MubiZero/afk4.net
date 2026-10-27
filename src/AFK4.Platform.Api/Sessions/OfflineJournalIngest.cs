using System.Text.Json;
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Billing;
using AFK4.Shared.Contracts.Sessions;
using AFK4.Shared.Contracts.Shifts;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Sessions;

/// <summary>
/// Приём журнала кассы, который Панель вела без интернета (docs/roadmap/offline-front-desk.md).
///
/// Не повтор онлайн-команд: те ставят «сейчас» вместо времени операции, кладут деньги в открытую сейчас
/// смену, пересчитывают цену и шлют ПК команды — а ПК всё это уже прожил по офлайн-аренде. Здесь
/// пишутся те же строки, что пишет посадка онлайн, но со временем, сменой и суммой из журнала, и без
/// команд ПК. Идущая сессия получит серверную аренду с обычным сердцебиением: номер у неё тот же.
/// </summary>
public sealed class OfflineJournalIngest(
    PlatformDbContext db,
    ISessionBillingService billing,
    IAuditRecordWriter audit,
    TimeProvider clock)
{
    public const int MaxRecordsPerUpload = 500;

    /// <summary>Дольше офлайн-аренда не бывает — дольше не бывает и одной офлайн-посадки или продления.</summary>
    private const int MaxMinutes = 240;

    private static readonly string[] LiveStates = [SessionStateNames.Active, SessionStateNames.Paused, SessionStateNames.Ending];
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<OfflineJournalUploadResponse> IngestAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, IReadOnlyList<OfflineJournalRecordDto> records, CancellationToken cancellationToken)
    {
        var results = new List<OfflineJournalRecordResultDto>(records.Count);
        foreach (var record in records)
        {
            results.Add(await IngestOneAsync(organizationId, branchId, staffUserId, record, cancellationToken));
        }

        return new OfflineJournalUploadResponse(results);
    }

    private async Task<OfflineJournalRecordResultDto> IngestOneAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, CancellationToken cancellationToken)
    {
        var seen = await db.OfflineJournalRecords.AsNoTracking()
            .Where(row => row.BranchId == branchId && row.RecordId == record.Id)
            .Select(row => new { row.Status })
            .SingleOrDefaultAsync(cancellationToken);
        if (seen is not null)
        {
            return new OfflineJournalRecordResultDto(record.Id, OfflineJournalStatusNames.Duplicate, null);
        }

        var rejection = await ApplyAsync(organizationId, branchId, staffUserId, record, cancellationToken);
        db.OfflineJournalRecords.Add(new OfflineJournalRecordEntity
        {
            OfflineJournalRecordId = Guid.NewGuid(),
            OrganizationId = organizationId,
            BranchId = branchId,
            RecordId = record.Id,
            Kind = record.Kind,
            SessionId = record.SessionId,
            OccurredAtUtc = record.OccurredAtUtc,
            PayloadJson = JsonSerializer.Serialize(record, Json),
            Status = rejection is null ? OfflineJournalStatusNames.Applied : OfflineJournalStatusNames.Rejected,
            RejectionCode = rejection,
            UploadedByStaffUserId = staffUserId,
            ReceivedAtUtc = clock.GetUtcNow()
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Две отправки одного журнала встретились: запись приняла первая. Иначе — настоящая ошибка.
            if (!await IsDuplicateAsync(branchId, record.Id, cancellationToken))
            {
                throw;
            }

            return new OfflineJournalRecordResultDto(record.Id, OfflineJournalStatusNames.Duplicate, null);
        }

        if (rejection is null)
        {
            await WriteAuditAsync(organizationId, branchId, staffUserId, record, cancellationToken);
        }

        return new OfflineJournalRecordResultDto(
            record.Id,
            rejection is null ? OfflineJournalStatusNames.Applied : OfflineJournalStatusNames.Rejected,
            rejection);
    }

    /// <returns>null — применено; иначе код отказа. Изменения остаются в контексте до общего сохранения.</returns>
    private async Task<string?> ApplyAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(record.Id) || record.Id.Length > 64 || record.SessionId == Guid.Empty)
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        return record.Kind switch
        {
            OfflineJournalKindNames.SessionStarted => await StartAsync(organizationId, branchId, staffUserId, record, cancellationToken),
            OfflineJournalKindNames.SessionExtended => await ExtendAsync(organizationId, branchId, staffUserId, record, cancellationToken),
            OfflineJournalKindNames.SessionEnded => await EndAsync(organizationId, branchId, staffUserId, record, cancellationToken),
            _ => OfflineJournalRejectionNames.Invalid
        };
    }

    private async Task<string?> StartAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, CancellationToken cancellationToken)
    {
        if (record.Minutes is < 1 or > MaxMinutes
            || record.ChargeMinorUnits < 0
            || string.IsNullOrWhiteSpace(record.CurrencyCode)
            || string.IsNullOrWhiteSpace(record.TariffRuleVersionId)
            || record.SeatId is not { } seatId
            || record.DeviceId is not { } deviceId)
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        if (!await db.Seats.AnyAsync(seat => seat.SeatId == seatId && seat.BranchId == branchId && seat.OrganizationId == organizationId, cancellationToken)
            || !await db.Devices.AnyAsync(device => device.DeviceId == deviceId && device.BranchId == branchId && device.OrganizationId == organizationId, cancellationToken)
            || await db.Sessions.AnyAsync(session => session.SessionId == record.SessionId, cancellationToken))
        {
            return OfflineJournalRejectionNames.UnknownTarget;
        }

        var shiftRefusal = await ShiftRefusalAsync(organizationId, branchId, record.ShiftId, cancellationToken);
        if (shiftRefusal is not null)
        {
            return shiftRefusal;
        }

        var now = clock.GetUtcNow();
        var session = new SessionEntity
        {
            SessionId = record.SessionId,
            OrganizationId = organizationId,
            BranchId = branchId,
            SeatId = seatId,
            DeviceId = deviceId,
            CreatedByStaffUserId = staffUserId,
            PlayerKind = "guest",
            TariffRuleVersionId = record.TariffRuleVersionId,
            BillingMode = BillingModeNames.PrepaidCash,
            Origin = SessionOriginNames.Offline,
            RequestedAtUtc = record.OccurredAtUtc,
            StartedAtUtc = record.OccurredAtUtc,
            EndsAtUtc = record.OccurredAtUtc.AddMinutes(record.Minutes),
            UpdatedAtUtc = now,
            Version = 1
        };
        await SettleByTimeAsync(session, cancellationToken);
        db.Sessions.Add(session);
        billing.AppendOfflineGuestCashCharge(session, staffUserId, record.ChargeMinorUnits, record.CurrencyCode, record.ShiftId, record.OccurredAtUtc);
        AddEvent(session, "session-started", staffUserId, record.OccurredAtUtc, record);
        return null;
    }

    private async Task<string?> ExtendAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, CancellationToken cancellationToken)
    {
        if (record.Minutes is < 1 or > MaxMinutes || record.ChargeMinorUnits < 0 || string.IsNullOrWhiteSpace(record.CurrencyCode))
        {
            return OfflineJournalRejectionNames.Invalid;
        }

        var session = await OfflineSessionAsync(organizationId, branchId, record.SessionId, cancellationToken);
        if (session is null)
        {
            return OfflineJournalRejectionNames.UnknownTarget;
        }

        var shiftRefusal = await ShiftRefusalAsync(organizationId, branchId, record.ShiftId, cancellationToken);
        if (shiftRefusal is not null)
        {
            return shiftRefusal;
        }

        // Кончилась ли сессия по времени (а не по кнопке «закончить»): тогда продление из журнала
        // её продолжает — продлили до того, как время вышло, просто сервер узнал об этом позже.
        var endedByTime = session.State == SessionStateNames.Ended && session.EndedAtUtc >= session.EndsAtUtc;
        session.EndsAtUtc = (session.EndsAtUtc ?? record.OccurredAtUtc).AddMinutes(record.Minutes);
        if (endedByTime || session.State != SessionStateNames.Ended)
        {
            await SettleByTimeAsync(session, cancellationToken);
        }

        session.UpdatedAtUtc = clock.GetUtcNow();
        session.Version++;
        billing.AppendOfflineGuestCashCharge(session, staffUserId, record.ChargeMinorUnits, record.CurrencyCode, record.ShiftId, record.OccurredAtUtc);
        AddEvent(session, "session-extended", staffUserId, record.OccurredAtUtc, record);
        return null;
    }

    private async Task<string?> EndAsync(
        Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, CancellationToken cancellationToken)
    {
        var session = await OfflineSessionAsync(organizationId, branchId, record.SessionId, cancellationToken);
        if (session is null)
        {
            return OfflineJournalRejectionNames.UnknownTarget;
        }

        // Закончили раньше, чем вышло время: конец — по кнопке, а не по часам.
        if (session.State != SessionStateNames.Ended || session.EndedAtUtc > record.OccurredAtUtc)
        {
            session.State = SessionStateNames.Ended;
            session.EndedAtUtc = record.OccurredAtUtc;
            session.UpdatedAtUtc = clock.GetUtcNow();
            session.Version++;
            AddEvent(session, "session-ended", staffUserId, record.OccurredAtUtc, record);
        }

        return null;
    }

    /// <summary>
    /// Идёт ли офлайн-сессия сейчас, по времени: оплаченное ещё не вышло и за ПК не сел следующий гость
    /// (связь вернулась раньше, чем журнал). Иначе она окончена тогда, когда вышло время, — деньги за неё
    /// всё равно ложатся в кассу: их взяли.
    /// </summary>
    private async Task SettleByTimeAsync(SessionEntity session, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var seatTaken = await db.Sessions.AnyAsync(
            other => other.SessionId != session.SessionId
                && (other.SeatId == session.SeatId || other.DeviceId == session.DeviceId)
                && LiveStates.Contains(other.State),
            cancellationToken);
        var endsAt = session.EndsAtUtc!.Value;
        if (!seatTaken && endsAt > now)
        {
            session.State = SessionStateNames.Active;
            session.EndedAtUtc = null;
            return;
        }

        session.State = SessionStateNames.Ended;
        session.EndedAtUtc = endsAt < now ? endsAt : now;
    }

    private Task<SessionEntity?> OfflineSessionAsync(Guid organizationId, Guid branchId, Guid sessionId, CancellationToken cancellationToken) =>
        db.Sessions.SingleOrDefaultAsync(
            session => session.SessionId == sessionId
                && session.OrganizationId == organizationId
                && session.BranchId == branchId
                && session.Origin == SessionOriginNames.Offline,
            cancellationToken);

    private async Task<string?> ShiftRefusalAsync(Guid organizationId, Guid branchId, Guid shiftId, CancellationToken cancellationToken)
    {
        var state = await db.Shifts.AsNoTracking()
            .Where(shift => shift.ShiftId == shiftId && shift.BranchId == branchId && shift.OrganizationId == organizationId)
            .Select(shift => shift.State)
            .SingleOrDefaultAsync(cancellationToken);
        return state switch
        {
            null => OfflineJournalRejectionNames.UnknownTarget,
            ShiftStateNames.Open => null,
            _ => OfflineJournalRejectionNames.ShiftClosed
        };
    }

    private void AddEvent(SessionEntity session, string type, Guid staffUserId, DateTimeOffset at, OfflineJournalRecordDto record) =>
        db.SessionEvents.Add(new SessionEventEntity
        {
            SessionEventId = Guid.NewGuid(),
            SessionId = session.SessionId,
            OrganizationId = session.OrganizationId,
            BranchId = session.BranchId,
            EventType = type,
            ActorStaffUserId = staffUserId,
            DeviceId = session.DeviceId,
            CreatedAtUtc = at,
            DetailsJson = JsonSerializer.Serialize(new { offline = true, recordId = record.Id, record.Minutes, record.ChargeMinorUnits }, Json)
        });

    private Task<bool> IsDuplicateAsync(Guid branchId, string recordId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return db.OfflineJournalRecords.AnyAsync(row => row.BranchId == branchId && row.RecordId == recordId, cancellationToken);
    }

    private Task WriteAuditAsync(Guid organizationId, Guid branchId, Guid staffUserId, OfflineJournalRecordDto record, CancellationToken cancellationToken) =>
        audit.WriteAsync(new AuditRecordWriteRequest(
            organizationId,
            branchId,
            staffUserId,
            record.Kind switch
            {
                OfflineJournalKindNames.SessionStarted => AuditActionNames.StartSession,
                OfflineJournalKindNames.SessionExtended => AuditActionNames.ExtendSession,
                _ => AuditActionNames.EndSession
            },
            "Session",
            record.SessionId.ToString("D"),
            AuditOutcome.Succeeded,
            "PlatformApi",
            JsonSerializer.Serialize(new { offline = true, recordId = record.Id, record.OccurredAtUtc, record.Minutes, record.ChargeMinorUnits }, Json)),
            cancellationToken);
}
