using System.Net;
using System.Net.Sockets;
using Microsoft.AspNetCore.StaticFiles;
using QRCoder;

var builder = WebApplication.CreateBuilder(args);

// Enables running as a Windows Service. Also sets the content root to the
// application directory, so storage and wwwroot resolve correctly when the
// service starts with a different working directory.
builder.Host.UseWindowsService();

var urls = builder.Configuration["LOCALDROP_URLS"] ?? "http://0.0.0.0:5000";
builder.WebHost.UseUrls(urls);

var serverPort = new Uri(urls.Split(';', StringSplitOptions.RemoveEmptyEntries)[0]).Port;

var app = builder.Build();

var storagePath = builder.Configuration["LOCALDROP_STORAGE_PATH"] ?? Path.Combine(app.Environment.ContentRootPath, "Storage");
Directory.CreateDirectory(storagePath);

var contentTypeProvider = new FileExtensionContentTypeProvider();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok", utc = DateTime.UtcNow }));

app.MapGet("/api/netinfo", () => Results.Ok(new { ip = GetLanIPv4(), port = serverPort }));

app.MapGet("/api/qr", (HttpContext http) =>
{
    var target = $"http://{GetLanIPv4()}:{serverPort}";

    using var generator = new QRCodeGenerator();
    using var qrData = generator.CreateQrCode(target, QRCodeGenerator.ECCLevel.M);
    var png = new PngByteQRCode(qrData).GetGraphic(8);

    // The LAN address can change with DHCP, so a cached QR would point nowhere.
    http.Response.Headers.CacheControl = "no-store";

    return Results.File(png, "image/png");
});

app.MapGet("/api/files", () =>
{
    var files = Directory.EnumerateFiles(storagePath)
        .Select(path =>
        {
            var info = new FileInfo(path);
            return new
            {
                name = info.Name,
                sizeBytes = info.Length,
                lastModifiedUtc = info.LastWriteTimeUtc
            };
        })
        .OrderByDescending(file => file.lastModifiedUtc);

    return Results.Ok(files);
});

app.MapPost("/api/files/upload", async (IFormFile file) =>
{
    if (file.Length <= 0)
    {
        return Results.BadRequest(new { error = "Archivo vacio." });
    }

    var safeName = Path.GetFileName(file.FileName);
    if (string.IsNullOrWhiteSpace(safeName))
    {
        return Results.BadRequest(new { error = "Nombre de archivo invalido." });
    }

    var destination = Path.Combine(storagePath, safeName);
    await using var stream = File.Create(destination);
    await file.CopyToAsync(stream);

    return Results.Ok(new { message = "Archivo subido.", fileName = safeName });
})
.DisableAntiforgery();

app.MapGet("/api/files/{fileName}", (string fileName) =>
{
    if (!IsSafeFileName(fileName))
    {
        return Results.BadRequest(new { error = "Nombre de archivo invalido." });
    }

    var fullPath = Path.Combine(storagePath, fileName);
    if (!File.Exists(fullPath))
    {
        return Results.NotFound(new { error = "Archivo no encontrado." });
    }

    var contentType = contentTypeProvider.TryGetContentType(fileName, out var resolvedContentType)
        ? resolvedContentType
        : "application/octet-stream";

    return Results.File(fullPath, contentType, fileName);
});

app.MapDelete("/api/files/{fileName}", (string fileName) =>
{
    if (!IsSafeFileName(fileName))
    {
        return Results.BadRequest(new { error = "Nombre de archivo invalido." });
    }

    var fullPath = Path.Combine(storagePath, fileName);
    if (!File.Exists(fullPath))
    {
        return Results.NotFound(new { error = "Archivo no encontrado." });
    }

    File.Delete(fullPath);
    return Results.Ok(new { message = "Archivo eliminado." });
});

app.Run();

static bool IsSafeFileName(string fileName)
{
    if (string.IsNullOrWhiteSpace(fileName))
    {
        return false;
    }

    return Path.GetFileName(fileName) == fileName;
}

static string GetLanIPv4()
{
    try
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        socket.Connect("8.8.8.8", 65530);
        if (socket.LocalEndPoint is IPEndPoint endPoint)
        {
            return endPoint.Address.ToString();
        }
    }
    catch
    {
        // Fall through to loopback below.
    }

    return "127.0.0.1";
}
