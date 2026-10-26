# InterviewFingerprint

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
- The method depends on an unchanged 8x8 pixel grid. **Rotation, cropping without a reference, screenshots, heavy JPEG compression, print/scan, and adversarial removal can defeat it.** With a verified originally marked `--reference`, the verifier now searches candidate pixel-level crop offsets up to 64 pixels removed per dimension before attempting resize recovery. Offsets may be ambiguous and some payload bits may be absent after cropping; only observed bits are counted, with at least 48 of 64 required for a reference-assisted match. This is not validated forensic crop recovery. PNG copies at the same dimensions remain the safest.
- Flat white/black areas and saturation may weaken the signal. **Images containing any transparent or partially transparent pixels are now rejected** for both marking and verification; flatten them against an opaque background first. This prevents invisible RGB data from being mistaken for a reliable visible-image watermark. Do not use it for tiny icons or assets whose color fidelity is critical.
- 64-bit fingerprints plus multiple votes are not formal proof of origin. The verifier is keyed but not an independent court-ready forensic measurement. Calibrate false-positive rates on clean controls.
- The detector is deliberately conservative: full-image matches require all 64 bits and at least 75% average agreement; reference-assisted crops require at least 48 observed bits and the same agreement threshold. Some genuinely marked images will fail.
- Evidence archives reject symbolic links and directory junctions rather than following them outside the submission tree. Archive checking also flags extra files and modified file sizes. The manifest is not cryptographically authenticated.
- This version provides no cryptographic signing of manifests; use an independent timestamp or signing solution to strengthen evidence.
- This project does not automatically establish rights, copyright infringement, compensation owed, a fake job posting, or an employer's intent.

## Synthetic watermark robustness diagnostics

The optional **local** smoke-test executable can generate `watermark-robustness.json` with `--report`; the report stays on your computer. It runs the keyed detector against **synthetic** gradient and noise images, including unmarked, wrong-ID and wrong-secret controls, JPEG compression (quality 90 and 70), grayscale conversion, cropped images (including unaligned and one-pixel crops) with and without the verified marked-image reference, and 75% resizing with that reference. The report includes matched bits, detection results and marked-image PSNR.

These observations are **diagnostics, not proof of resilience or statistically valid error rates**. Synthetic images cannot estimate real-world false-positive or false-negative rates, and transformation failures do not necessarily fail CI. The untransformed marked PNG and all synthetic negative controls for both image patterns are now required to pass; transformed-image outcomes remain diagnostic. The synthetic report contains no candidate submissions, private artwork or real secrets.

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
- `verify --reference originally-marked.png` first validates the reference's keyed watermark, then searches limited pixel-level crop offsets (up to 64 pixels removed per axis) and finally attempts resize recovery when aspect ratios match. The reference must be the originally submitted marked image, not the unmarked original. Crop offsets can be ambiguous; these are heuristics, not validated screenshot or arbitrary crop robustness. Verification remains keyed; unknown-key public detection is not implemented.
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

**Verification status:** Hosted CI has been removed. Build and run the local smoke tests yourself before treating a revision as verified. Changes made after the previous hosted build have **not** been compiled or tested in this review. Watermark robustness and forensic validity remain unproven; do not treat v0.2 as production- or court-ready. The original v0.1 instructions using `--key` are obsolete for v0.2.

### Review pass log

1. **Inspection:** Identified CLI key exposure, exact-pixel-grid dependency, memory-heavy file hashing, missing code comparison, and easily misleading aggregate confidence.
2. **Correction:** Replaced CLI secret with environment input, added optional resize grid recovery and weakest-bit checks, added Roslyn syntax comparison, changed file hashing to streaming.
3. **Reevaluation:** Added aspect-ratio rejection to avoid silently stretching suspected evidence; checked syntax/token comparison assumptions and called out non-semantic matching. Static review only; build, false-positive studies, and regression tests remain outstanding.

### Important limitations

The image method rejects transparent pixels but still has known limitations with clipped RGB values, altered geometry, JPEG damage, and adversarial removal. Archive manifests do not carry trustworthy timestamps or digital signatures and may be manipulated; the evidence archive is **integrity-checking assistance, not chain-of-custody proof**. Do not rely on the tool alone in legal proceedings.
## Manual local verification (no hosted CI)

No GitHub Actions workflows or paid hosted build services are configured. From the repository root, on a machine with the .NET 10 SDK installed, run the following **locally**. Package restore may need to download the open-source dependencies unless they are already cached; it does not configure a hosted build service.

```powershell
dotnet restore InterviewFingerprint.slnx
dotnet build InterviewFingerprint.slnx --configuration Release --no-restore
dotnet publish src/InterviewFingerprint/InterviewFingerprint.csproj --configuration Release --no-restore --output artifacts/publish
dotnet run --project tests/InterviewFingerprint.SmokeTests --configuration Release --no-build -- artifacts/publish/InterviewFingerprint.dll --report artifacts/watermark-robustness.json
```

Smoke tests cover keyed PNG marking/verification, rejection of wrong submission IDs, syntax fingerprints, file manifest round trips, rejection of attempts to overwrite an existing marked image or manifest, extra-file and size tampering detection, path validation, and Linux symbolic-link rejection. They do not establish resilience to editing, cropping, screenshots or adversarial attacks. The commands above have **not** been executed as part of this review; no local .NET SDK was available.

**Output safety:** `mark` and `archive` now refuse to replace existing output files. Choose a new output filename for each run, and preserve the originally submitted version and manifest.
