using System.Globalization;
using System.Net.Sockets;
using System.Text.Json;

namespace CydLan.Core;

/// <summary>Immediate, bounded JSON-lines diagnostics. Callers supply event metadata, never packet or chat contents.</summary>
public sealed class DiagnosticLog
{
    private readonly object gate = new();
    private readonly long maxBytes;
    private readonly int fileCount;
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "cydlan.log");

    public DiagnosticLog(string directoryPath, long maxBytes = 1024 * 1024, int fileCount = 5)
    {
        if (maxBytes < 1024) throw new ArgumentOutOfRangeException(nameof(maxBytes));
        if (fileCount is < 1 or > 20) throw new ArgumentOutOfRangeException(nameof(fileCount));
        DirectoryPath = Path.GetFullPath(directoryPath);
        this.maxBytes = maxBytes;
        this.fileCount = fileCount;
    }

    // Exception messages can contain XML/message text or file names. Record only types, codes and stacks.
    public void Write(string eventName, Exception? error = null, params (string Key, object? Value)[] fields)
    {
        lock (gate)
        {
            try
            {
                var metadata = new Dictionary<string, string>();
                foreach (var field in fields.Take(16))
                    metadata[Limit(field.Key, 64)] = Limit(Convert.ToString(field.Value, CultureInfo.InvariantCulture) ?? "", 256);
                byte[] line = JsonSerializer.SerializeToUtf8Bytes(new {
                    time = DateTimeOffset.Now, pid = Environment.ProcessId, @event = Limit(eventName, 96),
                    fields = metadata, error = error is null ? null : Describe(error, 0)
                });
                if (line.Length + 1 > maxBytes)
                    line = JsonSerializer.SerializeToUtf8Bytes(new { time = DateTimeOffset.Now, pid = Environment.ProcessId, @event = "log.entry_truncated", originalEvent = Limit(eventName, 96) });
                Directory.CreateDirectory(DirectoryPath);
                if (File.Exists(FilePath) && new FileInfo(FilePath).Length + line.Length + 1 > maxBytes) Rotate();
                using var stream = new FileStream(FilePath, FileMode.Append, FileAccess.Write, FileShare.Read);
                stream.Write(line);
                stream.WriteByte((byte)'\n');
                stream.Flush(flushToDisk: true);
            }
            // Logging failures must never take down messaging or mask the original error.
            catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
    }

    private void Rotate()
    {
        if (fileCount == 1) { File.Delete(FilePath); return; }
        File.Delete(FilePath + "." + (fileCount - 1));
        for (int i = fileCount - 2; i >= 1; i--)
            if (File.Exists(FilePath + "." + i)) File.Move(FilePath + "." + i, FilePath + "." + (i + 1), true);
        File.Move(FilePath, FilePath + ".1", true);
    }

    private sealed record ErrorInfo(string Type, int Code, string? SocketCode, string Stack, ErrorInfo[] Inner);
    private static ErrorInfo Describe(Exception error, int depth) => new(
        error.GetType().FullName ?? error.GetType().Name, error.HResult,
        error is SocketException socket ? socket.SocketErrorCode.ToString() : null,
        Limit(error.StackTrace ?? "", 4096),
        depth >= 3 ? [] : (error is AggregateException aggregate ? aggregate.InnerExceptions.Take(8)
            : error.InnerException is { } inner ? new[] { inner } : []).Select(e => Describe(e, depth + 1)).ToArray());
    private static string Limit(string value, int max) => value.Length <= max ? value : value[..max];
}
