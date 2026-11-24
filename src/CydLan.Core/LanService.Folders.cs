using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Sockets;
using System.Threading.Channels;

namespace CydLan.Core;

public sealed partial class LanService
{
    private const int MaxFolderFiles = 10_000;
    private static readonly TimeSpan FolderStepTimeout = TimeSpan.FromSeconds(45);
    private readonly ConcurrentDictionary<(string Peer, string Id), NativeFolder> nativeFolders = new();
    private readonly ConcurrentDictionary<(string Peer, string Id), PendingFolderSend> folderSends = new();
    private readonly ConcurrentDictionary<(string Peer, string Id), PendingFolderReceive> folderReceives = new();

    private static List<FolderEntry> ReadFolder(DirectoryInfo source, CancellationToken cancellationToken)
    {
        if (!source.Exists) throw new DirectoryNotFoundException("Folder not found.");
        if (source.Parent is null || !SafeFolderName(source.Name)) throw new IOException("This folder can't be attached.");
        var entries = new List<FolderEntry>();
        var pending = new Stack<FileSystemInfo>();
        pending.Push(source);
        long total = 0;
        while (pending.TryPop(out var item))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if ((item.Attributes & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The folder contains a symbolic link or junction.");
            if (!SafeFolderName(item.Name)) throw new IOException("The folder contains an unsupported file name.");
            if (item is DirectoryInfo directory)
            {
                foreach (var child in directory.EnumerateFileSystemInfos()) pending.Push(child);
            }
            else if (item is FileInfo file)
            {
                if (entries.Count == MaxFolderFiles || file.Length > long.MaxValue - total)
                    throw new IOException("The folder is too large to transfer.");
                total += file.Length;
                entries.Add(new(Guid.NewGuid().ToString("N").ToUpperInvariant(),
                    Path.GetRelativePath(source.FullName, file.FullName).Replace(Path.DirectorySeparatorChar, '/'), file.Length));
            }
        }
        return entries;
    }

    private static bool SafeFolderName(string name)
    {
        if (name.Length is 0 or > 240 || name.EndsWith('.') || name.EndsWith(' ') ||
            name.IndexOfAny(['\\', '/', ':', '*', '?', '"', '<', '>', '|']) >= 0 || name.Any(char.IsControl))
            return false;
        string stem = name.Split('.', 2)[0].TrimEnd().ToUpperInvariant();
        return stem is not ("CON" or "PRN" or "AUX" or "NUL" or "CONIN$" or "CONOUT$") &&
            !(stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) &&
              "0123456789¹²³".Contains(stem[3]));
    }

    private static bool FolderId(string id) => id.Length == 32 && id.All(Uri.IsHexDigit);

    private static string[] FolderPathParts(string path)
    {
        var parts = path.Replace('\\', '/').Split('/');
        if (path.Length > 4096 || parts.Any(part => !SafeFolderName(part)))
            throw new IOException("The folder contains an invalid relative path.");
        return parts;
    }

    private async Task<FileTransfer> OfferNativeFolderAsync(Session session, DirectoryInfo source, List<FolderEntry> entries)
    {
        var transfer = new FileTransfer {
            Id = Guid.NewGuid().ToString("N").ToUpperInvariant(), PeerId = session.Id, Name = source.Name,
            Size = entries.Sum(file => file.Size), Outgoing = true, Folder = true, Path = source.FullName
        };
        var folder = new NativeFolder(session, transfer, entries.Count, stopping.Token);
        nativeFolders[(session.Id, transfer.Id)] = folder;
        try { await SendAsync(session, "folder", FolderBody(folder, "request")); }
        catch { nativeFolders.TryRemove((session.Id, transfer.Id), out _); folder.Complete(); throw; }
        TransferChanged?.Invoke(transfer);
        _ = RunNativeFolderAsync(folder, () => SendNativeFolderAsync(folder, entries));
        return transfer;
    }

    private async Task HandleFolderAsync(Session session, WireMessage message)
    {
        string id = message.Get("folderid");
        if (!FolderId(id)) return;
        var key = (session.Id, id);
        string operation = message.Get("fileop");
        if (operation == "request" && message.Get("mode") == "send" && message.Get("filetype") == "folder")
        {
            string name = message.Get("filename");
            if (!SafeFolderName(name) ||
                !long.TryParse(message.Get("filesize"), NumberStyles.None, CultureInfo.InvariantCulture, out long size) ||
                !int.TryParse(message.Get("filecount"), NumberStyles.None, CultureInfo.InvariantCulture, out int count) ||
                count > MaxFolderFiles || (count == 0 && size != 0) || nativeFolders.Count >= 100)
            {
                await SendAsync(session, "folder", new() { ["folderid"] = id, ["fileid"] = id, ["filetype"] = "folder", ["mode"] = "receive", ["fileop"] = "decline" });
                return;
            }
            var transfer = new FileTransfer { Id = id, PeerId = session.Id, Name = name, Size = size, Folder = true };
            var incoming = new NativeFolder(session, transfer, count, stopping.Token);
            if (!nativeFolders.TryAdd(key, incoming)) { incoming.Complete(); return; }
            TransferChanged?.Invoke(transfer);
            _ = RunNativeFolderAsync(incoming, () => ReceiveNativeFolderAsync(incoming));
            if (AutoAcceptDirectory is { } folder)
            {
                try { await AcceptFileIntoAsync(transfer, folder); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    incoming.Stop("Failed: " + e.Message);
                    await SendAsync(session, "folder", FolderBody(incoming, "decline"));
                }
            }
            return;
        }
        if (!nativeFolders.TryGetValue(key, out var existing) || existing.Session != session) return;
        if (operation == "accept" && existing.Transfer.Outgoing && message.Get("mode") == "receive")
            existing.Decision.TrySetResult("");
        else if (operation is "cancel" or "abort" or "decline")
            existing.Stop(operation switch { "cancel" => "Cancelled", "decline" => "Declined", _ => "Aborted" });
    }

    private static Dictionary<string, string> FolderBody(NativeFolder folder, string operation) => new()
    {
        ["folderid"] = folder.Transfer.Id, ["fileid"] = folder.Transfer.Id, ["filetype"] = "folder",
        ["mode"] = folder.Transfer.Outgoing ? "send" : "receive", ["fileop"] = operation,
        ["filename"] = folder.Transfer.Name, ["filesize"] = folder.Transfer.Size.ToString(CultureInfo.InvariantCulture),
        ["filecount"] = folder.FileCount.ToString(CultureInfo.InvariantCulture)
    };

    private NativeFolder RequireNativeFolder(FileTransfer transfer)
    {
        if (!nativeFolders.TryGetValue((transfer.PeerId, transfer.Id), out var folder) || folder.Transfer != transfer ||
            transfer.State != "Waiting for approval" || folder.Token.IsCancellationRequested)
            throw new InvalidOperationException("This folder offer is no longer available.");
        return folder;
    }

    private void AcceptNativeFolder(FileTransfer transfer, string destination)
    {
        var folder = RequireNativeFolder(transfer);
        transfer.Path = destination;
        transfer.State = "Accepted";
        folder.Decision.TrySetResult(destination);
        TransferChanged?.Invoke(transfer);
    }

    private async Task AnswerNativeFolderAsync(FileTransfer transfer, string operation, string state)
    {
        var folder = RequireNativeFolder(transfer);
        await SendAsync(folder.Session, "folder", FolderBody(folder, operation));
        folder.Stop(state);
    }

    private void HandleFolderFile(Session session, WireMessage message)
    {
        string id = message.Get("fileid");
        if (!FolderId(id)) return;
        var key = (session.Id, id);
        string operation = message.Get("fileop");
        if (operation == "request" && message.Get("mode") == "send")
        {
            if (!nativeFolders.TryGetValue((session.Id, message.Get("folderid")), out var folder) || folder.Session != session ||
                folder.Transfer.Outgoing || folder.Transfer.State is not ("Accepted" or "Transferring")) return;
            if (!long.TryParse(message.Get("filesize"), NumberStyles.None, CultureInfo.InvariantCulture, out long size) ||
                !folder.Offers.Writer.TryWrite(new(id, message.Get("relpath"), size)))
                folder.Stop("Failed: Invalid folder file offer.");
        }
        else if (folderSends.TryGetValue(key, out var send) && send.Folder.Session == session)
        {
            if (operation == "accept" && message.Get("mode") == "receive")
                send.Port.TrySetResult(int.TryParse(message.Get("tcpport"), out int port) && port is > 0 and <= 65535 ? port : session.Port);
            else if (operation is "decline" or "cancel" or "abort") send.Folder.Stop("Aborted");
        }
        else if (folderReceives.TryGetValue(key, out var receive) && receive.Folder.Session == session &&
            operation is "cancel" or "abort") receive.Folder.Stop("Aborted");
    }

    private async Task SendNativeFolderAsync(NativeFolder folder, List<FolderEntry> entries)
    {
        await folder.Decision.Task.WaitAsync(folder.Token);
        var transfer = folder.Transfer;
        transfer.State = "Connecting";
        TransferChanged?.Invoke(transfer);
        long sent = 0;
        foreach (var entry in entries)
        {
            folder.Token.ThrowIfCancellationRequested();
            string path = ResolveFolderPath(transfer.Path, FolderPathParts(entry.RelativePath), checkLinks: true);
            var part = new PendingFolderSend(folder);
            var key = (transfer.PeerId, entry.Id);
            if (!folderSends.TryAdd(key, part)) throw new IOException("Duplicate folder file ID.");
            try
            {
                await SendAsync(folder.Session, "file", new() {
                    ["folderid"] = transfer.Id, ["fileid"] = entry.Id, ["filetype"] = "folder",
                    ["fileop"] = "request", ["mode"] = "send", ["filename"] = Path.GetFileName(path),
                    ["relpath"] = entry.RelativePath, ["filesize"] = entry.Size.ToString(CultureInfo.InvariantCulture)
                });
                int port = await part.Port.Task.WaitAsync(FolderStepTimeout, folder.Token);
                await SendFilePayloadAsync(folder.Session, port, entry.Id, path, entry.Size,
                    bytes => ReportFolderProgress(transfer, sent + bytes), folder.Token);
                sent += entry.Size;
            }
            finally { folderSends.TryRemove(key, out _); }
        }
    }

    private async Task ReceiveNativeFolderAsync(NativeFolder folder)
    {
        string destination = await folder.Decision.Task.WaitAsync(folder.Token);
        string parent = Path.GetDirectoryName(Path.GetFullPath(destination))!;
        Directory.CreateDirectory(parent);
        string stage = Path.Combine(parent, ".cydlan-" + Guid.NewGuid().ToString("N") + ".partial");
        Directory.CreateDirectory(stage);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long received = 0;
        bool moved = false;
        try
        {
            var response = FolderBody(folder, "accept");
            response["tcpport"] = ListeningPort.ToString(CultureInfo.InvariantCulture);
            await SendAsync(folder.Session, "folder", response);
            for (int index = 0; index < folder.FileCount; index++)
            {
                var entry = await folder.Offers.Reader.ReadAsync(folder.Token).AsTask().WaitAsync(FolderStepTimeout, folder.Token);
                string path = ResolveFolderPath(stage, FolderPathParts(entry.RelativePath), checkLinks: false);
                if (entry.Size > folder.Transfer.Size - received || !seen.Add(path))
                    throw new IOException("The folder offer doesn't match its files.");
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                await ReceiveNativeFolderEntryAsync(folder, entry, path, received);
                received += entry.Size;
            }
            if (received != folder.Transfer.Size) throw new IOException("The folder offer doesn't match its advertised size.");
            folder.Token.ThrowIfCancellationRequested();
            string name = Path.GetFileName(destination);
            while (true)
            {
                folder.Token.ThrowIfCancellationRequested();
                destination = UniquePath(parent, name);
                try { Directory.Move(stage, destination); break; }
                catch (IOException) when (Directory.Exists(destination) || File.Exists(destination)) { }
            }
            moved = true;
            folder.Transfer.Path = destination;
        }
        finally
        {
            if (!moved)
                try { Directory.Delete(stage, recursive: true); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { }
        }
    }

    private async Task ReceiveNativeFolderEntryAsync(NativeFolder folder, FolderEntry entry, string path, long received)
    {
        var part = new PendingFolderReceive(folder, entry, path, received);
        var key = (folder.Transfer.PeerId, entry.Id);
        if (!folderReceives.TryAdd(key, part)) throw new IOException("Duplicate folder file ID.");
        try
        {
            await SendAsync(folder.Session, "file", new() {
                ["fileid"] = entry.Id, ["folderid"] = folder.Transfer.Id, ["filetype"] = "folder",
                ["mode"] = "receive", ["fileop"] = "accept", ["filesize"] = entry.Size.ToString(CultureInfo.InvariantCulture),
                ["tcpport"] = ListeningPort.ToString(CultureInfo.InvariantCulture)
            });
            await part.Started.Task.WaitAsync(FolderStepTimeout, folder.Token);
            if (await part.Completed.Task is { } error) throw error;
        }
        finally
        {
            folderReceives.TryRemove(key, out _);
            // If the socket has claimed the entry, let it close its partial file before deleting the staging directory.
            if (!part.TryClaim()) await part.Completed.Task;
        }
    }

    private async Task<bool> TryReceiveFolderFileAsync(Session session, string id, NetworkStream stream)
    {
        if (!folderReceives.TryGetValue((session.Id, id), out var part)) return false;
        if (part.Folder.Session != session || part.Folder.Token.IsCancellationRequested || !part.TryClaim()) return true;
        part.Started.TrySetResult();
        Exception? error = null;
        try
        {
            await ReceiveFilePayloadAsync(stream, id, part.Path, part.Entry.Size,
                bytes => ReportFolderProgress(part.Folder.Transfer, part.PreviousBytes + bytes), part.Folder.Token);
        }
        catch (Exception e) { error = e; }
        finally { part.Completed.TrySetResult(error); }
        return true;
    }

    private static string ResolveFolderPath(string root, string[] parts, bool checkLinks)
    {
        if (checkLinks && (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
            throw new IOException("The folder now contains a symbolic link or junction.");
        string path = root;
        foreach (string part in parts)
        {
            path = Path.Combine(path, part);
            if (checkLinks && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("The folder now contains a symbolic link or junction.");
        }
        return path;
    }

    private void ReportFolderProgress(FileTransfer transfer, long bytes)
    {
        transfer.State = "Transferring";
        transfer.Progress = Percent(bytes, transfer.Size);
        TransferChanged?.Invoke(transfer);
    }

    private async Task RunNativeFolderAsync(NativeFolder folder, Func<Task> transferWork)
    {
        var transfer = folder.Transfer;
        string state = "Interrupted";
        try
        {
            await transferWork();
            transfer.Progress = 100;
            state = transfer.Outgoing ? "Sent · receipt unavailable" : "Received";
        }
        catch (OperationCanceledException) when (folder.Token.IsCancellationRequested) { }
        catch (Exception e) when (e is IOException or SocketException or UnauthorizedAccessException or InvalidDataException or TimeoutException)
        {
            state = "Failed: " + e.Message;
            log?.Write("transfer.folder_failed", e, ("peer", transfer.PeerId), ("id", transfer.Id));
            try { await SendAsync(folder.Session, "folder", FolderBody(folder, "abort")); }
            catch (Exception sendError) when (sendError is IOException or SocketException or OperationCanceledException) { }
        }
        finally
        {
            nativeFolders.TryRemove((transfer.PeerId, transfer.Id), out _);
            transfer.State = folder.Complete() ?? state;
            TransferChanged?.Invoke(transfer);
        }
    }

    private void StopFolderTransfers(Session session, string reason)
    {
        foreach (var folder in nativeFolders.Values)
            if (folder.Session == session) folder.Stop(reason);
    }

    private sealed record FolderEntry(string Id, string RelativePath, long Size);
    private sealed record PendingFolderSend(NativeFolder Folder)
    {
        public TaskCompletionSource<int> Port { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    private sealed record PendingFolderReceive(NativeFolder Folder, FolderEntry Entry, string Path, long PreviousBytes)
    {
        private int claimed;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<Exception?> Completed { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool TryClaim() => Interlocked.Exchange(ref claimed, 1) == 0;
    }

    private sealed class NativeFolder
    {
        private readonly CancellationTokenSource cancellation;
        private readonly object gate = new();
        private string? stopState;
        private bool completed;
        public NativeFolder(Session session, FileTransfer transfer, int fileCount, CancellationToken stopping)
        {
            Session = session; Transfer = transfer; FileCount = fileCount;
            cancellation = CancellationTokenSource.CreateLinkedTokenSource(stopping);
            Token = cancellation.Token;
        }
        public Session Session { get; }
        public FileTransfer Transfer { get; }
        public int FileCount { get; }
        public CancellationToken Token { get; }
        public TaskCompletionSource<string> Decision { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Finished { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Channel<FolderEntry> Offers { get; } = Channel.CreateBounded<FolderEntry>(1);
        public void Stop(string state)
        {
            lock (gate)
            {
                if (completed || stopState is not null) return;
                stopState = state;
                cancellation.Cancel();
            }
        }
        public string? Complete()
        {
            lock (gate)
            {
                completed = true;
                cancellation.Dispose();
                Finished.TrySetResult();
                return stopState;
            }
        }
    }
}
