using Microsoft.AspNetCore.Components.Server; // <-- add this for CircuitOptions
using MudBlazor.Services;
using SmartVestFinancialAdvisor.Components;
using SmartVestFinancialAdvisor.Core.Benchmarks;
using SmartVestFinancialAdvisor.Infrastructure.Benchmarks;
using SmartVestFinancialAdvisor.Infrastructure.Census;
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Constraints;
using SmartVestFinancialAdvisor.Core.Agents;
using SmartVestFinancialAdvisor.Core.Financial;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using SmartVestFinancialAdvisor.Components.ViewModels;
using SmartVestFinancialAdvisor.Components.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// 🔎 Fastest way to see detailed circuit errors in Dev
builder.Services.Configure<CircuitOptions>(o =>
{
    o.DetailedErrors = builder.Environment.IsDevelopment();
});

// ViewModel (scoped per circuit) — removed duplicate registration
builder.Services.AddScoped<FinancialSurveyViewModel>();

// Catalog
builder.Services.AddSingleton<IRecommendationCatalog, RecommendationCatalog>();

// Core Logic Services
builder.Services.AddScoped<FinancialAggregationService>();
builder.Services.AddScoped<ScoreCalculator>();
builder.Services.AddScoped<AdvisorEngine>(sp =>
{
    var engine = new AdvisorEngine();
    engine.RegisterAgent(new BenchmarkAgent());
    engine.RegisterAgent(new PortfolioAgent());
    engine.RegisterAgent(new SavingsAgent());
    return engine;
});
builder.Services.AddScoped<Builder>();

// Survey Service (Maps UI -> Core)
builder.Services.AddScoped<IFinancialSurveyService, FinancialSurveyService>();

// MudBlazor
builder.Services.AddMudServices(options =>
{
    // options.SnackbarConfiguration.PositionClass = Defaults.Classes.Position.TopCenter;
});

// Benchmarks / background census
string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks.db");
builder.Services.AddSingleton<SqliteBenchmarkProvider>(_ => new SqliteBenchmarkProvider(dbPath));
builder.Services.AddSingleton<IBenchmarkProvider>(sp => sp.GetRequiredService<SqliteBenchmarkProvider>());
builder.Services.AddHostedService<CensusBackgroundService>();

var app = builder.Build();

// Pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
//app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();