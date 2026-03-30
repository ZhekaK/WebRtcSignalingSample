# MediaServer safe patch set

This PR adds **non-compiling replacement files** that can be copied over the runtime classes manually.

## Why this is a safe version

These files are stored as `*.txt` so they do **not** affect the current Unity build automatically.
You can review them, then replace the original files manually.

## Files included

- `Patches/ReceiverManager.cs.txt`
- `Patches/ReceiverSession.cs.txt`
- `Patches/SenderManager.cs.txt`

## What the patch fixes

1. **Socket conflict on same PC**
   - In MediaServer mode, `ReceiverSession` becomes a **client** using `VibeClient.ConnectAsync(...)`.
   - It no longer starts `VibeListener` on the same port as the sender.

2. **Restart bug in SenderManager**
   - `RestartTransmissionAsync()` now respects `SenderTransportMode.MediaServer`.
   - It restarts `WebRtcMediaServer` instead of falling back to `SenderSession`.

3. **Receiver MediaServer flow**
   - Receiver supports `MediaServer` mode explicitly.
   - It can auto-request the default layout after connecting.
   - It can send manual `media-subscribe` requests.

4. **Message handling stability**
   - Receiver handles `media-hello`, `media-catalog`, and `media-subscribe-ack` without throwing.

## How to apply

Copy these files over the originals:

- `Patches/ReceiverManager.cs.txt` -> `Receiver/ReceiverManager.cs`
- `Patches/ReceiverSession.cs.txt` -> `Receiver/ReceiverSession.cs`
- `Patches/SenderManager.cs.txt` -> `Sender/SenderManager.cs`

Then test with:

- `SenderManager.RuntimeMode = MediaServer`
- `ReceiverManager.RuntimeMode = MediaServer`
- sender and receiver on the same machine
- receiver `IP = 127.0.0.1`
- same `Port` on both sides

Expected result:

- sender owns the listening socket
- receiver connects as a client
- no "only one usage of socket address" error
- test subscribe requests can be sent after connection
