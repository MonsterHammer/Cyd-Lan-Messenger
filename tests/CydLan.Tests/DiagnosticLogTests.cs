using System.Net;
using System.Text.Json;
using CydLan.Core;

internal static class DiagnosticLogTests
{
    public static async Task RunAsync()
    {
        string root = Path.Combine(Path.GetTempPath(), "CydLan-logging-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var log = new DiagnosticLog(Path.Combine(root, "concurrent"));
            Parallel.For(0, 100, i => log.Write("test.concurrent", null, ("index", i)));
            Check(File.ReadLines(log.FilePath).Count() == 100, "Concurrent writes lost records.");
            var resumed = new DiagnosticLog(log.DirectoryPath);
            resumed.Write("test.restarted");
            string secret = "PRIVATE_CHAT_BODY_不应记录";
            Exception failure;
            try { throw new InvalidOperationException(secret, new IOException(secret)); }
            catch (Exception e) { failure = e; }
            resumed.Write("test.error", failure, ("peer", "id\nnot-a-log-entry"));
            string[] lines = File.ReadAllLines(log.FilePath);
            Check(lines.Length == 102, "Logs did not survive reopening or contained injected lines.");
            Check(!File.ReadAllText(log.FilePath).Contains(secret), "Exception text leaked into logs.");
            foreach (string line in lines) { using var json = JsonDocument.Parse(line); Check(json.RootElement.TryGetProperty("time", out _), "A record lacks its timestamp."); }
            using (var error = JsonDocument.Parse(lines[^1]))
                Check(error.RootElement.GetProperty("error").GetProperty("Type").GetString() == typeof(InvalidOperationException).FullName
                    && error.RootElement.GetProperty("error").GetProperty("Stack").GetString()!.Contains(nameof(RunAsync)), "Error type or stack was lost.");

            var bounded = new DiagnosticLog(Path.Combine(root, "rotation"), maxBytes: 2048, fileCount: 3);
            for (int i = 0; i < 60; i++) bounded.Write("test.rotation", null, ("index", i), ("padding", new string('x', 240)));
            string[] files = Directory.GetFiles(bounded.DirectoryPath);
            Check(files.Length == 3 && files.All(f => new FileInfo(f).Length <= 2048), "Log rotation exceeded its file or size limit.");
            foreach (string file in files) foreach (string line in File.ReadLines(file)) { using var json = JsonDocument.Parse(line); }
            Check(File.ReadAllText(bounded.FilePath).Contains("59"), "Rotation lost the latest event.");

            string blocked = Path.Combine(root, "blocked");
            File.WriteAllText(blocked, "A file prevents creating a log directory.");
            new DiagnosticLog(blocked).Write("test.disk_unavailable", failure);
            Check(File.ReadAllText(blocked).StartsWith("A file"), "Logging damaged an existing file.");

            var settingsA = new AppSettings { Port = 0 }; var settingsB = new AppSettings { Port = 0 };
            var networkLog = new DiagnosticLog(Path.Combine(root, "network"));
            var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            await using (var one = new LanService(settingsA, log: networkLog))
            await using (var two = new LanService(settingsB))
            {
                var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                one.PeerOnline += _ => ready.TrySetResult();
                one.PeerOffline += _ => closed.TrySetResult();
                await one.StartAsync(false, IPAddress.Loopback); await two.StartAsync(false, IPAddress.Loopback);
                await one.ConnectAsync(settingsB.UserId, "127.0.0.1", two.ListeningPort);
                await ready.Task.WaitAsync(TimeSpan.FromSeconds(5));
                Check(await one.SendChatAsync(settingsB.UserId, secret, "912345"), "Logging interrupted live chat.");
            }
            // Socket cleanup can finish after DisposeAsync returns. Wait for the last log write.
            await closed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            string networkText = File.ReadAllText(networkLog.FilePath);
            Check(networkText.Contains("connection.encrypted") && networkText.Contains("message.delivered") && networkText.Contains("network.stop"), "Network lifecycle or receipt evidence is missing.");
            Check(!networkText.Contains(secret), "Chat contents leaked into network logs.");
        }
        finally
        {
            string full = Path.GetFullPath(root);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("CydLan-logging-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the logging test directory.");
            Directory.Delete(full, true);
        }
    }
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
}
