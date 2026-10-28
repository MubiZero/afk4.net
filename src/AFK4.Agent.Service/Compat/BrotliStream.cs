#if NETFRAMEWORK
namespace System.IO.Compression;

/// <summary>
/// Части дельта-обновлений сжаты Brotli (docs/roadmap/delta-updates.md), а в .NET Framework 4.8 его нет:
/// распаковывает управляемый порт декодера Google (BrotliSharpLib, MIT).
/// </summary>
internal sealed class BrotliStream(Stream stream, CompressionMode mode) : BrotliSharpLib.BrotliStream(stream, mode);
#endif
