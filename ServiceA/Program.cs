using System.Text;
using Dapr.Client;
using Grpc.Core;
using Grpc.Net.Client;
using ProxyStreamer.Grpc;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLogging();
builder.Services.AddHttpLogging();

var app = builder.Build();

app.MapGet("/start-dapr-stream", async (HttpContext context, ILogger<Program> logger) =>
{
    var channel = GrpcChannel.ForAddress("http://localhost:50001", new GrpcChannelOptions
    {
        HttpHandler = new SocketsHttpHandler
        {
            EnableMultipleHttp2Connections = true
        }
    });   
    
    // Create the gRPC client
    var client = new ProxyStreamer.Grpc.ProxyStreamer.ProxyStreamerClient(channel);

    // Metadata to route via Dapr to service-b
    var metadata = new Metadata
    {
        { "dapr-app-id", "service-b" },
        { "dapr-stream", "true" }
    };

    // Call the streaming method
    using var call = client.GetStream(new StreamRequest(), metadata);

    await foreach (var response in call.ResponseStream.ReadAllAsync())
    {
        // Stream data to HTTP response
        await context.Response.WriteAsync(response.Data + "\n");
    }});

await app.RunAsync();