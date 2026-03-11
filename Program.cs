using ShopifyApp.Models;
using ShopifyApp.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// Session (built into ASP.NET Core 8 — no NuGet package needed)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// HttpClient factory (used by services to call Shopify REST API)
builder.Services.AddHttpClient();

// Shopify settings from appsettings.json
var shopifySettings = builder.Configuration
    .GetSection("Shopify")
    .Get<ShopifySettings>() ?? new ShopifySettings();

builder.Services.AddSingleton(shopifySettings);

// App services
builder.Services.AddScoped<IShopifyAuthService, ShopifyAuthService>();
builder.Services.AddScoped<IShopifyDataService, ShopifyDataService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();