using System.Text.Json;
using System.Text.Json.Serialization;

namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// JSON моста хост ↔ страница оболочки без отражения: в Native AOT его нет (docs/roadmap/windows7.md). Пустые поля
/// в ответ не пишутся, как у страницы и ждёт её код.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(ShellSnapshotDto))]
[JsonSerializable(typeof(ShellAuthStateDto))]
[JsonSerializable(typeof(ShellSystemStateDto))]
[JsonSerializable(typeof(PlayerShellStateDto))]
public sealed partial class ShellBridgeJson : JsonSerializerContext;
