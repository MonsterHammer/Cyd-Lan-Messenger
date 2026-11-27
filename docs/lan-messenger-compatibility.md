# Using CydLan with LAN Messenger

CydLan is a modern Windows client that speaks the original **LAN Messenger** protocol. If your office, school, or home network already runs LAN Messenger, you can install CydLan on one PC and keep chatting with everyone still using the old client. Nobody else has to switch, and you don't need a server or an internet connection.

CydLan is an independent application. It is not an official LAN Messenger release.

[Download CydLan](https://github.com/MonsterHammer/Cyd-Lan-Messenger/releases/latest)

## What works with LAN Messenger 1.2.39

These results come from CydLan running beside a real LAN Messenger 1.2.39 install on one PC. A two-PC check is still pending. Details are in [the test report](test-results.md).

| Feature | CydLan ↔ LAN Messenger |
| --- | --- |
| Automatic discovery on the LAN | Yes |
| Encrypted chat session (legacy RSA/AES handshake) | Yes |
| Text chat both ways, including multiline text, non-Latin scripts, and emoji | Yes |
| Delivery receipts | Yes |
| Typing indicators and presence status | Yes |
| Broadcast announcements | Yes |
| Group rooms (create, invite, join, chat) | Yes |
| File transfers both ways | Yes |
| Folder transfers | Implemented; not yet verified against 1.2.39 |
| Profile pictures and contact information | Yes |
| LAN Messenger emoticon codes | Implemented; not yet verified against 1.2.39 |
| Public Chat | Not yet |
| IPv6 discovery | Not yet |

Other LAN Messenger versions use the same protocol family but haven't been tested. Results may vary.

## Why switch to CydLan

- A modern interface with themes, fonts, chat backgrounds, and optional effects.
- Search across people, IP addresses, and saved messages.
- Per-contact drafts that survive a restart.
- Manual retry for unconfirmed messages.
- Connect by IP when multicast discovery is blocked.
- Automatic port fallback when another app already uses the TCP port.
- Runs in the system tray and can start with Windows.
- Self-contained installer. No .NET install, account, or internet connection needed.

See the [feature showcase](features.md) for screenshots.

## Setup

1. Install CydLan from the [latest release](https://github.com/MonsterHammer/Cyd-Lan-Messenger/releases/latest).
2. Allow CydLan through Windows Firewall on **private networks**.
3. Keep the default network settings (UDP/TCP port `50000`, multicast group `239.255.100.100`) unless your LAN Messenger clients use different ones. The values must match.
4. Open CydLan. LAN Messenger users on the same network appear in your contacts automatically.

If someone doesn't appear, use **Preferences → Network → Connect by IP** with their PC's IPv4 address.

### Running both on the same PC

You can run CydLan and LAN Messenger on the same PC. If LAN Messenger already holds TCP port 50000, CydLan picks another TCP port. LAN Messenger ignores the advertised port, so in that setup the connection forms when CydLan dials the old client. A LAN Messenger instance that exclusively owns UDP 50000 may need to be closed.

## Security notes

To stay compatible, CydLan uses LAN Messenger's original encryption: an RSA-OAEP-SHA1 handshake and AES-256-CBC messages. That format doesn't authenticate peers or messages, and file transfers are unencrypted, exactly as in LAN Messenger. Use it on a trusted local network.

## Checking your own network

Every network is different. To confirm compatibility with the LAN Messenger build deployed on your network, follow [the compatibility checklist](compatibility-checklist.md).

## FAQ

**Do I need to uninstall LAN Messenger?**
No. Both clients can stay on the network, and on the same PC with the notes above.

**Does CydLan need the internet?**
No. Discovery, chat, and file transfers stay on your local network.

**Is it free?**
Yes. CydLan is free and open source under GPL-3.0-or-later.
