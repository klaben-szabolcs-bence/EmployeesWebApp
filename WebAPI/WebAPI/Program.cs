using Microsoft.Extensions.FileProviders;
using Newtonsoft.Json.Serialization;
using WebAPI.Data;

var builder = WebApplication.CreateBuilder(args);

// Hosting platforms (Render, Cloud Run, Container Apps) inject the port to bind
// and require 0.0.0.0. This is read here rather than set as ASPNETCORE_URLS in
// the Dockerfile, because $PORT would not be interpolated at container runtime.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

const string CorsPolicy = "AllowFrontend";
var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? Array.Empty<string>();

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            // No origins configured: local development only.
            policy.AllowAnyOrigin();
        }
        else
        {
            policy.WithOrigins(allowedOrigins);
        }

        policy.AllowAnyMethod().AllowAnyHeader();
    });
});

// Newtonsoft is required: the controllers return DataTable via JsonResult, which
// System.Text.Json cannot serialise.
builder.Services.AddControllers().AddNewtonsoftJson(options =>
{
    options.SerializerSettings.ReferenceLoopHandling = Newtonsoft.Json.ReferenceLoopHandling.Ignore;
    options.SerializerSettings.ContractResolver = new DefaultContractResolver();
});

builder.Services.AddSingleton<IStoragePaths, StoragePaths>();
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddSingleton<SqliteDatabaseInitializer>();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.Services.GetRequiredService<SqliteDatabaseInitializer>().Initialize();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// Before UseStaticFiles so /Photos responses also carry CORS headers.
app.UseCors(CorsPolicy);

var photosPath = app.Services.GetRequiredService<IStoragePaths>().PhotosPath;
Directory.CreateDirectory(photosPath);
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(photosPath),
    RequestPath = "/Photos"
});

app.MapControllers();

app.Run();
