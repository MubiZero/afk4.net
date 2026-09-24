using System.Buffers.Binary;
using System.Text.Json;

namespace AFK4.Shared.Contracts.Shell;

/// <summary>
/// Кадр канала: 4 байта длины (little-endian) и UTF-8 JSON. Одним кодеком пишут и читают обе
/// стороны — агент и хост, — иначе две реализации однажды разошлись бы в мелочи вроде порядка
/// байтов.
/// </summary>
public static class ShellPipeCodec
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Записать кадр. Без <c>Flush</c>: на канале Windows это <c>FlushFileBuffers</c>, и он ждёт,
    /// пока собеседник всё вычитает (#266). На постоянном соединении ждать незачем — данные уже
    /// в буфере канала, а зависшая запись заперла бы и все следующие сообщения.
    /// </summary>
    public static async Task WriteAsync(Stream stream, ShellPipeMessage message, CancellationToken cancellationToken)
    {
        var body = JsonSerializer.SerializeToUtf8Bytes(message, JsonOptions);
        if (body.Length > ShellPipeProtocol.MaxFrameBytes)
        {
            throw new InvalidDataException(
                $"Shell pipe message of {body.Length} bytes exceeds the {ShellPipeProtocol.MaxFrameBytes}-byte limit.");
        }

        var frame = new byte[sizeof(int) + body.Length];
        BinaryPrimitives.WriteInt32LittleEndian(frame, body.Length);
        body.CopyTo(frame, sizeof(int));
        await stream.WriteAsync(frame, cancellationToken);
    }

    /// <summary>
    /// Прочитать кадр. <c>null</c> — собеседник закрыл канал между кадрами, это штатный конец.
    /// Обрыв посреди кадра — <see cref="EndOfStreamException"/>: такое сообщение не собрать.
    /// </summary>
    public static async Task<ShellPipeMessage?> ReadAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[sizeof(int)];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken);
        if (read == 0)
        {
            return null;
        }

        if (read < header.Length)
        {
            throw new EndOfStreamException("Shell pipe closed in the middle of a frame header.");
        }

        var length = BinaryPrimitives.ReadInt32LittleEndian(header);
        if (length <= 0 || length > ShellPipeProtocol.MaxFrameBytes)
        {
            throw new InvalidDataException($"Shell pipe frame length {length} is outside 1..{ShellPipeProtocol.MaxFrameBytes}.");
        }

        var body = new byte[length];
        await stream.ReadExactlyAsync(body, cancellationToken);
        return JsonSerializer.Deserialize<ShellPipeMessage>(body, JsonOptions)
            ?? throw new InvalidDataException("Shell pipe frame carried no message.");
    }
}
