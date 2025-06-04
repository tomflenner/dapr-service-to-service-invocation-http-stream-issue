using System.Text;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddHttpLogging();

var app = builder.Build();

app.MapGet("/start-dapr-stream", async (HttpContext context, ILogger<Program> logger) =>
{
    await ProxyStreamAsync(context, logger, "http://localhost:3500/v1.0/invoke/service-b/method/proxy-stream");
});

app.MapGet("/start-non-dapr-stream", async (HttpContext context, ILogger<Program> logger) =>
{
    await ProxyStreamAsync(context, logger, "http://service-b:8000/proxy-stream");
});

await app.RunAsync();

static async Task ProxyStreamAsync(HttpContext context, ILogger logger, string url)
{
    using var client = new HttpClient();

    var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);

    LogHeaders(logger, response);

    context.Response.ContentType = "text/plain; charset=utf-8";
    context.Response.Headers.CacheControl = "no-cache";

    await using var stream = await response.Content.ReadAsStreamAsync();
    using var reader = new StreamReader(stream, Encoding.UTF8);

    while (!context.RequestAborted.IsCancellationRequested)
    {
        var line = await reader.ReadLineAsync();
        if (line is null)
            break;

        logger.LogInformation("[Proxy Stream] {Line}", line);

        var buffer = Encoding.UTF8.GetBytes(line + "\n");
        await context.Response.Body.WriteAsync(buffer, 0, buffer.Length, context.RequestAborted);
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
