using FileProcessor.Application.Abstractions;
using FileProcessor.Application.Configuration;
using FileProcessor.Application.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileProcessor.Application.Services;

public sealed class FileProcessor : IFileProcessor
{
    private readonly IStorageProvider _storageProvider;
    private readonly ILogger<FileProcessor> _logger;
    private readonly string _processedPrefix;
    private readonly bool _moveFiles;

    public FileProcessor(IStorageProvider storageProvider, ILogger<FileProcessor> logger, IOptions<FileProcessingOptions> options)
    {
        ArgumentNullException.ThrowIfNull(storageProvider);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.Value.ProcessedPrefix))
        {
            throw new ArgumentException("Processed prefix is required.", nameof(options));
        }

        _storageProvider = storageProvider;
        _logger = logger;
        _processedPrefix = NormalizePrefix(options.Value.ProcessedPrefix);
        _moveFiles = options.Value.MoveFiles;
    }

    public async Task<FileProcessResult> ProcessAsync(FileProcessRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);
        cancellationToken.ThrowIfCancellationRequested();

        if (request.ObjectKey.StartsWith(_processedPrefix, StringComparison.Ordinal))
        {
            _logger.LogInformation("⏭️ File skipped because it is already processed. Key: {ObjectKey}", request.ObjectKey);
            return new FileProcessResult(request.BucketName, request.ObjectKey, null, DateTimeOffset.UtcNow, true);
        }

        _logger.LogInformation("📦 Processing file. Bucket: {BucketName}, Key: {ObjectKey}", request.BucketName, request.ObjectKey);

        var metadata = await _storageProvider.GetMetadataAsync(request.BucketName, request.ObjectKey, cancellationToken);
        
        _logger.LogDebug(
            "Object metadata loaded. Bucket: {BucketName}, Key: {ObjectKey}, Size: {Size}, ContentType: {ContentType}, EventTime: {EventTime}",
            request.BucketName,
            request.ObjectKey,
            metadata.Size,
            metadata.ContentType,
            request.EventTime);

        string? processedKey = null;
        
        if (_moveFiles)
        {
            processedKey = BuildProcessedKey(request.ObjectKey);
            _logger.LogInformation("📤 Copying file. Source: {SourceKey}, Destination: {DestinationKey}", request.ObjectKey, processedKey);

            await _storageProvider.CopyAsync(
                request.BucketName,
                request.ObjectKey,
                request.BucketName,
                processedKey,
                cancellationToken);

            _logger.LogInformation("✅ File copied successfully. Destination: {DestinationKey}", processedKey);
            _logger.LogInformation("🗑️ Deleting original file. Key: {ObjectKey}", request.ObjectKey);

            await _storageProvider.DeleteAsync(request.BucketName, request.ObjectKey, cancellationToken);
        }
        else
        {
            _logger.LogWarning("⚠️ File move is disabled. File will remain at {ObjectKey}", request.ObjectKey);
        }

        var result = new FileProcessResult(
            request.BucketName,
            request.ObjectKey,
            processedKey,
            DateTimeOffset.UtcNow,
            false);

        _logger.LogInformation(
            "✅ File processed successfully. OriginalKey: {OriginalKey}, ProcessedKey: {ProcessedKey}",
            result.OriginalKey,
            result.ProcessedKey);

        return result;
    }

    private string BuildProcessedKey(string objectKey)
    {
        var separatorIndex = objectKey.IndexOf('/');
        var relativeKey = separatorIndex >= 0 ? objectKey[(separatorIndex + 1)..] : objectKey;
        return _processedPrefix + relativeKey;
    }

    private static string NormalizePrefix(string prefix) => prefix.Trim().Trim('/') + "/";

    private static void Validate(FileProcessRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.BucketName))
        {
            throw new ArgumentException("Bucket name is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.ObjectKey))
        {
            throw new ArgumentException("Object key is required.", nameof(request));
        }
    }
}
