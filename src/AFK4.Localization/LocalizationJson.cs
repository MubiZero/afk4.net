#if NET
using System.Text.Json.Serialization;

namespace AFK4.Localization;

/// <summary>Каталоги строк читаются без отражения: в Native AOT его нет (docs/roadmap/windows7.md).</summary>
[JsonSerializable(typeof(Dictionary<string, string>))]
internal sealed partial class LocalizationJson : JsonSerializerContext;
#endif
