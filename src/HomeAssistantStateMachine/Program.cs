using Radzen;
using HomeAssistantStateMachine.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

var settingsFolder = Path.Combine(builder.Environment.ContentRootPath, "Settings");
var settingsPath = Path.Combine(settingsFolder, "appsettings.json");
var databasePath = Path.Combine(settingsFolder, "hasm.db3");

Directory.CreateDirectory(settingsFolder);
if (!File.Exists(settingsPath))
{
    File.Copy(Path.Combine(builder.Environment.ContentRootPath, "appsettings.json"), settingsPath);
}
if (!File.Exists(databasePath))
{
    File.Copy(Path.Combine(builder.Environment.ContentRootPath, "hasm.db3"), databasePath);
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

builder.Services.AddSingleton<HomeAssistantStateMachine.Services.VariableService>();
builder.Services.AddSingleton<HomeAssistantStateMachine.Services.HAClientService>();
builder.Services.AddSingleton<HomeAssistantStateMachine.Services.StateMachineService>();
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

app.MapRazorComponents<HomeAssistantStateMachine.Components.App>()
    .AddInteractiveServerRenderMode();

HomeAssistantStateMachine.Services.Startup.Init(app.Services);

Task.Run(async () =>
{
    var variableService = app.Services.GetRequiredService<HomeAssistantStateMachine.Services.VariableService>();
    await variableService.StartAsync();
    var haClientService = app.Services.GetRequiredService<HomeAssistantStateMachine.Services.HAClientService>();
    await haClientService.StartAsync();
    var stateMachineService = app.Services.GetRequiredService<HomeAssistantStateMachine.Services.StateMachineService>();
    await stateMachineService.StartAsync();
}).Wait();

app.Run();

