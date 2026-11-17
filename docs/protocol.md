# LAN Messenger compatibility notes

Reference: upstream commit `39345c302d5d31c7fd10db82cf1aa5bf4a06ca5c`; see `THIRD-PARTY-NOTICES.md`.

## Discovery

UDP port 50000, multicast `239.255.100.100`. UTF-8 XML packets have root `lmcmessage`, a `head` containing `from`, decimal `messageid`, and `type`, and a `body`. Announcements use `announce`; shutdown uses `depart`. Current upstream optionally advertises `body/tcpport`; released clients without it fall back to TCP 50000. CydLan joins multicast on active IPv4 adapters and sends announcements at startup and on explicit refresh. Multicast TTL is 1. Received packets are never relayed or rebroadcast.

## Encrypted direct session

1. Announce receiver opens a TCP connection to the announcing peer.
2. Initiator sends ASCII `MSG` followed by its user ID, unframed.
3. Acceptor sends a framed `PUBKEY` packet containing PKCS#1 PEM (`RSA PUBLIC KEY`).
4. Initiator sends framed `HNDSHK`, containing a 48-byte session material blob encrypted using RSA-OAEP with SHA1/MGF1-SHA1. The first 32 bytes are an AES key; the last 16 are an IV. Existing 1024-bit peer keys and modern 2048-bit keys are accepted.
5. Subsequent frames use `MESSAG` plus AES-256-CBC/PKCS7 encrypted UTF-8 XML. Each message resets to the negotiated IV, matching upstream's per-call copied cipher context.

A frame is a 4-byte big-endian unsigned byte count followed by a 6-byte ASCII type and payload. The count includes the six type bytes. Reads must tolerate partial length words, partial payloads, and multiple frames in a single TCP read. CydLan limits each frame to 1 MiB, prohibits XML DTDs/entities, bounds user text, and validates that the session identity matches XML `from`.

Simultaneous connection attempts are resolved deterministically while the handshake is pending: the lower peer ID keeps its outgoing connection and the higher peer ID keeps its incoming connection. Established encrypted sessions are retained. No discovery packet or user message is relayed to another peer.

The initial user ID has no length or terminator. CydLan uses a short idle interval to delimit it, as the initiator waits for the public key before sending anything else. This inherited ambiguity can fail on an unusually delayed/fragmented identity; the initial exchange has a 10-second deadline. IDs sent by CydLan are stable ASCII GUIDs under the legacy 61-character greeting limit.

The acceptor requests `userdata` with `queryop=get`; the initiator responds with `queryop=result`. Fields include `userid`, `name`, `address`, `version`, `status`, and `usercaps`. CydLan uses the actual socket source address for contact routing instead of trusting the supplied address. Only normal file capability (`1`) is advertised.

Direct messages use `type=message` and `body/message`. Acknowledgments use `type=acknowledge` and echo the original ID in `body/messageid`. IDs sent by the UI are signed-64-bit-compatible decimal values. The sender shows Delivered only after an acknowledgment; a 10-second timeout is Unconfirmed. Duplicate received IDs are suppressed. Heartbeat pings check established connections every 30 seconds, with a 10-second response deadline.

Presence uses `status` (`chat`, `away`, `busy`, `dnd`), name updates use `name`, typing uses `chatstate` (`composing`, `paused`), and announcements use `broadcast` with `body/broadcast`. Broadcasts travel directly over each established encrypted TCP session.

## File transfer

Encrypted `file` control messages negotiate `fileid` (32 hex characters), `mode` (`send`/`receive`), `filetype=normal`, `fileop`, `filename`, and `filesize`. A receiver must explicitly accept an offer and advertise its TCP port. The sender then connects separately and sends ASCII `FILE`, file ID, and sender ID. The receiver replies `START`, followed by the sender's raw bytes. File payloads are unencrypted by the original protocol.

CydLan matches incoming file streams against a previously accepted offer and the connected peer's IP. It never uses a sender-supplied path. Received bytes go to a unique `.partial` sibling, with a 30-second inactivity timeout. After the exact advertised size, the file is moved to the user-selected destination without overwriting. Empty files follow the same process. An incomplete transfer removes its partial file. The protocol supplies no final application-level receipt to the sender.

## Architectural boundary

`CydLan.Core` contains transport, XML, cryptography, models, and persistence without a GUI dependency. `CydLan.Desktop` owns WPF views, UI dispatch, history lifecycle, and the tray. There is no HTTP endpoint, WebSocket server, cloud service, relay, or background web process.
