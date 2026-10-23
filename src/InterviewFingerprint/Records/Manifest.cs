namespace InterviewFingerprint.Records;

internal sealed record Manifest(Int32 SchemaVersion, String SubmissionId, DateTimeOffset RecordedUtc, FileRecord[] Files);
