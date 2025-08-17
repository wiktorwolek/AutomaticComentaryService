using System.Diagnostics;
using AutomaticComentaryService.Services;
using Microsoft.AspNetCore.HttpLogging;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Mvc.NewtonsoftJson;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 104_857_600; // 100 MB
});

builder.Services
    .AddControllers()
    .AddNewtonsoftJson(options =>
    {
        options.SerializerSettings.NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore;
        options.SerializerSettings.Formatting = Newtonsoft.Json.Formatting.None;
        options.SerializerSettings.CheckAdditionalContent = false;
        options.SerializerSettings.DateParseHandling = Newtonsoft.Json.DateParseHandling.None;
        options.SerializerSettings.MetadataPropertyHandling = Newtonsoft.Json.MetadataPropertyHandling.Ignore;
    });

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});
builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});



builder.Services.AddSingleton<ITTSEngine, OpenTtsEngine>();
builder.Services.AddHttpClient("ollama");
builder.Services.AddSingleton<IOllamaClient, OllamaClient>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Logging.ClearProviders();

var app = builder.Build();


if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.Use(async (context, next) =>
{
    context.Request.EnableBuffering(); 
    await next.Invoke();
});


app.Use(async (context, next) =>
{

    if (context.Request.Headers.TryGetValue("Content-Encoding", out var encoding) &&
        encoding.ToString().Equals("gzip", StringComparison.OrdinalIgnoreCase))
    {
        // Buffer the gzip stream to memory
        var originalStream = context.Request.Body;
        using var decompressedStream = new GZipStream(originalStream, CompressionMode.Decompress);
        var memoryStream = new MemoryStream();
        await decompressedStream.CopyToAsync(memoryStream);
        memoryStream.Seek(0, SeekOrigin.Begin);
        context.Request.Body = memoryStream;
    }


    await next();
});

app.MapControllers();

app.Run();
