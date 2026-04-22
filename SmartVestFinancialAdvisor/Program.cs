using Microsoft.AspNetCore.Components.Server;
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
using Microsoft.EntityFrameworkCore;
using SmartVestFinancialAdvisor.Data;

var builder = WebApplication.CreateBuilder(args);

// ✅ Database configuration
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ✅ Razor components and interactivity
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.Configure<CircuitOptions>(o =>
{
    o.DetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddScoped<IEncryptionService, EncryptionService>();

builder.Services.AddScoped<ISurveyDataService, SurveyDataService>();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();

builder.Services.AddScoped<FinancialSurveyViewModel>();

builder.Services.AddSingleton<IRecommendationCatalog, RecommendationCatalog>();

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

builder.Services.AddScoped<IFinancialSurveyService, FinancialSurveyService>();

builder.Services.AddMudServices(options =>
{
});

string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "benchmarks.db");
builder.Services.AddSingleton<SqliteBenchmarkProvider>(_ => new SqliteBenchmarkProvider(dbPath));
builder.Services.AddSingleton<IBenchmarkProvider>(sp => sp.GetRequiredService<SqliteBenchmarkProvider>());
builder.Services.AddHostedService<CensusBackgroundService>();

var app = builder.Build();

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