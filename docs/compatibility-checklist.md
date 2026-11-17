# Real-client acceptance check

Automated fixtures are useful, but the final compatibility check must use the actual older client deployed on your LAN.

1. Install/run CydLan on Windows computer A. Run the old LAN Messenger on computer B on the same private LAN. Record B's app version. Close any old client on A to avoid port collisions.
2. Allow CydLan on the private network in Windows Firewall. Match both clients' UDP/TCP port and multicast settings (defaults: 50000 and `239.255.100.100`).
3. Start B, then A. Confirm each sees the other with the correct name and status. Repeat with the startup order reversed.
4. Send a plain-text message each way, then multiline text, `& < >`, non-Latin text, and emoji. Confirm each message appears once and CydLan reports Delivered after the old client acknowledges it.
5. Change names and presence each way. Test typing indicators and a broadcast announcement.
6. Send a small binary file and an empty file in both directions. Verify the receiver is prompted, decline one offer, then accept another. Compare SHA256 hashes of accepted files. Confirm an existing file is not overwritten.
7. Interrupt a transfer or disconnect the network. Confirm no incomplete file is presented as received and no chat is falsely marked Delivered.
8. Close the CydLan window. Confirm it remains in the tray and receives a notification. Reopen it, restart it, and confirm history is retained. Quit from the tray and confirm the old peer removes it.
9. Disconnect the internet while keeping the LAN active. Confirm discovery/chat still work.

Record results, Windows version, old-client version, firewall configuration, and whether the two devices use Wi-Fi, Ethernet, VPN, or separate VLANs. The settings dialog includes local diagnostics for connection failures.
