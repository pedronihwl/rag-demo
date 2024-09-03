namespace Shared.Collections;

public class PageDetail
{
    public int Index { get; set; }
    
    
}

public class Fragment() : Collection("frag")
{
    public string Text { get; set; }
    
    public float[] Embeddings { get; set; }
    
    public string File { get; set; }
    
    public string Context { get; set; }
    
    public int Index { get; set; }
    
    public int Offset { get; set; }

    protected override string GetPartitionKeyValue() => File;
}