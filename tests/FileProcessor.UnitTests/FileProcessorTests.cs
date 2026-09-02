using FileProcessor.Application.Configuration;
using FileProcessor.Application.Models;
using FileProcessor.Application.Services;
using FileProcessor.UnitTests.Fakes;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FileProcessor.UnitTests;

public sealed class FileProcessorTests
{
    private readonly FakeStorageProvider _storage = new();

    [Fact]
    public async Task ProcessAsync_ValidFile_ProcessesFile()
    {
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(CreateRequest());

        result.WasSkipped.Should().BeFalse();
        result.BucketName.Should().Be("files-bucket");
        result.OriginalKey.Should().Be("incoming/file.pdf");
        _storage.Operations.Should().StartWith("metadata");
    }

    [Fact]
    public async Task ProcessAsync_ValidFile_GeneratesProcessedKey()
    {
        var processor = CreateProcessor();

        var result = await processor.ProcessAsync(CreateRequest());

        result.ProcessedKey.Should().Be("processed/file.pdf");
    }

    [Fact]
    public async Task ProcessAsync_FileAlreadyProcessed_DoesNotProcessAgain()
    {
        var processor = CreateProcessor();
        var request = CreateRequest() with { ObjectKey = "processed/file.pdf" };

        var result = await processor.ProcessAsync(request);

        result.WasSkipped.Should().BeTrue();
        _storage.Operations.Should().BeEmpty();
    }

    [Fact]
    public async Task ProcessAsync_WhenMoving_CopiesBeforeDeleting()
    {
        var processor = CreateProcessor();

        await processor.ProcessAsync(CreateRequest());

        _storage.Operations.Should().ContainInOrder("copy:processed/file.pdf", "delete:incoming/file.pdf");
    }

    [Fact]
    public async Task ProcessAsync_WhenCopyFails_DoesNotDelete()
    {
        _storage.FailCopy = true;
        var processor = CreateProcessor();

        var action = () => processor.ProcessAsync(CreateRequest());

        await action.Should().ThrowAsync<InvalidOperationException>();
        _storage.Operations.Should().ContainInOrder("metadata", "copy:processed/file.pdf");
        _storage.Operations.Should().NotContain(operation => operation.StartsWith("delete:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ProcessAsync_WhenCopyFails_PropagatesSameError()
    {
        _storage.FailCopy = true;
        var processor = CreateProcessor();

        var action = () => processor.ProcessAsync(CreateRequest());

        var exception = await action.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(_storage.CopyException);
    }

    [Fact]
    public async Task ProcessAsync_CustomPrefix_UsesConfiguredPrefix()
    {
        var processor = CreateProcessor("archive/");

        var result = await processor.ProcessAsync(CreateRequest());

        result.ProcessedKey.Should().Be("archive/file.pdf");
    }

    [Theory]
    [InlineData("", "incoming/file.pdf")]
    [InlineData("files-bucket", "")]
    public async Task ProcessAsync_MissingRequiredArgument_Throws(
        string bucketName,
        string objectKey)
    {
        var processor = CreateProcessor();
        var request = CreateRequest() with { BucketName = bucketName, ObjectKey = objectKey };

        var action = () => processor.ProcessAsync(request);

        await action.Should().ThrowAsync<ArgumentException>();
        _storage.Operations.Should().BeEmpty();
    }

    private FileProcessor.Application.Services.FileProcessor CreateProcessor(string processedPrefix = "processed/") =>
        new(
            _storage,
            NullLogger<FileProcessor.Application.Services.FileProcessor>.Instance,
            Options.Create(new FileProcessingOptions
            {
                ProcessedPrefix = processedPrefix,
                MoveFiles = true
            }));

    private static FileProcessRequest CreateRequest() =>
        new("files-bucket", "incoming/file.pdf", 42, DateTimeOffset.Parse("2026-01-01T12:00:00Z"));
}
