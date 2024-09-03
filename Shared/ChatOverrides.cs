using System.Text.Json.Serialization;

namespace Shared;

public enum RetrievalMode
{
    RAG,
    Assistant
}

public class ChatOverrides
{
    public RetrievalMode RetrievalMode { get; set; } = RetrievalMode.RAG;
    
    
}