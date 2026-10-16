using BookReader.Data;
using BookReader.Data.Models;
using BookReader.Extentions;
using BookReader.Services;
using BookReader.Storage;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using S3Storage;
using System.Security.Claims;
using System.Reflection;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddJsonFile("appsettings.Admin.json", optional: true, reloadOnChange: false);

builder.AddServiceDefaults();

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAdminUserService, AdminUserService>();

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

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = async context =>
            {
                var userIdValue = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
                var tokenVersionValue = context.Principal?.FindFirstValue("token_version");
                if (!int.TryParse(userIdValue, out var userId)
                    || !int.TryParse(tokenVersionValue, out var tokenVersion))
                {
                    context.Fail("JWT does not contain valid user state claims");
                    return;
                }

                var db = context.HttpContext.RequestServices.GetRequiredService<ApplicationDbContext>();
                var userState = await db.Users.AsNoTracking()
                    .Where(user => user.Id == userId)
                    .Select(user => new { user.IsBanned, user.TokenVersion })
                    .FirstOrDefaultAsync(context.HttpContext.RequestAborted);

                if (userState is null || userState.IsBanned || userState.TokenVersion != tokenVersion)
                    context.Fail("JWT has been revoked or the user is banned");
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Guest", policy => policy.RequireAssertion(_ => true));
    options.AddPolicy("User", policy => policy.RequireRole(
        nameof(UserRole.User), nameof(UserRole.Author), nameof(UserRole.Admin)));
    options.AddPolicy("Author", policy => policy.RequireRole(
        nameof(UserRole.Author), nameof(UserRole.Admin)));
    options.AddPolicy("Admin", policy => policy.RequireRole(nameof(UserRole.Admin)));
});
//var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? []; для разрешенных сайтов
builder.Services.AddCors(options => options.AddPolicy("Frontend", policy =>
    policy.AllowAnyOrigin() //allowedOrigins
        .AllowAnyHeader()
        .AllowAnyMethod()));

builder.Services.AddApiDocumentation();

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

if (!await s3Client.BucketExistsAsync("image"))
    await s3Client.CreateBucketAsync("image");

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

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await InitialGenreSeeder.SeedAsync(db, app.Logger);
    await InitialAdminSeeder.SeedAsync(db, app.Configuration, app.Logger);
}

app.UseRouting();
app.UseCors("Frontend");
app.UseHttpsRedirection();
app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
