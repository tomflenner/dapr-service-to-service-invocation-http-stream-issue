using System.Text;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddHttpLogging();

builder.Services.AddHttpClient();

var app = builder.Build();

app.MapGet("/start-dapr-stream", async (HttpContext context, ILogger<Program> logger, IHttpClientFactory httpClientFactory) =>
{
    await ProxyStreamAsync(context, logger, httpClientFactory, "http://localhost:3500/v1.0/invoke/service-b/method/proxy-stream");
});

app.MapGet("/start-non-dapr-stream", async (HttpContext context, ILogger<Program> logger, IHttpClientFactory httpClientFactory) =>
{
    await ProxyStreamAsync(context, logger, httpClientFactory, "http://service-b:8000/proxy-stream");
});

await app.RunAsync();

static async Task ProxyStreamAsync(HttpContext context, ILogger logger, IHttpClientFactory clientFactory, string url)
{
    var client = clientFactory.CreateClient();

    using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, context.RequestAborted);

    LogHeaders(logger, response);

    context.Response.ContentType = "text/event-stream";
    context.Response.Headers.CacheControl = "no-cache";
    context.Response.Headers.Connection = "keep-alive";

    await using var stream = await response.Content.ReadAsStreamAsync(context.RequestAborted);
    using var reader = new StreamReader(stream, Encoding.UTF8);

    while (!context.RequestAborted.IsCancellationRequested)
    {
        string? line;
        try
        {
            line = await reader.ReadLineAsync().WaitAsync(context.RequestAborted);
        }
        catch (OperationCanceledException)
        {
            break;
        }

        if (line is null)
            break;

        logger.LogInformation("[Proxy Stream] {Line}", line);

        var buffer = Encoding.UTF8.GetBytes(line + "\n");
        await context.Response.Body.WriteAsync(buffer, context.RequestAborted);
        await context.Response.Body.FlushAsync(context.RequestAborted);
    }
}

static void LogHeaders(ILogger logger, HttpResponseMessage response)
{
    foreach (var header in response.Headers)
        logger.LogInformation("{Header}: {Value}", header.Key, string.Join(", ", header.Value));

    foreach (var header in response.Content.Headers)
        logger.LogInformation("{Header}: {Value}", header.Key, string.Join(", ", header.Value));
}
