using FileProcessor.Application.Abstractions;
using FileProcessor.Application.Models;

namespace FileProcessor.UnitTests.Fakes;

public sealed class FakeStorageProvider : IStorageProvider
{
    public List<string> Operations { get; } = [];

    public bool FailCopy { get; set; }

    public InvalidOperationException CopyException { get; } = new("Copy failed.");

    public Task<StorageObjectMetadata> GetMetadataAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        Operations.Add("metadata");
        return Task.FromResult(new StorageObjectMetadata(123, "application/octet-stream"));
    }

    public Task CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default)
    {
        Operations.Add($"copy:{destinationObjectKey}");
        return FailCopy
            ? Task.FromException(CopyException)
            : Task.CompletedTask;
    }

    public Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        Operations.Add($"delete:{objectKey}");
        return Task.CompletedTask;
    }
}
