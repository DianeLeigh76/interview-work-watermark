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

## Checkpoint 1: repeatable local execution

Run the solution tests on a local .NET 10 installation before interpreting any recovery result. Keep a separate, immutable copy of each synthetic input. Record the exact command, SDK version (`dotnet --version`), OS, test count and failures. A test that was only inspected or compiled is not a passing test. Keep the original standalone smoke runner separate from the xUnit results; `dotnet test` alone does not run its robustness matrix.

## Checkpoint 2: threshold calibration, not threshold guessing

Before choosing a diagnostic threshold, define the measurement: e.g., matching recovered bits divided by *observed* expected bits. Record the observed-bit denominator, the number of missing bits and the algorithm version. Do not score unobserved bits as correct, or silently ignore a low-coverage result. Require a separate minimum coverage gate; high agreement on only a handful of bits is not strong evidence. For a string metric, document whether it is edit distance, exact-character positions or something else; an 87.5% character score is not a watermark confidence score.

Run both positive controls (unchanged marked material, then declared transformations) and negative controls (unmarked material, wrong submission IDs and wrong keys) across multiple synthetic seeds and input types. Keep positives and negatives disjoint when tuning thresholds. For each sample, record the expected classification *before* running the detector, so a surprising result cannot be relabeled after inspection. Report counts as `detected / total` for each group, with false positives and false negatives separately. Do not treat repeated crops of one image as independent samples. Compare candidate thresholds such as 75% and 90% *without changing production verification logic*. Thresholds selected after inspecting a sample must be evaluated on separate held-out samples before drawing conclusions. No particular threshold is endorsed as statistically validated by this document.

## Checkpoint 3: failure classification and regression review

A failure to decode compressed or transformed content is not automatically evidence of tampering. Only apply explicitly supported reversible decoding before exact comparison; never guess encodings until something looks similar. For intentionally lossy media, preserve both raw score and the specific transformation. Classify an unsupported transform as `inconclusive/unsupported` rather than `verified`, and do not suppress actual regression failures in the test runner. In a controlled negative test, deliberately flip a payload bit, alter a digest or change an ID and confirm the assertion catches the change. If a change unexpectedly passes, investigate the detector and test oracle rather than lowering the threshold.
