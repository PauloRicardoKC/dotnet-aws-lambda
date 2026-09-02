using Amazon.S3;
using Amazon.S3.Model;
using FileProcessor.Application.Abstractions;
using FileProcessor.Application.Models;

namespace FileProcessor.Function.Infrastructure;

public sealed class AwsS3StorageProvider : IStorageProvider
{
    private readonly IAmazonS3 _s3Client;

    public AwsS3StorageProvider(IAmazonS3 s3Client)
    {
        ArgumentNullException.ThrowIfNull(s3Client);
        _s3Client = s3Client;
    }

    public async Task<StorageObjectMetadata> GetMetadataAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        var response = await _s3Client.GetObjectMetadataAsync(
            new GetObjectMetadataRequest { BucketName = bucketName, Key = objectKey },
            cancellationToken);

        return new StorageObjectMetadata(response.ContentLength, response.Headers.ContentType);
    }

    public Task CopyAsync(
        string sourceBucketName,
        string sourceObjectKey,
        string destinationBucketName,
        string destinationObjectKey,
        CancellationToken cancellationToken = default) =>
        _s3Client.CopyObjectAsync(
            new CopyObjectRequest
            {
                SourceBucket = sourceBucketName,
                SourceKey = sourceObjectKey,
                DestinationBucket = destinationBucketName,
                DestinationKey = destinationObjectKey
            },
            cancellationToken);

    public Task DeleteAsync(
        string bucketName,
        string objectKey,
        CancellationToken cancellationToken = default) =>
        _s3Client.DeleteObjectAsync(
            new DeleteObjectRequest { BucketName = bucketName, Key = objectKey },
            cancellationToken);
}