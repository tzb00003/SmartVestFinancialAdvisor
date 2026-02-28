using MudBlazor.Services;
using SmartVestFinancialAdvisor.Components;
using SmartVestFinancialAdvisor.Components.ViewModels;
using SmartVestFinancialAdvisor.Components.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// ViewModel (scoped per circuit)
builder.Services.AddScoped<FinancialSurveyViewModel>();

// Optional: persistence service (VM will use it if present)
builder.Services.AddScoped<IFinancialSurveyService, FinancialSurveyService>();

// Register MudBlazor services (required for Mud components like MudTextField)
builder.Services.AddMudServices(options =>
{
    // Optional UX tuning:
    // options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
    // options.SnackbarConfiguration.PreventDuplicates = true;
    // options.SnackbarConfiguration.ShowCloseIcon = true;
});

// If you plan to call a backend API from services, you can also register HttpClient:
// builder.Services.AddHttpClient<IFinancialSurveyService, FinancialSurveyHttpService>(client =>
// {
//     client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"]!);
// });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
