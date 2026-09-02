using FileProcessor.Application.Models;

namespace FileProcessor.Application.Abstractions;

public interface IFileProcessor
{
    Task<FileProcessResult> ProcessAsync(
        FileProcessRequest request,
        CancellationToken cancellationToken = default);
}
