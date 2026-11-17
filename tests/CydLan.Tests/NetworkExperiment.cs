using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using CydLan.Core;

// Uses a second OS process and real loopback UDP/TCP. Never scans the LAN,
// contacts an internet service, or impersonates a remote-computer test.
internal static class NetworkExperiment
{
    private sealed record Packet(string Kind, string Text = "", string Id = "", int Port = 0);

    public static async Task<int> RunPeerAsync(string[] args)
    {
        var settings = new AppSettings { UserId = args[1], DisplayName = "Independent process peer", Port = 0 };
        string directory = args[2];
        await using var service = new LanService(settings);
        var offers = new ConcurrentDictionary<string, FileTransfer>();
        object outputGate = new();
        void Output(Packet packet) { lock (outputGate) Console.WriteLine(JsonSerializer.Serialize(packet)); }
        service.PeerOnline += peer => Output(new("online", Id: peer.Id));
        service.MessageReceived += message => Output(new("received", message.Text, message.Id));
        service.TransferChanged += transfer => {
            offers[transfer.Id] = transfer;
            if (transfer.State == "Waiting for approval") Output(new("offer", transfer.Name, transfer.Id));
            if (transfer.State == "Received") Output(new("file-received", transfer.Name, transfer.Id));
        };
        await service.StartAsync(true, IPAddress.Loopback);
        Output(new("ready", Port: service.DiscoveryPort));
        while (await Console.In.ReadLineAsync() is { } line)
        {
            var command = JsonSerializer.Deserialize<Packet>(line)!;
            switch (command.Kind)
            {
                case "send":
                    bool delivered = await service.SendChatAsync(command.Id, command.Text, "12001");
                    Output(new("receipt", delivered.ToString()));
                    break;
                case "accept":
                    await service.AcceptFileAsync(offers[command.Id], Path.Combine(directory, "child-received.bin"));
                    break;
                case "offer":
                    await service.OfferFileAsync(command.Id, Path.Combine(directory, "child-source.bin"));
                    break;
                case "quit": return 0;
            }
        }
        return 0;
    }

    public static async Task RunAsync()
    {
        string directory = Path.Combine(Path.GetTempPath(), "CydLan-process-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var settings = new AppSettings { DisplayName = "Parent process", Port = 0 };
        var childId = AppSettings.NewUserId();
        var start = new ProcessStartInfo(Environment.ProcessPath!) {
            UseShellExecute = false, RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            CreateNoWindow = true
        };
        if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            start.ArgumentList.Add(typeof(NetworkExperiment).Assembly.Location);
        start.ArgumentList.Add("--experiment-peer"); start.ArgumentList.Add(childId); start.ArgumentList.Add(directory);
        using var child = Process.Start(start) ?? throw new Exception("Could not launch the independent peer.");
        var stderr = child.StandardError.ReadToEndAsync();
        try
        {
            async Task Send(Packet command) { await child.StandardInput.WriteLineAsync(JsonSerializer.Serialize(command)); await child.StandardInput.FlushAsync(); }
            async Task<Packet> Wait(string kind)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(8));
                while (await child.StandardOutput.ReadLineAsync(deadline.Token) is { } line)
                {
                    var packet = JsonSerializer.Deserialize<Packet>(line)!;
                    if (packet.Kind == kind) return packet;
                }
                throw new Exception("Independent peer exited: " + await stderr);
            }
            static void Require(bool ok, string reason) { if (!ok) throw new Exception(reason); }
            var ready = await Wait("ready");
            await using var service = new LanService(settings);
            var online = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var inbound = new TaskCompletionSource<ChatMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
            var offer = new TaskCompletionSource<FileTransfer>(TaskCreationOptions.RunContinuationsAsynchronously);
            var fileReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            service.PeerOnline += _ => online.TrySetResult();
            service.MessageReceived += message => inbound.TrySetResult(message);
            service.TransferChanged += transfer => {
                if (!transfer.Outgoing && transfer.State == "Waiting for approval") offer.TrySetResult(transfer);
                if (!transfer.Outgoing && transfer.State == "Received") fileReceived.TrySetResult();
            };
            await service.StartAsync(true, IPAddress.Loopback);
            await service.DiscoverAsync(IPAddress.Loopback, ready.Port);
            await online.Task.WaitAsync(TimeSpan.FromSeconds(8));
            await Wait("online");
            const string outbound = "Hello from process A 👋\n你好 & <LAN>";
            Require(await service.SendChatAsync(childId, outbound, "11001"), "Child did not acknowledge chat.");
            Require((await Wait("received")).Text == outbound, "Child received altered Unicode text.");
            const string reply = "Reply from process B ✅\nSeamless local chat.";
            await Send(new("send", reply, settings.UserId));
            Require((await inbound.Task.WaitAsync(TimeSpan.FromSeconds(8))).Text == reply, "Parent received an altered reply.");
            Require((await Wait("receipt")).Text == "True", "Parent did not acknowledge the child reply.");
            var bytes = RandomNumberGenerator.GetBytes(262177);
            string source = Path.Combine(directory, "parent-source.bin");
            await File.WriteAllBytesAsync(source, bytes);
            await service.OfferFileAsync(childId, source);
            var incoming = await Wait("offer"); await Send(new("accept", Id: incoming.Id));
            await Wait("file-received");
            Require(SHA256.HashData(await File.ReadAllBytesAsync(Path.Combine(directory, "child-received.bin"))).SequenceEqual(SHA256.HashData(bytes)), "Child file hash differs.");
            await File.WriteAllBytesAsync(Path.Combine(directory, "child-source.bin"), bytes.Reverse().ToArray());
            await Send(new("offer", Id: settings.UserId));
            var reverseOffer = await offer.Task.WaitAsync(TimeSpan.FromSeconds(8));
            string target = Path.Combine(directory, "parent-received.bin");
            await service.AcceptFileAsync(reverseOffer, target);
            await fileReceived.Task.WaitAsync(TimeSpan.FromSeconds(8));
            Require(SHA256.HashData(await File.ReadAllBytesAsync(target)).SequenceEqual(SHA256.HashData(bytes.Reverse().ToArray())), "Reverse transfer hash differs.");
            Console.WriteLine($"      Two OS processes · UDP discovery · Unicode chat + receipts both ways · 262,177-byte files both ways, SHA256 verified.");
            await Send(new("quit"));
            await child.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            Require(child.ExitCode == 0, "Child peer failed: " + await stderr);
        }
        finally
        {
            if (!child.HasExited) { child.Kill(entireProcessTree: true); await child.WaitForExitAsync(); }
            string full = Path.GetFullPath(directory);
            string temp = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (!full.StartsWith(temp, StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(full).StartsWith("CydLan-process-", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the experiment directory.");
            Directory.Delete(full, true);
        }
    }
}
