using System.Text.Json;
using ShopifyApp.Models;

namespace ShopifyApp.Services;

public interface IShopifyAuthService
{
    string BuildAuthorizationUrl(string shop);
    Task<string> ExchangeCodeForToken(string shop, string code);
    bool ValidateShopDomain(string shop);
}

public class ShopifyAuthService : IShopifyAuthService
{
    private readonly ShopifySettings _settings;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShopifyAuthService> _logger;

    public ShopifyAuthService(
        ShopifySettings settings,
        IHttpClientFactory httpClientFactory,
        ILogger<ShopifyAuthService> logger)
    {
        _settings = settings;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public bool ValidateShopDomain(string shop)
    {
        if (string.IsNullOrWhiteSpace(shop)) return false;
        shop = shop.Trim().ToLower();
        return shop.EndsWith(".myshopify.com")
            && !shop.Contains('/')
            && !shop.Contains(' ');
    }

    public string BuildAuthorizationUrl(string shop)
    {
        if (!ValidateShopDomain(shop))
            throw new ArgumentException("Invalid Shopify shop domain.", nameof(shop));

        var redirectUri = Uri.EscapeDataString($"{_settings.AppUrl}/auth/callback");
        var scopes = Uri.EscapeDataString(_settings.Scopes);
        var nonce = Guid.NewGuid().ToString("N");

        return $"https://{shop}/admin/oauth/authorize"
             + $"?client_id={_settings.ApiKey}"
             + $"&scope={scopes}"
             + $"&redirect_uri={redirectUri}"
             + $"&state={nonce}";
    }

    public async Task<string> ExchangeCodeForToken(string shop, string code)
    {
        var client = _httpClientFactory.CreateClient();

        var payload = new Dictionary<string, string>
        {
            ["client_id"] = _settings.ApiKey,
            ["client_secret"] = _settings.ApiSecret,
            ["code"] = code
        };

        var response = await client.PostAsync(
            $"https://{shop}/admin/oauth/access_token",
            new FormUrlEncodedContent(payload));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(body);

        var token = doc.RootElement.GetProperty("access_token").GetString()
            ?? throw new InvalidOperationException("Shopify did not return an access token.");

        _logger.LogInformation("Access token obtained for shop: {Shop}", shop);
        return token;
    }
}