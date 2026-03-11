using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ShopifyApp.Models;

namespace ShopifyApp.Services;

public interface IShopifyDataService
{
    Task<DashboardViewModel> GetDashboardData(string shop, string accessToken);
    Task<List<ShopifyProduct>> GetProducts(string shop, string accessToken, int limit = 10);
    Task<List<ShopifyOrder>> GetOrders(string shop, string accessToken, int limit = 10);
    Task<ShopifyProduct> CreateProduct(string shop, string accessToken, ShopifyProduct product);
    Task DeleteProduct(string shop, string accessToken, long productId);
}

public class ShopifyDataService : IShopifyDataService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShopifyDataService> _logger;

    private const string ApiVersion = "2024-01";

    public ShopifyDataService(IHttpClientFactory httpClientFactory, ILogger<ShopifyDataService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private HttpClient CreateClient(string shop, string accessToken)
    {
        var client = _httpClientFactory.CreateClient();
        client.BaseAddress = new Uri($"https://{shop}");
        client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
        client.DefaultRequestHeaders.Accept
              .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static async Task<JsonElement> FetchJson(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadAsStringAsync();
        // Return a cloned element so the document stays in scope
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static string SafeString(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) ? v.GetString() ?? "" : "";

    private static long SafeLong(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.TryGetInt64(out var n) ? n : 0;

    private static decimal SafeDecimal(JsonElement el, string key)
    {
        if (!el.TryGetProperty(key, out var v)) return 0m;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var d)) return d;
        if (v.ValueKind == JsonValueKind.String &&
            decimal.TryParse(v.GetString(), out var ds)) return ds;
        return 0m;
    }

    private static DateTime? SafeDateTime(JsonElement el, string key)
        => el.TryGetProperty(key, out var v) && v.TryGetDateTimeOffset(out var dt)
            ? dt.DateTime : null;

    // ── Public methods ─────────────────────────────────────────────────────────

    public async Task<DashboardViewModel> GetDashboardData(string shop, string accessToken)
    {
        var products = await GetProducts(shop, accessToken, 5);
        var orders = await GetOrders(shop, accessToken, 5);

        var client = CreateClient(shop, accessToken);
        var shopJson = await FetchJson(client, $"/admin/api/{ApiVersion}/shop.json");
        var shopName = shopJson.TryGetProperty("shop", out var s)
            ? SafeString(s, "name") : shop;

        return new DashboardViewModel
        {
            ShopName = string.IsNullOrEmpty(shopName) ? shop : shopName,
            ShopDomain = shop,
            TotalProducts = products.Count,
            TotalOrders = orders.Count,
            TotalRevenue = orders.Sum(o => o.TotalPrice ?? 0m),
            RecentProducts = products,
            RecentOrders = orders
        };
    }

    public async Task<List<ShopifyProduct>> GetProducts(string shop, string accessToken, int limit = 10)
    {
        try
        {
            var client = CreateClient(shop, accessToken);
            var root = await FetchJson(client, $"/admin/api/{ApiVersion}/products.json?limit={limit}");

            var list = new List<ShopifyProduct>();
            if (!root.TryGetProperty("products", out var arr)) return list;

            foreach (var p in arr.EnumerateArray())
            {
                decimal? price = null;
                if (p.TryGetProperty("variants", out var variants) &&
                    variants.GetArrayLength() > 0)
                {
                    price = SafeDecimal(variants[0], "price");
                }

                string? image = null;
                if (p.TryGetProperty("image", out var img) &&
                    img.ValueKind == JsonValueKind.Object)
                {
                    image = SafeString(img, "src");
                }

                list.Add(new ShopifyProduct
                {
                    Id = SafeLong(p, "id"),
                    Title = SafeString(p, "title"),
                    BodyHtml = SafeString(p, "body_html"),
                    Vendor = SafeString(p, "vendor"),
                    ProductType = SafeString(p, "product_type"),
                    Status = SafeString(p, "status"),
                    Price = price,
                    Image = image,
                    CreatedAt = SafeDateTime(p, "created_at")
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching products for shop: {Shop}", shop);
            return new List<ShopifyProduct>();
        }
    }

    public async Task<List<ShopifyOrder>> GetOrders(string shop, string accessToken, int limit = 10)
    {
        try
        {
            var client = CreateClient(shop, accessToken);
            var root = await FetchJson(client, $"/admin/api/{ApiVersion}/orders.json?limit={limit}&status=any");

            var list = new List<ShopifyOrder>();
            if (!root.TryGetProperty("orders", out var arr)) return list;

            foreach (var o in arr.EnumerateArray())
            {
                var customerName = "Guest";
                if (o.TryGetProperty("customer", out var cust) &&
                    cust.ValueKind == JsonValueKind.Object)
                {
                    var first = SafeString(cust, "first_name");
                    var last = SafeString(cust, "last_name");
                    var name = $"{first} {last}".Trim();
                    if (!string.IsNullOrEmpty(name)) customerName = name;
                }

                list.Add(new ShopifyOrder
                {
                    Id = SafeLong(o, "id"),
                    OrderNumber = $"#{SafeLong(o, "order_number")}",
                    Email = SafeString(o, "email"),
                    TotalPrice = SafeDecimal(o, "total_price"),
                    FinancialStatus = SafeString(o, "financial_status"),
                    FulfillmentStatus = SafeString(o, "fulfillment_status") is { Length: > 0 } fs
                                        ? fs : "unfulfilled",
                    CreatedAt = SafeDateTime(o, "created_at"),
                    CustomerName = customerName
                });
            }

            return list;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching orders for shop: {Shop}", shop);
            return new List<ShopifyOrder>();
        }
    }

    public async Task<ShopifyProduct> CreateProduct(string shop, string accessToken, ShopifyProduct product)
    {
        var client = CreateClient(shop, accessToken);

        var body = JsonSerializer.Serialize(new
        {
            product = new
            {
                title = product.Title,
                body_html = product.BodyHtml ?? "",
                vendor = product.Vendor ?? "",
                product_type = product.ProductType ?? "",
                status = "active",
                variants = new[]
                {
                    new
                    {
                        price                = (product.Price ?? 0m).ToString("F2"),
                        inventory_management = "shopify"
                    }
                }
            }
        });

        var content = new StringContent(body, Encoding.UTF8, "application/json");
        var response = await client.PostAsync($"/admin/api/{ApiVersion}/products.json", content);
        response.EnsureSuccessStatusCode();

        var resBody = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(resBody);

        if (doc.RootElement.TryGetProperty("product", out var created))
            product.Id = SafeLong(created, "id");

        _logger.LogInformation("Created product '{Title}' for shop: {Shop}", product.Title, shop);
        return product;
    }

    public async Task DeleteProduct(string shop, string accessToken, long productId)
    {
        var client = CreateClient(shop, accessToken);
        var response = await client.DeleteAsync($"/admin/api/{ApiVersion}/products/{productId}.json");
        response.EnsureSuccessStatusCode();
        _logger.LogInformation("Deleted product {ProductId} for shop: {Shop}", productId, shop);
    }
}