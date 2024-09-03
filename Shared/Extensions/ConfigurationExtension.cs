using Azure.Identity;
using Microsoft.Extensions.Configuration;

namespace Shared.Extensions;

public static class ConfigurationExtension
{
    public static IConfigurationBuilder ConfigureAzureKeyVault(this IConfigurationBuilder builder, DefaultAzureCredential credential)
    {
        var azureKeyVaultEndpoint = Environment.GetEnvironmentVariable("AZURE_KEY_VAULT_ENDPOINT") 
                                    ?? throw new InvalidOperationException("Azure Key Vault endpoint is not set.");
        
        ArgumentException.ThrowIfNullOrEmpty(azureKeyVaultEndpoint);
        
        builder.AddAzureKeyVault(new Uri(azureKeyVaultEndpoint), credential);

        return builder;
    }
}