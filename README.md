# CydLan

A Windows desktop LAN messenger with a modern interface, system tray support, and a direct implementation of the original LAN Messenger protocol. No central server, cloud account, web backend, or internet connection is needed for messaging.

## Run

**Current phase: offline design only.** Opening CydLan loads sample contacts and conversations. The desktop entry point does not create the LAN service, bind ports, discover peers, or send network traffic. You can keep the original LAN Messenger running while reviewing this design build. Preview messages stay in memory and disappear when the app exits.

Open `artifacts/CydLan-Windows/CydLan.exe` after building, or run from source:

```powershell
dotnet run --project src/CydLan.Desktop
```

Requires Windows 10/11 and the .NET 10 Desktop Runtime. Building requires the .NET 10 SDK. This project uses WPF and the built-in Windows tray API; it has no third-party NuGet dependencies.

Live LAN operation is paused while the design is refined. The protocol implementation remains in `CydLan.Core`, but it is not started by this desktop build. The same-computer port coexistence change is deferred until networking is resumed.

Closing the window keeps CydLan running in the system tray. Double-click its tray icon to reopen it; use **Quit CydLan** in the tray menu to exit. Settings can start it in the tray when launched; automatic launch at Windows sign-in is not yet implemented.

## Included

- Automatic IPv4 multicast discovery, with one-hop multicast scope.
- Direct chat with UTF-8 text, delivery acknowledgments, duplicate suppression, typing indicators, presence, and heartbeat checks.
- Broadcast announcements sent individually to connected peers.
- File offers with explicit save/decline, progress, and support for empty files. Incoming files are staged and moved only after the advertised byte count is received. Existing destination files are never replaced.
- Search people, IP addresses, and saved messages; search within a conversation.
- Local conversation history and saved contacts, unread counts, profile settings, and tray notifications.
- A clearly labeled design preview that never opens network sockets or writes application settings.

## Development

```powershell
dotnet build CydLan.slnx
dotnet run --project tests/CydLan.Tests
dotnet run --project src/CydLan.Desktop -- --preview
dotnet run --project src/CydLan.Desktop -- --screenshot artifacts/design-preview.png
dotnet run --project src/CydLan.Desktop -- --tray-screenshot artifacts/tray-preview.png
dotnet publish src/CydLan.Desktop -c Release -o artifacts/CydLan-Windows
```

The test runner has no external test-framework dependency. It exercises malformed packets, TCP fragmentation, independent OpenSSL crypto fixtures, an independently encoded legacy peer, real loopback chat/receipts, and binary/empty file transfers. It does not send LAN announcements.

Settings, contacts, and up to 10,000 messages are stored in `%LOCALAPPDATA%\CydLan`. History is local JSON and is **not encrypted at rest**. Network settings can be changed in `settings.json` while CydLan is closed (`Port`, `Multicast`); these must match the old clients. No router forwarding or dedicated server is required. Each app listens for direct incoming peer connections.

## Compatibility and current limits

This is a working first implementation, not a claim of certification against every released client. The wire protocol is based on the linked upstream source and exercised with independent legacy-format fixtures. A real two-computer check with your installed old LAN Messenger version is still required; use [the compatibility checklist](docs/compatibility-checklist.md).

Legacy mode preserves the old RSA-OAEP-SHA1 handshake and AES-256-CBC message format. It does not authenticate peer identities, and the old format has no message authentication. File payloads use the original unencrypted TCP file stream. Use it on a trusted local network. The app does not silently advertise stronger protection than the protocol supplies.

Group rooms, folder transfers, avatar exchange, IPv6 discovery, message editing/reactions, Windows sign-in startup, and an installer are not implemented. Folder/avatar offers are declined; only normal file-transfer capability is advertised. A sender can confirm bytes were written to the connection, but legacy file transfers have no final application receipt; the UI states that limitation. Transfers are not persisted across restarts. Separate VLANs, Wi-Fi client isolation, and VPN filtering can prevent discovery.

## License

GPL-3.0-or-later. Protocol research and test fixture attribution: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). This is an independent application, not an official LAN Messenger release.
