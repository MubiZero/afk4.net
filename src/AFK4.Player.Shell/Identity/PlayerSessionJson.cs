using System.Text.Json;
using System.Text.Json.Serialization;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Players;

namespace AFK4.Player.Shell.Identity;

/// <summary>JSON обновления и выхода игрока без отражения: Native AOT (docs/roadmap/windows7.md).</summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(PlatformPersonSessionResponse))]
[JsonSerializable(typeof(PlayerRefreshRequest))]
[JsonSerializable(typeof(PlayerSignOutRequest))]
internal sealed partial class PlayerSessionJson : JsonSerializerContext;
