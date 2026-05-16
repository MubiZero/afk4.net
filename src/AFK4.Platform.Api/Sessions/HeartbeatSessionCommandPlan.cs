using AFK4.Platform.Api.Devices;

namespace AFK4.Platform.Api.Sessions;

public sealed record HeartbeatSessionCommandPlan(
    Guid DeviceId,
    CreateDeviceCommandRequest Command);
