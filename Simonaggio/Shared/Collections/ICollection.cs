namespace Shared.Collections;

public interface ICollection
{
    string Id { get; set; }
    
    string PartitionKey { get; }
    
}