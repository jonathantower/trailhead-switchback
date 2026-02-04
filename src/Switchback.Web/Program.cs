using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.Cookies;
using Switchback.Core.Repositories;
using Switchback.Core.Services;
using Switchback.Web.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Listen on all interfaces in Development (port 5050 to avoid macOS Control Center/AirPlay using 5000)
if (builder.Environment.IsDevelopment() && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
    builder.WebHost.UseUrls("http://0.0.0.0:5050");

var connectionString = builder.Configuration["ConnectionStrings:TableStorage"] ?? builder.Configuration["AzureWebJobsStorage"];
if (string.IsNullOrWhiteSpace(connectionString))
    connectionString = "UseDevelopmentStorage=true";
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddSingleton(new TableServiceClient(connectionString));
    builder.Services.AddScoped<IUserRepository, TableUserRepository>();
    builder.Services.AddScoped<IProviderConnectionRepository, TableProviderConnectionRepository>();
    builder.Services.AddHostedService<TableStorageInitializer>();
}
builder.Services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.LogoutPath = "/Logout";
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorPages();
builder.Services.AddHttpClient();

var app = builder.Build();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();

app.Run();
