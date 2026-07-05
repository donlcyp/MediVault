using MediVault.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace MediVault.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error(int? statusCode = null)
    {
        var resolvedStatusCode = statusCode ?? HttpContext.Response.StatusCode;

        if (resolvedStatusCode < StatusCodes.Status400BadRequest)
        {
            resolvedStatusCode = StatusCodes.Status500InternalServerError;
        }

        Response.StatusCode = resolvedStatusCode;

        return View(new ErrorViewModel
        {
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier,
            StatusCode = resolvedStatusCode
        });
    }
}
