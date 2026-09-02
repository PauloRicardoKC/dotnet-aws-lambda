namespace FileProcessor.Application.Configuration;

public sealed class FileProcessingOptions
{
    public const string DefaultProcessedPrefix = "processed/";

    public string ProcessedPrefix { get; set; } = DefaultProcessedPrefix;

    public bool MoveFiles { get; set; } = true;
}
