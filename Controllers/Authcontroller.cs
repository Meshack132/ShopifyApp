using Microsoft.AspNetCore.Mvc;
using ShopifyApp.Services;

namespace ShopifyApp.Controllers;

public class AuthController : Controller
{
    private readonly IShopifyAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IShopifyAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    // GET /auth/install?shop=mystore.myshopify.com
    [HttpGet("auth/install")]
    public IActionResult Install([FromQuery] string shop)
    {
        if (string.IsNullOrWhiteSpace(shop))
            return BadRequest("Shop parameter is required.");

        if (!_authService.ValidateShopDomain(shop))
            return BadRequest("Invalid shop domain.");

        try
        {
            var authUrl = _authService.BuildAuthorizationUrl(shop);
            _logger.LogInformation("Redirecting shop {Shop} to auth URL", shop);
            return Redirect(authUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building auth URL for shop: {Shop}", shop);
            return StatusCode(500, "An error occurred during authorization.");
        }
    }

    // GET /auth/callback
    [HttpGet("auth/callback")]
    public async Task<IActionResult> Callback(
        [FromQuery] string shop,
        [FromQuery] string code,
        [FromQuery] string state)
    {
        if (string.IsNullOrWhiteSpace(shop) || string.IsNullOrWhiteSpace(code))
            return BadRequest("Missing required parameters.");

        if (!_authService.ValidateShopDomain(shop))
            return BadRequest("Invalid shop domain.");

        try
        {
            var accessToken = await _authService.ExchangeCodeForToken(shop, code);

            // Store in session (in production, save to a database)
            HttpContext.Session.SetString("ShopifyShop", shop);
            HttpContext.Session.SetString("ShopifyAccessToken", accessToken);

            _logger.LogInformation("Successfully authenticated shop: {Shop}", shop);
            return RedirectToAction("Index", "Dashboard");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auth callback failed for shop: {Shop}", shop);
            return StatusCode(500, "Authentication failed. Please try again.");
        }
    }

    [HttpGet("auth/logout")]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Index", "Home");
    }
}