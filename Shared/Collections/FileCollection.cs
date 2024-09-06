namespace Shared.Collections;

public class FileCollection(string context, string hash) : Collection("file")
{
    public enum FileStatus
    {
        NotProcessed,
        Processing,
        ProcessingFailed,
        Processed
    }

    protected override string GetPartitionKeyValue() => Context;
    public string Context { get; set; } = context;

    public FileStatus Status { get; set; } = FileStatus.NotProcessed;

    public string Hash { get; set; } = hash;
    
    public string Name { get; set; }
    
    public int Pages { get; set; }
    public int ProcessedPages { get; set; }
    public int Chunks { get; set; }
}