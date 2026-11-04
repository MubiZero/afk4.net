using System.Text.Json;
using System.Text.Json.Serialization;

namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// JSON кадров канала без отражения: в Native AOT его нет, а кодек общий для агента и хоста
/// (docs/roadmap/windows7.md).
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ShellPipeMessage))]
public sealed partial class ShellPipeJson : JsonSerializerContext;
