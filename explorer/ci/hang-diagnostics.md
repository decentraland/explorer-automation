# Windows Explorer hang capture

For a diagnostic dispatch of `windows-inworld-custom-image.yml`, set
`hang_dump_public_key` to a base64-encoded RSA public-key XML (2048 bits or more).
Keep the corresponding private-key XML locally, outside the repository.

The optional monitor captures up to two stack minidumps after 60 seconds without
AltTester log progress. Idle tests can also trigger captures. It records process
CPU, memory, and thread counts, and enables driver/desktop connection logs.
No heap is requested, but stacks can contain session data, so dumps are encrypted
with AES and authenticated with HMAC before reaching the diagnostic artifact.
Only the encryption public key is sent to CI. Raw temporary dumps are deleted
and are outside the artifact directory.

Download the diagnostic artifact and decrypt locally:

```powershell
./explorer/ci/decrypt-explorer-hang.ps1 -EncryptedDump ./explorer-hang-123-1.dmp.enc `
  -PrivateKey C:\private\hang-key.xml -OutputPath C:\private\explorer-hang.dmp
```

Inspect all thread stacks in WinDbg with the matching client and Unity symbols.
A capture is evidence of a progress gap; it does not by itself establish a deadlock.
