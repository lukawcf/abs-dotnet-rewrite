using Microsoft.AspNetCore.Mvc;

namespace Abs.Api.Controllers;

/// <summary>Express <c>res.sendStatus(code)</c> answers with the status text as a plain text body.</summary>
public static class PlainStatus
{
    public static IActionResult Forbidden() => Result(StatusCodes.Status403Forbidden, "Forbidden");

    public static IActionResult NotFound() => Result(StatusCodes.Status404NotFound, "Not Found");

    private static ContentResult Result(int code, string text) =>
        new() { StatusCode = code, Content = text, ContentType = "text/plain; charset=utf-8" };
}
