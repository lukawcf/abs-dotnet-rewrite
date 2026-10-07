using Abs.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddAbsData(builder.Configuration);

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program;
