using Hasm.Components;
using Hasm.Services;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Radzen;
using Wolverine;

var builder = WebApplication.CreateBuilder(args);

var settingsFolder = Path.Combine(builder.Environment.ContentRootPath, "Settings");
var settingsPath = Path.Combine(settingsFolder, "appsettings.json");

if (!Directory.Exists(settingsFolder))
{
    Directory.CreateDirectory(settingsFolder);
}
if (!File.Exists(settingsPath))
{
    File.Copy(Path.Combine(builder.Environment.ContentRootPath, "appsettings.json"), settingsPath);
}

builder.Configuration
    .SetBasePath(builder.Environment.ContentRootPath)
    .AddJsonFile(settingsPath, optional: false, reloadOnChange: true)
    .AddEnvironmentVariables();

builder.Host.UseWolverine(opts =>
{
    // Surely plenty of other configuration for Wolverine...

    // This *temporary* line of code will write out a full report about why or
    // why not Wolverine is finding this handler and its candidate handler messages
    Console.WriteLine(opts.DescribeHandlerMatch(typeof(Hasm.Models.Client)));
});

builder.Services.AddSingleton<Hasm.Repository.DataRepository>();
builder.Services.AddSingleton<MessageBusService>();
builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<VariableService>();
builder.Services.AddSingleton<ClientService>();
builder.Services.AddSingleton<StateMachineService>();
builder.Services.AddSingleton<UIEventRegistration>();
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


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

Task.Run(async () =>
{
    var dataService = app.Services.GetRequiredService<DataService>();
    await dataService.StartAsync();
    var variableService = app.Services.GetRequiredService<VariableService>();
    await variableService.StartAsync();
    var clientService = app.Services.GetRequiredService<ClientService>();
    await clientService.StartAsync();
    var stateMachineService = app.Services.GetRequiredService<StateMachineService>();
    await stateMachineService.StartAsync();
}).Wait();

app.Run();
