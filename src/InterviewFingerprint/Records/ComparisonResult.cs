namespace InterviewFingerprint;

internal sealed record ComparisonResult(Boolean SyntaxIdentical, Double Jaccard, Double Containment, Int32 TokenCountA, Int32 TokenCountB);
