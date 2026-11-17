using System;
using System.IO;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CampusLostAndFound.Data;
using CampusLostAndFound.Models;
using CampusLostAndFound.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Database Connection & Provider
var envProvider = Environment.GetEnvironmentVariable("EF_MIGRATION_PROVIDER");
var configuredProvider = !string.IsNullOrEmpty(envProvider)
    ? envProvider
    : (builder.Configuration.GetValue<string>("DatabaseProvider") ?? "SqlServer");

var sqlServerConnection = builder.Configuration.GetConnectionString("DefaultConnection") 
    ?? "Server=localhost;Database=CampusLostAndFoundDb;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;";
var sqliteConnection = builder.Configuration.GetConnectionString("SqliteConnection") 
    ?? "Data Source=CampusLostAndFound.db";

bool useSqlite = configuredProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase);

if (!useSqlite && string.IsNullOrEmpty(envProvider))
{
    try
    {
        // Probe SQL Server connection with a 1-second timeout
        var probeBuilder = new SqlConnectionStringBuilder(sqlServerConnection) { ConnectTimeout = 1 };
        using var probeConn = new SqlConnection(probeBuilder.ConnectionString);
        probeConn.Open();
        useSqlite = false;
    }
    catch
    {
        // SQL Server is not currently reachable on this machine (e.g., macOS dev environment or sandbox)
        // Seamlessly use SQLite for zero-friction local development while maintaining full SQL Server compatibility
        useSqlite = true;
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    if (useSqlite)
    {
        options.UseSqlite(sqliteConnection);
    }
    else
    {
        options.UseSqlServer(sqlServerConnection, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(maxRetryCount: 3, maxRetryDelay: TimeSpan.FromSeconds(5), errorNumbersToAdd: null);
        });
    }
});

// 2. Configure ASP.NET Core Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Academic / university password policy
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;

    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedAccount = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// 3. Configure Authentication Cookie
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.Cookie.Name = "CampusLostAndFound.Auth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
});

// 4. Register Application & Domain Services (Three-Layer Architecture)
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IMatchingService, MatchingService>();
builder.Services.AddScoped<IVerificationService, VerificationService>();
builder.Services.AddScoped<IItemService, ItemService>();
builder.Services.AddScoped<IClaimService, ClaimService>();
builder.Services.AddScoped<ICustodyService, CustodyService>();

// 5. Add MVC Controllers and Views
builder.Services.AddControllersWithViews();

var app = builder.Build();

// 6. Ensure uploads directory exists in wwwroot
var webRoot = app.Environment.WebRootPath ?? Path.Combine(app.Environment.ContentRootPath, "wwwroot");
var uploadsDir = Path.Combine(webRoot, "uploads", "items");
if (!Directory.Exists(uploadsDir))
{
    Directory.CreateDirectory(uploadsDir);
}

// 7. Initialize Database, Seed Roles, Privileged Accounts, Categories, and Locations
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        logger.LogInformation("Initializing database using provider: {Provider}", useSqlite ? "SQLite (Local Dev)" : "SQL Server");
        await DbInitializer.InitializeAsync(services);
        logger.LogInformation("Database initialized and seeded successfully.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database initialization.");
    }
}

// 8. Configure HTTP Request Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
