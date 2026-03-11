using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ShopifyApp.Models;

namespace ShopifyApp.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        var shop = HttpContext.Session.GetString("ShopifyShop");
        if (!string.IsNullOrEmpty(shop))
            return RedirectToAction("Index", "Dashboard");

        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }
}