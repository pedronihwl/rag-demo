namespace Shared.Collections;

public class FileCollection(string context, string hash) : Collection("file")
{
    public enum FileStatus
    {
        NOT_PROCESSED,
        PROCESSING,
        PROCESSED
    }

    protected override string GetPartitionKeyValue() => Context;
    public string Context { get; set; } = context;

    public FileStatus Status { get; set; } = FileStatus.NOT_PROCESSED;

    public string Hash { get; set; } = hash;
    
    public string Name { get; set; }
    
    public int Pages { get; set; }
    public int ProcessedPages { get; set; }
    public int Chunks { get; set; }
}