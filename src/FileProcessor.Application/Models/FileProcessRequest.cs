namespace FileProcessor.Application.Models;

public sealed record FileProcessRequest(
    string BucketName,
    string ObjectKey,
    long Size,
    DateTimeOffset EventTime);
