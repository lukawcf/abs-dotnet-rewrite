using Abs.Api.Abstractions;
using Abs.Api.Auth;
using Abs.Api.Services;
using Abs.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpContextAccessor();
builder.Services.AddAbsData(builder.Configuration);
builder.Services.AddAbsAuthentication(builder.Environment);
builder.Services.AddAuthorization();

builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<LibraryService>();
builder.Services.AddScoped<LibraryItemService>();
builder.Services.AddScoped<UserService>();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
