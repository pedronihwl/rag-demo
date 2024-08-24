using Azure.Identity;
using Backend.Extensions;
using EmbedFunction;
using Microsoft.Extensions.Azure;


// Document Inteligence
// Cosmos DB
// Blob Storage
var builder = Host.CreateApplicationBuilder(args);


builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();