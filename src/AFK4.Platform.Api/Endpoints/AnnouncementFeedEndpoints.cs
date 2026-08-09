using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Platform.Announcements;

namespace AFK4.Platform.Api.Endpoints;

/// <summary>
/// Сообщения платформы глазами клуба. Отдельного права нет намеренно: анонс адресован всем, кто
/// работает в клубе, а не только тем, кто что-то настраивает. Требуется ровно одно — быть
/// сотрудником этого клуба.
/// </summary>
internal static class AnnouncementFeedEndpoints
{
    public static void MapAnnouncementFeedEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("announcements", async (
            IStaffContextAccessor staffContextAccessor,
            AnnouncementFeedService feedService,
            CancellationToken cancellationToken) =>
        {
            var staff = staffContextAccessor.Current;
            if (staff is null) return Results.Unauthorized();

            // Организация берётся из контекста сотрудника, а не из адреса: иначе подставленный
            // чужой id отдал бы клубу сообщения, которые ему не адресованы.
            var items = await feedService.ListAsync(staff.OrganizationId, staff.StaffUserId, cancellationToken);
            return Results.Ok(items);
        });

        app.MapPost("announcements/{announcementId:guid}/read", async (
            Guid announcementId,
            IStaffContextAccessor staffContextAccessor,
            AnnouncementFeedService feedService,
            CancellationToken cancellationToken) =>
        {
            var staff = staffContextAccessor.Current;
            if (staff is null) return Results.Unauthorized();

            var marked = await feedService.MarkReadAsync(
                staff.OrganizationId, staff.StaffUserId, announcementId, cancellationToken);

            return marked ? Results.NoContent() : Results.NotFound();
        });
    }
}
