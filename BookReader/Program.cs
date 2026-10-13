using BookReader.Data;
using BookReader.Extentions;
using BookReader.Services;
using BookReader.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using S3Storage;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<IAuthService, AuthService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Secret"]!)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddApiDocumentation();

builder.Services.AddSingleton<UserSevice>();/// пробник Удалить


builder.AddNpgsqlDbContext<ApplicationDbContext>("bookdb");


var connStrBooks = builder.Configuration.GetConnectionString("rustfs")
    ?? throw new InvalidOperationException("Не найдена connection string rustfs-books");



var partsBooks = connStrBooks.Split(';', StringSplitOptions.RemoveEmptyEntries)
    .Select(p => p.Split('=', 2))
    .ToDictionary(x => x[0].Trim(), x => x.Length > 1 ? x[1].Trim() : "");



var endpointUriBooks = new Uri(partsBooks["Endpoint"]);
var accessKeyBooks = partsBooks["AccessKey"];
var secretKeyBooks = partsBooks["SecretKey"];



var s3Client = S3ClientFactory.ForRustFS(
    host: endpointUriBooks.Host,
    accessKey: accessKeyBooks,
    secretKey: secretKeyBooks,
    port: endpointUriBooks.Port,
    useSSL: endpointUriBooks.Scheme == Uri.UriSchemeHttps);


builder.Services.AddSingleton<IFileStorageService, S3FileStorageService>();


builder.Services.AddSingleton<IS3Client>(s3Client);
var app = builder.Build();

try
{
    using var scope = app.Services.CreateScope();
    var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();
    await storage.EnsureBucketAsync();
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "S3 storage недоступно при старте — файлы временно не работают");
}

app.MapDefaultEndpoints();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI();


    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
}
app.UseAuthentication();


app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
