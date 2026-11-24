# LAN Messenger compatibility notes

Reference: upstream commit `39345c302d5d31c7fd10db82cf1aa5bf4a06ca5c`; see `THIRD-PARTY-NOTICES.md`.

## Discovery

UDP port 50000, multicast `239.255.100.100`. UTF-8 XML packets have root `lmcmessage`, a `head` containing `from`, decimal `messageid`, and `type`, and a `body`. Announcements use `announce`; shutdown uses `depart`. Current upstream optionally advertises `body/tcpport`; released clients without it fall back to TCP 50000. CydLan joins multicast on active IPv4 adapters and sends announcements at startup, on explicit refresh, and every 15 seconds. Adapter memberships are refreshed and failed joins retried. Multicast TTL is 1. Received packets are never relayed or rebroadcast.

Connect by IP sends the same announcement as a directed UDP packet. The recipient opens the normal encrypted session to the advertised TCP port. CydLan listens dual-stack (`[::]` with IPv4 mapping) and exclusively, as LAN Messenger listens on `[::]`; a separate `0.0.0.0` listener on the same port would silently take the old client's IPv4 connections. If the configured TCP port is occupied, CydLan selects an ephemeral TCP port and advertises it while keeping the configured UDP discovery port. LAN Messenger 1.2.39 ignores `tcpport` and always dials its configured port, so an old client reaches a CydLan on a fallback port only when CydLan connects to it (after the old client's announcement or refresh, every 5 minutes by default). UDP sharing still depends on the other local client's socket options. CydLan peers also include `tcpport` in `userdata` so contacts learned through incoming connections retain the correct listening port. Saved endpoints are retried at startup, on refresh, and on the 15-second recovery cycle; a working multicast announcement can update a changed endpoint.

## Encrypted direct session

1. Announce receiver opens a TCP connection to the announcing peer.
2. Initiator sends ASCII `MSG` followed by its user ID, unframed.
3. Acceptor sends a framed `PUBKEY` packet containing PKCS#1 PEM (`RSA PUBLIC KEY`).
4. Initiator sends framed `HNDSHK`, containing a 48-byte session material blob encrypted using RSA-OAEP with SHA1/MGF1-SHA1. The first 32 bytes are an AES key; the last 16 are an IV. Existing 1024-bit peer keys and modern 2048-bit keys are accepted.
5. Subsequent frames use `MESSAG` plus AES-256-CBC/PKCS7 encrypted UTF-8 XML. Each message resets to the negotiated IV, matching upstream's per-call copied cipher context.

A frame is a 4-byte big-endian unsigned byte count followed by a 6-byte ASCII type and payload. The count includes the six type bytes. Reads must tolerate partial length words, partial payloads, and multiple frames in a single TCP read. CydLan limits each frame to 1 MiB, prohibits XML DTDs/entities, bounds user text, and validates that the session identity matches XML `from`.

Simultaneous connection attempts are resolved deterministically while the handshake is pending: the lower peer ID keeps its outgoing connection and the higher peer ID keeps its incoming connection. An established encrypted session with another CydLan is retained. LAN Messenger 1.2.39 needs different handling: it dials a contact whenever that contact's announcement arrives while it does not list them (including during the first handshake), sends through its newest connection (`messageMap.insert`), keeps one AES key per contact, and removes the contact when *any* of its connections to that contact closes (`MsgStream::disconnected` → `removeUser`). A removed contact's messages are no longer acknowledged ("Recipient not found"), and a message that opens a chat window for it crashes LAN Messenger. CydLan therefore never closes or re-handshakes such a second connection: it holds it open without sending `PUBKEY` (LAN Messenger has no handshake timeout), reads frames arriving on it with the established session's key, and closes it together with that session. No discovery packet or user message is relayed to another peer.

The initial user ID has no length or terminator. CydLan uses a short idle interval to delimit it, as the initiator waits for the public key before sending anything else. This inherited ambiguity can fail on an unusually delayed/fragmented identity; the initial exchange has a 10-second deadline. IDs sent by CydLan are stable 24-character lowercase hex strings. LAN Messenger reads the file greeting (below) with a single 64-byte read, so a sender ID longer than 28 characters is truncated and the transfer is never matched; earlier 32-character CydLan IDs are replaced at startup.

The acceptor requests `userdata` with `queryop=get`; the initiator responds with `queryop=result`. Fields include `userid`, `name`, `address`, `version`, `status`, and `usercaps`. CydLan uses the actual socket source address for contact routing instead of trusting the supplied address. CydLan advertises `7`: file transfer (`1`), group chat (`2`), and folder transfer (`4`). LAN Messenger checks the capability bits when offering group invitations or folders.

Direct messages use `type=message` and `body/message`. Acknowledgments use `type=acknowledge` and echo the original ID in `body/messageid`. IDs sent by the UI are signed-64-bit-compatible decimal values. The sender shows Delivered only after an acknowledgment. Each send allows three attempts with a three-second receipt deadline each, reusing the same ID. Exhausting the receipt window is Unconfirmed; connection/write errors are Failed. Manual retries also reuse the original ID. Duplicate received IDs are suppressed in a bounded in-memory cache, including across reconnects while the receiving process remains running; the cache is not persisted across receiver restarts. Heartbeat pings check established connections every 30 seconds, with a 10-second response deadline.

Presence uses `status` (`chat`, `away`, `busy`, `dnd`), name updates use `name`, typing uses `chatstate` (`composing`, `paused`), and announcements use `broadcast` with `body/broadcast`. Broadcasts travel directly over each established encrypted TCP session.

## File transfer

Encrypted `file` control messages negotiate `fileid` (32 hex characters), `mode` (`send`/`receive`), `filetype=normal`, `fileop`, `filename`, and `filesize`. The receiver accepts the offer and advertises its TCP port. The sender then connects separately and sends ASCII `FILE`, file ID, and sender ID; the receiver reads at most 64 bytes of this greeting, which limits sender IDs to 28 characters. The receiver replies `START`, followed by the sender's raw bytes. File payloads are unencrypted by the original protocol.

CydLan matches incoming file streams against a previously accepted offer and the connected peer's IP. It never uses a sender-supplied path. Received bytes go to a unique `.partial` sibling, with a 30-second inactivity timeout. After the exact advertised size, the file is moved to its destination without overwriting. Like LAN Messenger (`StdLocation::fileStorageDir`, `lmcMessaging::getFreeFileName`), an accepted file goes straight into the received files folder, which defaults to `Documents\Received Files`, and a taken name becomes `name [1].ext`, `name [2].ext`, and so on. Empty files follow the same process. An incomplete transfer removes its partial file. The protocol supplies no final application-level receipt to the sender.

Native `folder` offers carry a folder ID, total size, and file count. After acceptance, the sender offers each file individually with `filetype=folder` and its relative path. CydLan validates paths, stages the entire received folder, and moves it to a free name after all advertised files and bytes arrive. Native transfers omit empty subfolders. CydLan falls back to a ZIP for a completely empty folder or a peer without the folder capability.

## Group chat

Matches LAN Messenger's chat rooms (`src/gui/chat/chatroomwindow.cpp` and `src/app/lmc.cpp` upstream). Every operation is an encrypted `groupmessage` sent to one participant at a time, with `body/thread` (the room ID) and `body/groupmsgop`:

- `request`: invite the recipient. A receiver that has no room with that thread creates one, adds the sender, and then sends `join` to its online contacts (only people already in the room act on a `join`).
- `join`: the sender is in the room; add them to the participant list.
- `message`: `body/message` holds the text; it is sent separately to each participant.
- `leave`: remove the sender from the participant list.

Room IDs are the creator's user ID followed by 32 hex digits. LAN Messenger 1.2.39 only adds a participant whose `version` compares above 1.2.10 (it reads the first three dot-separated numbers, without bounds checks), so CydLan sends `1.2.39.CydLan.0.1` and shows it as "CydLan 0.1". It also only lists contacts with the group-chat `usercaps` bit in its room invitation picker.

LAN Messenger reuses one message object when it sends a group operation to several people and appends a header per recipient, so later recipients receive repeated `from`/`to`/`messageid`/`type` elements. CydLan reads the last of each, and a copy addressed to someone else is skipped without closing the session. Group messages are **not** acknowledged by LAN Messenger, so CydLan shows "Sent" (or "Sent to n of m") rather than Delivered. Messages and joins for unknown threads are dropped, and only `request` creates a room, as upstream. Room names are local only; the protocol has no room name.

## Profile

CydLan's `userdata` carries `firstname`, `lastname` and `about` alongside `name`. LAN Messenger's `userdata` does not, so after each connection CydLan sends `query` with `queryop=get`; the `result` contains those fields (LAN Messenger writes `N/A` for blanks). CydLan's own query result includes every field LAN Messenger's Get Information window reads: `userid`, `name`, `address`, `version`, `status`, `note`, `logon`, `host`, `os`, `firstname`, `lastname` and `about`.

Profile pictures use LAN Messenger's `avatar` message: `filetype=avatar`, `fileop=request`, `mode=send`, `fileid`, `filename`, `filesize` and `filepath`. The receiver answers `fileop=accept` automatically and the picture travels over the normal `FILE` stream. CydLan offers its picture (a 96 px PNG) once per connection and to everyone when it changes; it accepts pictures up to 1 MiB, stores them under the transfer ID in its own avatars folder, and keeps them only when the bytes start with a PNG, JPEG, GIF or BMP signature. Pictures never appear in the file-transfer list. LAN Messenger has no message for removing a picture.

LAN Messenger 1.2.39 sends its picture whenever it discovers a peer, and delivers it by dialling its own configured port. When both apps run on one computer that port is LAN Messenger's own listener, so the stream never arrives. For a peer whose address belongs to this computer, CydLan therefore copies the picture from the request's `filepath` (with the same size and signature checks) and answers `fileop=decline`.

### CydLan inline images (extension)

Images pasted, dropped or attached in the composer travel as ordinary `file` offers, so legacy LAN Messenger users receive a normal file they can accept or decline. When the receiving peer's `userdata` `version` starts with `CydLan`, the sender adds one extra body element, `cydlaninline=image`, which legacy parsers skip.

A CydLan receiver accepts such an offer automatically only when all of these hold: the session's peer reported a CydLan version, the extension is `.png`, `.jpg`, `.jpeg`, `.gif`, `.bmp` or `.webp`, the advertised size is at most 20 MiB, and fewer than 20 inline images are already in flight. The file is written under the receiver's own `images/received` folder as `<fileid><extension>` (the sender's name is never used as a path), follows the normal partial-file rules above, and is shown in the conversation only after it decodes as an image; otherwise it is deleted. Any offer that fails these checks falls back to the normal explicit-accept flow. Sent images are copied into `images/sent` so history keeps showing them.

## Architectural boundary

`CydLan.Core` contains transport, XML, cryptography, models, and persistence without a GUI dependency. `CydLan.Desktop` owns WPF views, UI dispatch, history lifecycle, and the tray. There is no HTTP endpoint, WebSocket server, cloud service, relay, or background web process.

## Emoticons

LAN Messenger has no emoticon message. Its picker inserts text codes such as `:-)`, `(Y)` and `(L)`; each client replaces the 81 known codes with 70 pictures when it shows a message, case-insensitively and one code at a time in table order. CydLan sends the same codes, draws the same pictures with the same matching rules (`Smileys.cs`), and leaves Unicode emoji as text.
