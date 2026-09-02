using FileProcessor.Application.Models;

namespace FileProcessor.Application.Abstractions;

public interface IStorageProvider
{
    Task<StorageObjectMetadata> GetMetadataAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);

    Task CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default);
}
