# Cyd Lan Messenger

A Windows desktop LAN messenger with a modern interface, system tray support, and a direct implementation of the original LAN Messenger protocol. No central server, cloud account, web backend, or internet connection is needed for messaging.

## Install and run

Download the Windows x64 installer and its SHA-256 checksum from the [v0.1.4 release](https://github.com/MonsterHammer/Cyd-Lan-Messenger/releases/tag/v0.1.4). The unsigned installer includes the .NET runtime and installs for the current Windows user. The optional **Open CydLan** checkbox starts live LAN mode after setup.

Opening CydLan starts live LAN discovery and direct messaging. Contacts appear automatically when another compatible client is on the same network. Allow CydLan through Windows Firewall on **private networks** on both PCs. No internet connection or account is required.

Open `artifacts/CydLan-Windows/CydLan.exe` after building, or run from source:

```powershell
dotnet run --project src/CydLan.Desktop
```

Requires Windows 10/11. The self-contained Windows build includes the runtime; running from source requires the .NET 10 SDK. This project uses WPF and the built-in Windows tray API; it has no third-party NuGet dependencies.

If a contact does not appear, open **Preferences → Network → Connect by IP** and enter the other PC's IPv4 address, optionally followed by its UDP discovery port (for example `192.168.1.25:50000`). This sends a directed announcement; the contact appears after the encrypted connection succeeds. Both devices still need network reachability. Discovery runs every 15 seconds. Automatic connection retries for unreachable peers back off from 30 seconds to 5 minutes. An occupied TCP port gets an automatically selected alternative while UDP discovery stays on the configured port. A legacy client that exclusively owns UDP 50000 may need to be closed if it runs on the same PC.

Use `--preview` for an explicitly offline design preview. Sample contacts, preview messages, and preview preferences stay in memory; preview and screenshot modes open no network sockets or application settings files.

Closing the window keeps CydLan running in the system tray. Double-click its tray icon to reopen it; use **Quit CydLan** in the tray menu to exit. Settings can start it in the tray when launched; automatic launch at Windows sign-in is not yet implemented.

## Included

- Automatic IPv4 multicast discovery, adapter refresh, directed IP discovery, and saved-contact reconnection, with one-hop multicast scope.
- Direct chat with UTF-8 text, delivery acknowledgments, duplicate suppression, typing indicators, and online/offline labels. Chat headers show profile pictures or initials. CydLan peers use application heartbeats and legacy peers use TCP keepalive.
- Group rooms, LAN Messenger emoticons, and light, dark, and system themes.
- Broadcast announcements sent individually to connected peers.
- File offers with save/decline or optional auto-accept, progress, and support for empty files. The Files view and conversation file panel show transfers. The default receive folder is `Documents\Received Files`. Incoming files are staged until the advertised byte count is received. Existing destination files are preserved.
- Search people, IP addresses, and saved messages; search within a conversation.
- Local conversation history with chat deletion, saved contacts, per-contact and broadcast drafts, unread counts, profile settings, and tray notifications. Nonempty drafts survive a normal restart.
- Selectable message text, manual retry for failed/unconfirmed messages, and preserved scroll position while reading older messages.
- A clearly labeled design preview that never opens network sockets or writes application settings.

## Development

The interface uses a compact navigation rail, a collapsible conversation sidebar, and a centered chat column. Maximizing uses the monitor's work area so the composer stays above the Windows taskbar. `Ctrl+B` toggles the sidebar, `Ctrl+K` finds people, `Ctrl+F` searches messages, and Escape closes search/details. The original vector logo and Windows icon live in [assets/brand](assets/brand/README.md).

```powershell
dotnet build CydLan.slnx
dotnet run --project tests/CydLan.Tests
dotnet run --project tests/CydLan.Desktop.Tests
dotnet run --project src/CydLan.Desktop -- --preview
dotnet run --project src/CydLan.Desktop -- --screenshot artifacts/design-preview.png
dotnet run --project src/CydLan.Desktop -- --tray-screenshot artifacts/tray-preview.png
dotnet publish src/CydLan.Desktop -c Release -r win-x64 --self-contained true -o artifacts/CydLan-Windows
./scripts/Publish-Windows.ps1
./scripts/Build-Installer.ps1
```

`Build-Installer.ps1` requires Inno Setup 6 and creates a versioned installer and SHA-256 checksum under `artifacts/installers`. Setup adds a Start menu shortcut and offers an optional desktop shortcut. Firewall rules are managed separately. Uninstall removes the app and shortcuts while retaining local conversations and settings.

Live mode saves diagnostic events to `%LOCALAPPDATA%\CydLan\logs\cydlan.log`. **Preferences → Network → Open logs** opens the folder. The current log and four backups hold up to 1 MiB each. Logs include LAN addresses and peer IDs, connection changes, retries, transfer outcomes, and managed crash details. Chat text, filenames, and exception messages are excluded. Preview mode creates no logs.

The test runners have no external test-framework dependency. Core experiments cover malformed packets, TCP fragmentation, independent OpenSSL crypto fixtures, independently encoded legacy peers, lost-receipt retries, concurrent chat, UDP discovery, automatic recovery after restart, occupied ports (including a dual-stack legacy listener), legacy file-greeting ID limits, and safe binary/empty/interrupted file transfers. One experiment launches a second OS process, exchanges Unicode chat and receipts, and verifies files in both directions. Desktop tests exercise the live view model and render the normal/compact chat and preferences windows. Automated connections use loopback (one port-coexistence check briefly listens on all interfaces); they send no LAN multicast announcements and use temporary settings. See [the experiment report](docs/test-results.md) for evidence and limits.

Settings, contacts, drafts, and up to 10,000 messages are stored in `%LOCALAPPDATA%\CydLan`. These local JSON files are **not encrypted at rest**. Network settings can be changed in `settings.json` while CydLan is closed (`Port`, `Multicast`); these must match the old clients. No router forwarding or dedicated server is required. Each app listens for direct incoming peer connections.

## Compatibility and current limits

The wire protocol is based on upstream LAN Messenger and tested with independent legacy-format fixtures. Compatibility can vary by LAN Messenger version and network configuration. Use [the compatibility checklist](docs/compatibility-checklist.md) to verify your deployment.

Legacy mode preserves the old RSA-OAEP-SHA1 handshake and AES-256-CBC message format. It does not authenticate peer identities, and the old format has no message authentication. File payloads use the original unencrypted TCP file stream. Use it on a trusted local network. The app does not silently advertise stronger protection than the protocol supplies.

Folder transfers, Public Chat, IPv6 discovery, message editing/reactions, and Windows sign-in startup are not implemented. Folder offers are declined. Group rooms and profile pictures interoperate with LAN Messenger 1.2.39. A sender can confirm bytes were written to the connection, but legacy file transfers have no final application receipt; the UI states that limitation. Transfers are not persisted across restarts. Separate VLANs, Wi-Fi client isolation, and VPN filtering can prevent discovery.

## License

GPL-3.0-or-later. Protocol research and test fixture attribution: [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md). This is an independent application, not an official LAN Messenger release.
