# Local chat experiments — 2026-11-17

Result: **17/17 core network/protocol groups and 4/4 desktop groups passed.** The complete solution builds with zero warnings and zero errors. A self-contained Windows x64 publish also completed, and the published executable successfully rendered its offline preview and exited.

These are real UDP/TCP socket experiments on this Windows PC, including a separately launched OS process and independently encoded legacy peer fixtures. They are **not a remote LAN or two-computer certification**. All automated sockets bind to IPv4 loopback. No LAN multicast announcements are sent, and desktop tests use isolated temporary settings instead of the user's profile.

| Experiment | Observed result |
| --- | --- |
| Two independent OS processes | Directed UDP discovery established an encrypted TCP session; Unicode and multiline messages with receipts worked both ways. |
| Binary files across processes | 262,177-byte files transferred in both directions and matched SHA256 hashes. |
| Concurrent chat | 80 bidirectional messages received acknowledgments, with no missing or duplicate messages. |
| Lost delivery receipt | The independent legacy fixture withheld the first acknowledgment; the retry preserved its message ID and succeeded. |
| Manual retry in the desktop | An Unconfirmed message became Delivered after retry, with one message at the receiver. |
| Peer restart | Directed discovery re-established a session using the same identity on a new endpoint. The surviving receiver suppressed an already-seen message ID. |
| Automatic recovery | A saved contact reconnected at startup and again after restart through the 15-second recovery cycle, without multicast or user refresh. |
| Occupied TCP port | The client used an alternate TCP listener, kept its UDP discovery port, and successfully exchanged acknowledged chat. |
| File safety | Empty and binary files succeeded; declined offers stayed declined; an existing destination was preserved; changed-source/interrupted transfers exposed no incomplete file and left no partial bytes. |
| Legacy compatibility fixtures | Inbound and outbound RSA handshakes, independent OpenSSL vectors, numeric IDs, UTF-8/XML escaping, frame fragmentation, and duplicate acknowledgment behavior passed. |
| Desktop behavior | Live discovery, send/receive, real delivery labels, unread counts while inactive or viewing transfers, offline draft preservation, and retry feedback passed. |
| Local persistence | History, separate contact/broadcast drafts, profile changes, stable identity, and corrupt JSON recovery passed. |
| Layout | Normal and compact live chat plus live preferences were rendered and visually inspected. |

Run these experiments with the .NET 10 SDK:

```powershell
dotnet build CydLan.slnx
dotnet run --project tests/CydLan.Tests
dotnet run --project tests/CydLan.Desktop.Tests
```

Desktop tests write screenshots to `artifacts/live-chat-tested.png`, `artifacts/compact-chat-tested.png`, and `artifacts/live-preferences-tested.png`. Captured runner output is available in `artifacts/network-test-results.txt` and `artifacts/desktop-test-results.txt` for this run.

Still requires a second device: physical Wi-Fi/Ethernet multicast, Windows Firewall prompts/rules on another PC, adapter changes on a real network, and the particular released LAN Messenger build installed there. Use the [real-client checklist](compatibility-checklist.md) when another computer is available. IPv6, group rooms, and modern authenticated encryption remain outside the implemented feature set. The legacy cryptographic and unencrypted-file limitations in the README still apply.

## Logo and layout revision

The four desktop groups were rerun successfully after the logo and layout redesign. Normal (1200×800), compact (800×560), empty, transfer, and contact-detail screens were rendered and inspected. The Windows icon loaded successfully at 32px; its ICO contains seven sizes. The test harness now uses WPF's application lifetime with the shared theme, and exits cleanly after disposing the isolated peers and data. Close-to-tray handling allows the main window to close when the application is quitting.

Design captures are `artifacts/layout-preview.png`, `artifacts/layout-compact.png`, `artifacts/layout-empty.png`, `artifacts/layout-transfers.png`, and `artifacts/layout-details.png`. Editable logo sources and exports are in `assets/brand`.

## Real LAN Messenger 1.2.39 on the same PC — 2026-11-17

A headless CydLan peer built from `CydLan.Core` ran beside the installed LAN Messenger 1.2.39 (Windows 11, WSL/VirtualBox virtual adapters present). The legacy GUI was driven directly; results were checked in both clients and in LAN Messenger's own log.

| Check | Result |
| --- | --- |
| Discovery and encrypted session, both startup orders | Worked. |
| Chat both ways (multiline, `& < > " '`, Cyrillic/Japanese/Arabic, emoji), receipts | Worked. |
| Typing, status, broadcast, group request → join | Worked. |
| File legacy → CydLan | Worked; SHA256 matched. |
| File CydLan → legacy | **Failed before the fix**: LAN Messenger reads the file greeting in one 64-byte read, truncating CydLan's 32-character ID. Fixed with 24-character IDs; a 70,000-byte file then completed with a matching SHA256. |
| Same-PC port 50000 | **Failed before the fix**: CydLan bound `0.0.0.0:50000` beside LAN Messenger's `[::]:50000` and received IPv4 connections meant for it. Fixed with a dual-stack exclusive listener; CydLan now falls back to another port. |

Still open: LAN Messenger ignores `tcpport`, so with CydLan on a fallback port the connection forms only when CydLan dials the old client (on its announcement or refresh). LAN Messenger's message IDs restart at small numbers each launch, so CydLan's per-contact duplicate filter could drop a new message after a legacy restart; not reproduced. Public Chat, avatar exchange, and subnet-broadcast discovery are not implemented. A second physical PC is still required for certification.

### Group chat and profiles with LAN Messenger 1.2.39 — 2026-11-17

| Check | Result |
| --- | --- |
| Room created in LAN Messenger, two CydLan peers invited | **Failed before the fix**: CydLan was missing from the invitation picker (`usercaps` 1) and was never added as a participant (`version` "CydLan 0.1" compares below 1.2.10). A second recipient's copy carried stacked headers, which CydLan treated as an identity mismatch and disconnected. After the fixes, all three participants joined, and a message with Chinese, Cyrillic and emoji arrived intact in both CydLan peers; a CydLan reply appeared in LAN Messenger. |
| Get Information on a CydLan contact in LAN Messenger | Picture, status, first name, last name, About, IP, logon, computer name, OS and version all shown. |
| Profile picture CydLan → LAN Messenger | Shown in LAN Messenger's contact list. |
| Profile picture LAN Messenger → CydLan | Received byte-for-byte (SHA256 matched `avt_local.png`). On one PC this needed a relay on IPv4 :50000, because LAN Messenger ignores `tcpport`. |
| LAN Messenger first/last name/About → CydLan | Query reply received (fields were blank in that profile). |

LAN Messenger crashed once (heap corruption, 0xC0000374) when its own avatar was changed while an earlier avatar send to itself was stuck (a same-PC artifact) and old chat-room windows were open. After a restart its avatar reached CydLan normally; the avatar change was not repeated, so the cause is not confirmed.
