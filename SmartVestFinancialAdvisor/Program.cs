/* using MudBlazor.Services;
using SmartVestFinancialAdvisor.Components;
using MudBlazor.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
// Register MudBlazor services (required for MudBlazor components like MudTextField)
builder.Services.AddMudServices();

builder.Services.AddMudServices();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();*/
using SmartVestFinancialAdvisor.Core.Scoring;
using SmartVestFinancialAdvisor.Core.Constraints;

namespace SmartVestFinancialAdvisor
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=== SmartVestFinancialAdvisor Test ===\n");

            // 1. Create a sample client profile
            ClientProfile clientProfile = new ClientProfile
            {
                MonthlyIncome = 6000m,
                Savings = 20000m,
                MonthlyDebt = 1500m
            };

            // 2. Create a FinancialProfile for constraints builder
            FinancialProfile financialProfile = new FinancialProfile
            {
                MonthlyIncome = clientProfile.MonthlyIncome,
                Savings = clientProfile.Savings,
                MonthlyDebt = clientProfile.MonthlyDebt,
                RiskTolerance = 0.5m // example risk tolerance
            };

            // 3. Calculate scores
            ScoreCalculator scoreCalculator = new ScoreCalculator();
            FinancialScore financialScore = scoreCalculator.AggregateScore(clientProfile);

            Console.WriteLine("--- SubScores ---");
            foreach (var sub in financialScore.SubScores)
            {
                Console.WriteLine($"{sub.Name}: Raw={sub.RawScore}, Weight={sub.Weight}, Weighted={sub.WeightedScore}");
            }

            Console.WriteLine($"\nTotal Score: {financialScore.Total}");

            // 4. Determine client category
            ClientCategory category = Categories.DetermineCategory(financialScore);
            var categoryDefinition = Categories.GetCategoryDefinition(category);

            Console.WriteLine($"\nClient Category: {category}");
            Console.WriteLine($"Category Rules: Stock={categoryDefinition.DefaultStockAllocation}, Bond={categoryDefinition.DefaultBondAllocation}, Cash={categoryDefinition.DefaultCashAllocation}");

            // 5. Build portfolio constraints
            Builder builder = new Builder();
            BuildResult buildResult = builder.Build(financialProfile);

            Console.WriteLine("\n--- Portfolio Constraints ---");
            Console.WriteLine($"Max Stock Allocation: {buildResult.Constraints.MaxStockAllocation}");
            Console.WriteLine($"Max Bond Allocation: {buildResult.Constraints.MaxBondAllocation}");
            Console.WriteLine($"Max Cash Allocation: {buildResult.Constraints.MaxCashAllocation}");

            Console.WriteLine("\n=== Test Complete ===");
        }
    }
}
