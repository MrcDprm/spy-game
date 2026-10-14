using Microsoft.AspNetCore.Mvc;
using SpyGame.Web.Models;

namespace SpyGame.Web.Controllers;

/// <summary>Friendly error pages. Details stay in the log; visitors never see a stack trace.</summary>
[ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
public class ErrorController : Controller
{
    [Route("Error/{code:int?}")]
    public IActionResult Index(int? code) => View("~/Views/Shared/Error.cshtml", new ErrorViewModel(code ?? 500));
}
