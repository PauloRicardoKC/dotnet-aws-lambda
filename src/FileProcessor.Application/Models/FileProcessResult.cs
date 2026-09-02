namespace FileProcessor.Application.Models;

public sealed record FileProcessResult(
    string BucketName,
    string OriginalKey,
    string? ProcessedKey,
    DateTimeOffset ProcessedAt,
    bool WasSkipped);
