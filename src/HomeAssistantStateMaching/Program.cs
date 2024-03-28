using Radzen;
using HomeAssistantStateMaching.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var settingsFolder = Path.Combine(builder.Environment.ContentRootPath, "Settings");
var settingsPath = Path.Combine(settingsFolder, "appsettings.json");

Directory.CreateDirectory(settingsFolder);
if (!File.Exists(settingsPath))
{
    File.Copy(Path.Combine(builder.Environment.ContentRootPath, "appsettings.json"), settingsPath);
}

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile(settingsPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Services.AddDbContextFactory<HasmDbContext>(options =>
{
    options.UseSqlite(builder.Configuration.GetConnectionString("hasm"));
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
});

builder.Services.AddSingleton<HomeAssistantStateMaching.Services.HAClientService>();
builder.Services.AddSingleton<HomeAssistantStateMaching.Services.StateMachineService>();
builder.Services.AddScoped<ContextMenuService>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<TooltipService>();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<HomeAssistantStateMaching.Components.App>()
    .AddInteractiveServerRenderMode();

HomeAssistantStateMaching.Services.Startup.Init(app.Services);

Task.Run(async () =>
{
    var haClientService = app.Services.GetRequiredService<HomeAssistantStateMaching.Services.HAClientService>();
    await haClientService.StartAsync();
}).Wait();

app.Run();

