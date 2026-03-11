using Microsoft.AspNetCore.Mvc;
using ShopifyApp.Models;
using ShopifyApp.Services;

namespace ShopifyApp.Controllers;

public class DashboardController : Controller
{
    private readonly IShopifyDataService _dataService;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IShopifyDataService dataService, ILogger<DashboardController> logger)
    {
        _dataService = dataService;
        _logger = logger;
    }

    private (string? shop, string? token) GetSession()
    {
        return (
            HttpContext.Session.GetString("ShopifyShop"),
            HttpContext.Session.GetString("ShopifyAccessToken")
        );
    }

    public async Task<IActionResult> Index()
    {
        var (shop, token) = GetSession();
        if (shop == null || token == null)
            return RedirectToAction("Index", "Home");

        try
        {
            var dashboard = await _dataService.GetDashboardData(shop, token);
            return View(dashboard);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading dashboard for shop: {Shop}", shop);
            ViewBag.Error = "Failed to load dashboard data.";
            return View(new DashboardViewModel { ShopDomain = shop, ShopName = shop });
        }
    }

    public async Task<IActionResult> Products()
    {
        var (shop, token) = GetSession();
        if (shop == null || token == null)
            return RedirectToAction("Index", "Home");

        var products = await _dataService.GetProducts(shop, token, 20);
        return View(products);
    }

    public async Task<IActionResult> Orders()
    {
        var (shop, token) = GetSession();
        if (shop == null || token == null)
            return RedirectToAction("Index", "Home");

        var orders = await _dataService.GetOrders(shop, token, 20);
        return View(orders);
    }

    [HttpPost]
    public async Task<IActionResult> CreateProduct(ShopifyProduct product)
    {
        var (shop, token) = GetSession();
        if (shop == null || token == null)
            return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
            return RedirectToAction("Products");

        try
        {
            await _dataService.CreateProduct(shop, token, product);
            TempData["Success"] = $"Product '{product.Title}' created successfully!";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating product");
            TempData["Error"] = "Failed to create product.";
        }

        return RedirectToAction("Products");
    }

    [HttpPost]
    public async Task<IActionResult> DeleteProduct(long id)
    {
        var (shop, token) = GetSession();
        if (shop == null || token == null)
            return RedirectToAction("Index", "Home");

        try
        {
            await _dataService.DeleteProduct(shop, token, id);
            TempData["Success"] = "Product deleted successfully.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting product {Id}", id);
            TempData["Error"] = "Failed to delete product.";
        }

        return RedirectToAction("Products");
    }
}