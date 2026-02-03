using Azure.Data.Tables;
using Microsoft.AspNetCore.Authentication.Cookies;
using Switchback.Core.Repositories;
using Switchback.Core.Services;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration["ConnectionStrings:TableStorage"] ?? builder.Configuration["AzureWebJobsStorage"];
if (!string.IsNullOrEmpty(connectionString))
{
    builder.Services.AddSingleton(new TableServiceClient(connectionString));
    builder.Services.AddScoped<IUserRepository, TableUserRepository>();
    builder.Services.AddScoped<IProviderConnectionRepository, TableProviderConnectionRepository>();
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
