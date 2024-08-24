namespace Shared.Options;

public class AzureContextOptions
{
    public string DbCollection { get; set; } = "azureServiceOptions:dbCollection";
    public string DbContextName { get; set; } = "azureServiceOptions:dbContextName";
    public string DbFileName { get; set; } = "azureServiceOptions:dbFileName";
    
    public string DbFragments { get; set; } = "azureServiceOptions:dbFragments";
    
    
}