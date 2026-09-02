using System.Net;
using Amazon.Lambda.Core;
using Amazon.Lambda.S3Events;
using Amazon.S3;
using FileProcessor.Application;
using FileProcessor.Application.Abstractions;
using FileProcessor.Application.Configuration;
using FileProcessor.Application.Models;
using FileProcessor.Function.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileProcessor.Function;

public sealed class Function
{
    private readonly IFileProcessor _fileProcessor;
    private readonly ILogger<Function> _logger;

    public Function() : this(CreateServiceProvider())
    {
    }

    public Function(IServiceProvider serviceProvider)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _fileProcessor = serviceProvider.GetRequiredService<IFileProcessor>();
        _logger = serviceProvider.GetRequiredService<ILogger<Function>>();
    }

    public async Task FunctionHandler(S3Event s3Event, ILambdaContext context)
    {
        ArgumentNullException.ThrowIfNull(s3Event);
        ArgumentNullException.ThrowIfNull(context);

        using var cancellationTokenSource = context.RemainingTime > TimeSpan.FromSeconds(1)
            ? new CancellationTokenSource(context.RemainingTime - TimeSpan.FromSeconds(1))
            : new CancellationTokenSource(TimeSpan.Zero);
        var cancellationToken = cancellationTokenSource.Token;

        foreach (var record in s3Event.Records ?? [])
        {
            cancellationToken.ThrowIfCancellationRequested();
            var bucketName = record.S3.Bucket.Name;
            var objectKey = WebUtility.UrlDecode(record.S3.Object.Key);

            try
            {
                _logger.LogInformation(
                    "📥 S3 event received. RequestId: {RequestId}, Bucket: {BucketName}, Key: {ObjectKey}",
                    context.AwsRequestId,
                    bucketName,
                    objectKey);

                var request = new FileProcessRequest(bucketName, objectKey, record.S3.Object.Size, record.EventTime);
                await _fileProcessor.ProcessAsync(request, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "❌ File processing failed. RequestId: {RequestId}, Bucket: {BucketName}, Key: {ObjectKey}",
                    context.AwsRequestId,
                    bucketName,
                    objectKey);
                throw;
            }
        }
    }

    private static ServiceProvider CreateServiceProvider()
    {
        var services = new ServiceCollection();

        services.AddLogging(builder => builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-ddTHH:mm:ss.fffZ ";
            options.UseUtcTimestamp = true;
        }));
        services.AddSingleton<IAmazonS3, AmazonS3Client>();
        services.AddSingleton<IStorageProvider, AwsS3StorageProvider>();
        services.Configure<FileProcessingOptions>(options =>
        {
            options.ProcessedPrefix = Environment.GetEnvironmentVariable("PROCESSED_PREFIX")
                ?? FileProcessingOptions.DefaultProcessedPrefix;
            options.MoveFiles = !bool.TryParse(Environment.GetEnvironmentVariable("MOVE_FILES"), out var moveFiles) || moveFiles;
        });
        services.AddFileProcessing();

        return services.BuildServiceProvider();
    }
}
