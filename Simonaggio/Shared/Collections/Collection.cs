using System.Text.Json.Serialization;

namespace Shared.Collections;

public abstract class Collection : ICollection
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    
    [JsonIgnore]
    public string Suffix { get; protected set; }
    
    string ICollection.PartitionKey => GetPartitionKeyValue();
    protected virtual string GetPartitionKeyValue() => Id;

    protected Collection(string suffix = "col")
    {
        Suffix = suffix;
        Id = Suffix + "_" + Guid.NewGuid().ToString("N")[..8];
    }
}