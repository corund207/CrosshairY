# Code signing

Unsigned executables trigger SmartScreen ("Windows protected your PC"), and **Smart App Control** blocks them outright. Signing the exe fixes both. The build script handles the signing; what you need is a certificate.

## Getting a certificate

Cheapest and most useful first:

| Option | Cost | Notes |
|---|---|---|
| **[SignPath Foundation](https://signpath.org/)** | Free for open source | Apply with the GitHub repo. They sign releases built by CI (GitHub Actions), so it pairs with an automated release workflow. |
| **[Azure Trusted Signing](https://learn.microsoft.com/azure/trusted-signing/)** | ~$10 / month | Available to individual developers (identity validation required). Microsoft-issued, so SmartScreen trusts it right away. |
| OV code-signing certificate (Sectigo, DigiCert, SSL.com, …) | ~$200–400 / year | Delivered on a hardware token or cloud HSM. SmartScreen reputation builds up over the first downloads. |

EV certificates no longer skip SmartScreen reputation, so they aren't worth the extra cost for this project.

## Signing a build

Install the Windows SDK's *Signing Tools for Desktop Apps* (for `signtool.exe`), set **one** of the following, then build with `-Sign`:

```powershell
# certificate installed in your certificate store (hardware token / cloud HSM)
$env:RETICLY_CERT_THUMBPRINT = "<sha1 thumbprint>"

# or a .pfx file
$env:RETICLY_PFX = "C:\path\cert.pfx"
$env:RETICLY_PFX_PASSWORD = "<password>"

# or Azure Trusted Signing
$env:RETICLY_TRUSTED_SIGNING_DLIB = "C:\path\Azure.CodeSigning.Dlib.dll"
$env:RETICLY_TRUSTED_SIGNING_JSON = "C:\path\metadata.json"

.\build.ps1 -Out build\release\Reticly.exe -Sign
```

The script signs with SHA-256, adds an RFC 3161 timestamp (override the server with `$env:RETICLY_TIMESTAMP_URL`) and verifies the signature.

Never commit certificates or passwords. Keep them in environment variables or CI secrets.

## Updates and signing

The in-app updater verifies each download against the SHA-256 that GitHub publishes for the release asset, signed or not. Once releases are signed, the updater could also check that the new exe's Authenticode signature matches the running one's.
