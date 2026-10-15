using Abs.Api.Abstractions;
using Abs.Api.Auth;
using Abs.Api.Services;
using Abs.Data;
using Microsoft.AspNetCore.WebUtilities;

var builder = WebApplication.CreateBuilder(args);

// Node answers res.sendStatus(code) with the plain reason phrase ("Forbidden"); ASP.NET would add a ProblemDetails body.
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options => options.SuppressMapClientErrors = true);
builder.Services.AddHttpContextAccessor();
builder.Services.AddAbsData(builder.Configuration);
builder.Services.AddAbsAuthentication(builder.Environment);
builder.Services.AddAuthorization();

builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<LibraryItemService>();
builder.Services.AddScoped<PlaybackProgressService>();
builder.Services.AddScoped<IUserProgressReader>(services => services.GetRequiredService<PlaybackProgressService>());
builder.Services.AddScoped<BookmarkService>();
builder.Services.AddScoped<UserService>();

var app = builder.Build();

app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    response.ContentType = "text/plain; charset=utf-8";
    await response.WriteAsync(ReasonPhrases.GetReasonPhrase(response.StatusCode));
});

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
