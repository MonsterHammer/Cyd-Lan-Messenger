# Third-party notices

LAN Messenger: https://github.com/lanmessenger/lanmessenger

Protocol behavior was researched from upstream commit `39345c302d5d31c7fd10db82cf1aa5bf4a06ca5c`, specifically `src/core/definitions.h`, `src/core/settings.h`, `src/core/crypto.cpp`, `src/network/datagram.cpp`, `src/network/netstreamer.cpp`, and `src/messaging/`.

Upstream source notices identify:

- Copyright (c) 2010–2012 Qualia Digital Solutions.
- Copyright (c) 2026 LAN Messenger.
- SPDX-License-Identifier: GPL-3.0-or-later.

`tests/CydLan.Tests/Fixtures/legacy-test-private.pem` is the intentionally public, test-only 1024-bit key from upstream `tests/data/crypto/test-rsa1024-priv.pem`. It is never used by the application. `openssl-envelope.bin` is an independently generated RSA-OAEP-SHA1 test envelope containing bytes 0 through 47, encrypted against that key using OpenSSL 3. The AES known-answer vector was also generated with OpenSSL 3.

The full GPL v3 license text is included in `LICENSE`.
