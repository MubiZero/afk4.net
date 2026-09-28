using AFK4.Platform.Api.Data;

namespace AFK4.Platform.Api.Notifications;

public sealed record NotificationOutboxAddResult(NotificationOutboxEntity Row, bool Created);
