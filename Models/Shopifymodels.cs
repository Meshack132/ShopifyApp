namespace ShopifyApp.Models;

public class ShopifySettings
{
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public string Scopes { get; set; } = string.Empty;
    public string AppUrl { get; set; } = string.Empty;
}

public class ShopifyProduct
{
    public long Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? BodyHtml { get; set; }
    public string? Vendor { get; set; }
    public string? ProductType { get; set; }
    public string? Status { get; set; }
    public decimal? Price { get; set; }
    public string? Image { get; set; }
    public DateTime? CreatedAt { get; set; }
}

public class ShopifyOrder
{
    public long Id { get; set; }
    public string? OrderNumber { get; set; }
    public string? Email { get; set; }
    public decimal? TotalPrice { get; set; }
    public string? FinancialStatus { get; set; }
    public string? FulfillmentStatus { get; set; }
    public DateTime? CreatedAt { get; set; }
    public string? CustomerName { get; set; }
}

public class DashboardViewModel
{
    public string ShopName { get; set; } = string.Empty;
    public string ShopDomain { get; set; } = string.Empty;
    public int TotalProducts { get; set; }
    public int TotalOrders { get; set; }
    public decimal TotalRevenue { get; set; }
    public List<ShopifyProduct> RecentProducts { get; set; } = new();
    public List<ShopifyOrder> RecentOrders { get; set; } = new();
}

public class ErrorViewModel
{
    public string? RequestId { get; set; }
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}