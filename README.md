# InterviewFingerprint

[![.NET build and CLI smoke tests](https://github.com/DianeLeigh76/interview-work-watermark/actions/workflows/dotnet.yml/badge.svg)](https://github.com/DianeLeigh76/interview-work-watermark/actions/workflows/dotnet.yml)

Open-source proof-of-origin tools for interview take-home assignments, artwork and code submissions.

**Status: early experimental v0.2 prototype, not a forensic-grade watermarking system.** Built in C# for .NET 10.

## What works in this prototype

- `mark`: alters the **actual pixels** of an input image with a keyed 64-bit, repeated DCT fingerprint and outputs a PNG. The fingerprint is not EXIF or an invisible text layer.
- `verify`: tests a suspect image against a private key and per-submission ID; reports matching bit count and block vote agreement.
- `archive`: creates a JSON SHA-256 manifest for **any** files, including source code, slides, documents, PDFs and graphics, without changing those files.
- `check-archive`: checks recorded hashes **and file sizes**, rejects duplicate or escaping paths, and reports files added since the manifest was created.

This project does **not yet** hide a fingerprint inside C# source code, prose, PDF text, Office documents, or arbitrary source files. The manifest proves integrity relative to the manifest; it does **not** prove who created a file or when it existed. Independent timestamping and retention of the actual submission are important.

### Quick start

```powershell
cd src/InterviewFingerprint
dotnet restore
dotnet run -- mark --in "campaign.png" --out "campaign-marked.png" --id "employerA-2026-10" 
dotnet run -- verify --in "campaign-marked.png" --id "employerA-2026-10"
dotnet run -- archive --in "C:\Submissions\EmployerA" --out "C:\Evidence\EmployerA.json" --id "employerA-2026-10"
dotnet run -- check-archive --in "C:\Submissions\EmployerA" --manifest "C:\Evidence\EmployerA.json"
```

Before using `mark` or `verify`, set `IFP_SECRET` to a private random secret of at least 16 characters. Do not use the example placeholders as real secrets. Run `dotnet run --` to see usage.

For safety, provide secret keys through a restricted input mechanism in a future release; the current CLI's `IFP_SECRET` is read from an environment variable, which can still be exposed to privileged processes. Never commit real secrets or confidential source materials to GitHub.

### Intended use

1. Produce your original content.
2. Mark individual exported graphics with a **unique submission ID**, checking quality and running `verify` on the actual submitted image.
3. Place the final submitted versions in a submission folder and produce an evidence manifest. Keep the originals, hashes, transmission receipt and source history securely.
4. When suspected reuse occurs, preserve copies of public materials plus URLs and capture dates before comparing anything. Validate image matches and manually investigate other similarities.

### Known limitations

- Images must contain at least **320 complete 8x8 blocks** (roughly 160x160 pixels).
- The method depends on an unchanged 8x8 pixel grid. **Resizing, rotation, cropping, screenshots, color conversion, heavy JPEG compression, print/scan, and adversarial removal can defeat it.** PNG copies at the same dimensions have the best chance.
- Flat white/black areas, transparent images and saturation may weaken the signal. The prototype processes RGB values while preserving alpha; do not use it to protect tiny icons or image assets whose color fidelity is critical.
- 64-bit fingerprints plus multiple votes are not formal proof of origin. The verifier is keyed but not an independent court-ready forensic measurement. Calibrate false-positive rates on clean controls.
- The detector is deliberately conservative: it requires all 64 bits and at least 75% average agreement; some genuinely marked images will fail.
- Evidence archives reject symbolic links and directory junctions rather than following them outside the submission tree. Archive checking also flags extra files and modified file sizes. The manifest is not cryptographically authenticated.
- This version provides no cryptographic signing of manifests; use an independent timestamp or signing solution to strengthen evidence.
- This project does not automatically establish rights, copyright infringement, compensation owed, a fake job posting, or an employer's intent.

## Architecture / planned roadmap

- **v0.1**: image transform + verifier + hash manifest (initial prototype).
- **v0.2**: test harness with image comparison metrics, automatic negative controls, and image transformation robustness matrix; signature and trusted timestamp integration.
- **v0.3**: optional semantic fingerprinting for text and code, applying deterministic *reviewable* edits without changing behavior; requires language-aware parsers, not invisible Unicode characters.
- **v0.4**: DOCX/PPTX/PDF workflows that modify appropriate rendered components and preserve document editability; batch project view.
- **v0.5**: local desktop UI and public evidence report export.

## Dependencies and license

- .NET 10 SDK (LTS)
- SkiaSharp 3.119.4 (MIT), with Linux native assets. See `docs/THIRD-PARTY.md` for notices and packaging considerations.
- Project source code: MIT (see LICENSE). Dependency licensing is separate.

## Contributing

Security and robustness claims must be accompanied by reproducible tests. Keep original/suspect datasets outside the repository. No privacy-invasive telemetry or online dependencies are built in.

## v0.2 engineering review — 2026-10-23

The `v0.2` source changes include:

- `mark` and `verify` now read the secret from `IFP_SECRET` rather than taking it on the command line. **Do not print or commit the secret.** An environment variable can still leak to privileged local processes, so this is not equivalent to a hardware-backed secret store.
- `verify --reference originally-marked.png` can resample a resized suspected image back to its original grid **only when aspect ratios match**. The reference must be the originally submitted marked image, not the unmarked original. This is a heuristic, not validated screenshot or crop robustness. Verification remains keyed; unknown-key public detection is not implemented.
- Verification reports the lowest per-bit vote agreement and uses stricter thresholds. These are engineering heuristics, not measured error rates.
- `code-fingerprint --in file.cs` uses a Roslyn lexer to create a **trivia-independent syntax fingerprint**, making differences in whitespace/comments irrelevant.
- `compare-code --a original.cs --b suspected.cs` returns token equality and five-token shingle Jaccard/containment similarity. Different identifiers and changed syntax still affect this; it is *not* semantic equivalence detection. Very short files are particularly unreliable evidence.
- File hashing now streams file contents rather than loading entire files into memory.

### Example (.NET 10)

```powershell
# Use a long randomly-generated secret. Do not commit it to GitHub.
$env:IFP_SECRET = 'REPLACE_WITH_A_PRIVATE_RANDOM_SECRET_AT_LEAST_16_CHARACTERS'

dotnet run --project src/InterviewFingerprint -- mark --in campaign.png --out campaign-marked.png --id employer-A-2026

dotnet run --project src/InterviewFingerprint -- verify --in campaign-marked.png --id employer-A-2026

# Optional resize recovery for a suspect with unchanged aspect ratio:
dotnet run --project src/InterviewFingerprint -- verify --in suspect.png --reference campaign-marked.png --id employer-A-2026

dotnet run --project src/InterviewFingerprint -- code-fingerprint --in Example.cs
dotnet run --project src/InterviewFingerprint -- compare-code --a Example.cs --b ExampleEdited.cs

Remove-Item Env:IFP_SECRET
```

**Verification status:** GitHub Actions now runs .NET 10 builds and deterministic CLI smoke tests on Windows and Linux. Check the latest Actions result before treating a revision as build-verified. Watermark robustness and forensic validity remain unproven; do not treat v0.2 as production- or court-ready. The original v0.1 instructions using `--key` are obsolete for v0.2.

### Review pass log

1. **Inspection:** Identified CLI key exposure, exact-pixel-grid dependency, memory-heavy file hashing, missing code comparison, and easily misleading aggregate confidence.
2. **Correction:** Replaced CLI secret with environment input, added optional resize grid recovery and weakest-bit checks, added Roslyn syntax comparison, changed file hashing to streaming.
3. **Reevaluation:** Added aspect-ratio rejection to avoid silently stretching suspected evidence; checked syntax/token comparison assumptions and called out non-semantic matching. Static review only; build, false-positive studies, and regression tests remain outstanding.

### Important limitations

The existing image method still has known defects for fully transparent pixels, clipped RGB values, altered geometry, JPEG damage, and adversarial removal. Archive manifests do not carry trustworthy timestamps or digital signatures and may be manipulated; the evidence archive is **integrity-checking assistance, not chain-of-custody proof**. Do not rely on the tool alone in legal proceedings.
## Automated verification

GitHub Actions runs `dotnet restore`, `dotnet build` and a dependency-free C# smoke-test executable on Windows and Ubuntu. Smoke tests cover keyed PNG marking/verification, rejection of wrong submission IDs, syntax fingerprints, file manifest round trips, extra-file and size tampering detection, path validation, and Linux symbolic-link rejection. They do not establish resilience to editing, cropping, screenshots or adversarial attacks.
