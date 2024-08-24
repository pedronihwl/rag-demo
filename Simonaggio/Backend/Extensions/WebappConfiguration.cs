using System.Net;
using Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace Backend.Extensions;

internal static class WebappConfiguration
{
    internal static WebApplication AddApi(this WebApplication app)
    {
        var api = app.MapGroup("api");

        api.MapGet("/context/{id}", OnGetContext);

        api.MapDelete("/files/{id}", OnDeleteFile);

        api.MapPost("/chat/{contextId}", OnChat);
        
        api.MapPost("/context", OnPostContext)
            .DisableAntiforgery();
        
        api.MapPost("/context/{id}/files", OnPostFile)
            .DisableAntiforgery();

        return app;
    }

    private static async Task<IResult> OnChat(string contextId)
    {
        return TypedResults.Ok("");
    }

    private static Task<IResult> OnDeleteFile(
        string id,
        [FromQuery] string context,
        [FromServices] AzureContextService service,
        CancellationToken cancelToken)
    {
        service.DeleteFile(id, context, cancelToken);
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