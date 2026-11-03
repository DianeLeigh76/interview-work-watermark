# Local test validation and interpretation

This document describes how to interpret automated test results without overstating the evidence. The project is experimental; a passing test suite is not forensic validation.

## Run locally

Install the .NET 10 SDK. From the repository root:

```powershell
dotnet restore InterviewFingerprint.slnx
dotnet build InterviewFingerprint.slnx --configuration Release --no-restore
dotnet test InterviewFingerprint.slnx --configuration Release --no-restore
```

The standalone smoke-test executable is separate from the xUnit suite. See README.md for its publish-and-run command. Do not configure GitHub Actions or hosted runners to run these tests.

## Three independent review checkpoints

1. **Input and fixture integrity:** Use only synthetic or redistributable fixture files. Generate fresh temporary working copies, retain immutable expected inputs, and independently compute exact byte hashes for lossless operations. Confirm a negative control (wrong ID, wrong key, altered evidence, or unmarked image) is rejected.
2. **Behavior and threshold interpretation:** Require exact equality where the operation promises a lossless round trip. For intentionally lossy transformations, record the raw measurement and the explicit acceptance threshold separately. A diagnostic similarity percentage is not a probability of authorship or a cryptographic confidence score. Do not silently change production verification thresholds to make a test pass.
3. **Failure and provenance review:** Verify tests would detect a deliberately changed input or expected digest. Preserve failure details and reproducible seeds without exposing secrets. Do not label an unexecuted test as passing, and do not treat matching behavior alone as evidence of copying or legal infringement.

## Suggested results vocabulary

- **Exact:** all required bytes, hashes, or recovered payload bits match the specified lossless contract.
- **Threshold met:** a declared *diagnostic* similarity metric meets its configured minimum; include numerator, denominator, algorithm, and threshold in the report.
- **Below threshold:** the diagnostic metric falls below its configured minimum. Preserve the observed score rather than presenting it as a catastrophic failure.
- **Inconclusive:** the input cannot be measured reliably, or there is insufficient observable signal. Do not convert this into a positive match.
- **Invalid input:** malformed, unsafe, or unsupported input was rejected.

These are reporting concepts, not claims that the current CLI implements all five result categories.

## Important distinctions

Compression and encoding are not automatically corruption. Apply only documented, deterministic decoding before comparing recovered content; never accept arbitrary fuzzy matches as exact recovered secrets. A 75% similarity threshold and a 90% threshold describe different test policies, not different historical facts. Record which threshold was used for each result, and retain the measured score so a future reviewer can reevaluate it.

A code comparison can highlight matching tokens, control flow, or unusual edge cases. Independent implementations may still produce similar results. A genuine clean-room claim requires process evidence beyond this tool's measurements.

## Verification record

For each local run, retain the SDK version, operating system, command line (excluding secrets), test counts, failing test names, and any synthetic diagnostic report. Do not commit private submissions, keys, evidence records, or generated reports. The presence of this document does not mean the suite was compiled or executed.
