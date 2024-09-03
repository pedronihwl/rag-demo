using System.Net;
using System.Text.Json.Nodes;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;
using Shared;

namespace Backend.Extensions;

internal static class WebappConfiguration
{
    internal static WebApplication AddApi(this WebApplication app)
    {
        var api = app.MapGroup("api");

        api.MapGet("/context/{id}", OnGetContext);

        api.MapDelete("/files/{id}", OnDeleteFile);

        api.MapGet("/fragments/{contextId}", OnDownloadFragments);

        api.MapPost("/chat/{contextId}", OnChat);
        
        api.MapPost("/context", OnPostContext)
            .DisableAntiforgery();
        
        api.MapPost("/context/{id}/files", OnPostFile)
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> OnDownloadFragments(
        string contextId, 
        [FromQuery] string fonts,
        [FromServices] AzureContextService service,
        CancellationToken cancelToken)
    {
        return TypedResults.File(
            await service.DownloadFragmentAsync(fonts.Split(","), contextId, cancelToken), 
            "application/pdf", 
            $"{Guid.NewGuid().ToString("N")[..8]}.pdf"
            );
    }

    private static async Task<IResult> OnChat(
        string contextId, 
        ChatRequest request,
        [FromServices] AzureContextService service, 
        CancellationToken cancelToken)
    {

        var result = await service.ChatAsync(request, contextId, cancelToken);
        
        return TypedResults.Ok(result ?? "");
    }

    private static Task<IResult> OnDeleteFile(
        string id,
        [FromQuery] string context,
        [FromServices] AzureContextService service,
        CancellationToken cancelToken)
    {
        _ = service.DeleteFile(id, context, cancelToken);
        return Task.FromResult<IResult>(TypedResults.NoContent());
    }

    private static async Task<IResult> OnGetContext(
        string id,
        HttpContext context,
        [FromServices] AzureContextService service,
        CancellationToken cancelToken)
    {
        var baseUri = $"{context.Request.Scheme}://{context.Request.Host}";
        var ctx = await service.GetContext(id, baseUri + "/api/files/{0}", cancelToken);

        return TypedResults.Ok(ctx);
    }

    private static async Task<IResult> OnPostFile(
        string id, [FromForm] 
        IFormFileCollection files,
        [FromServices] AzureContextService service,
        CancellationToken cancelToken)
    {
        if (files.Count > 1)
        {
            throw new ArgumentException("One file per time");
        }

        var file = await service.AddFileAsync(id, files, cancelToken);

        var uri = new Uri($"/api/context/{id}", UriKind.Relative);
        return TypedResults.Created(uri,file);
    }

    private static async Task<IResult> OnPostContext(
        [FromForm] IFormFileCollection files,
        [FromServices] ILoggerFactory loggerFactory,
        [FromServices] AzureContextService service,
        CancellationToken cancelToken
    )
    {

        if (files.Any(file => Path.GetExtension(file.FileName).ToLower() != ".pdf"))
        {
            throw new ArgumentException("Accepts only .pdf files");
        }

        var context = await service.PostContextAsync(files, cancelToken);

        var uri = new Uri($"/api/context/{context.Id}", UriKind.Relative);
        return TypedResults.Created(uri, context);
    }
    
}