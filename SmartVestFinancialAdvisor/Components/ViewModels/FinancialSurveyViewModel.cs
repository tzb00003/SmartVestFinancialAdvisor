using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Components.Services;
using SmartVestFinancialAdvisor.Core.Constraints;
using SmartVestFinancialAdvisor.Core.Scoring;

namespace SmartVestFinancialAdvisor.Components.ViewModels
{
    public sealed class FinancialSurveyViewModel
    {
        private readonly ISurveyDataService _surveyDataService;
        private readonly IAuthenticationService _authService;
        private readonly IFinancialSurveyService _financialSurveyService;
        private readonly ScoreCalculator _scoreCalculator;

        public FinancialSurveyViewModel(
            ISurveyDataService surveyDataService,
            IAuthenticationService authService,
            IFinancialSurveyService financialSurveyService,
            ScoreCalculator scoreCalculator)
        {
            _surveyDataService = surveyDataService;
            _authService = authService;
            _financialSurveyService = financialSurveyService;
            _scoreCalculator = scoreCalculator;
        }

        public FinancialSurveyModel Model { get; set; } = new();

        public BuildResult? AdvisorResult { get; set; }

        public bool IsValid { get; set; }

        public bool IsSubmitting { get; set; }

        public event Action? StateChanged;

        public List<string> States { get; } = new()
        {
            "Alabama", "Alaska", "Arizona", "Arkansas", "California",
            "Colorado", "Connecticut", "Delaware", "Florida", "Georgia",
            "Hawaii", "Idaho", "Illinois", "Indiana", "Iowa",
            "Kansas", "Kentucky", "Louisiana", "Maine", "Maryland",
            "Massachusetts", "Michigan", "Minnesota", "Mississippi", "Missouri",
            "Montana", "Nebraska", "Nevada", "New Hampshire", "New Jersey",
            "New Mexico", "New York", "North Carolina", "North Dakota", "Ohio",
            "Oklahoma", "Oregon", "Pennsylvania", "Rhode Island", "South Carolina",
            "South Dakota", "Tennessee", "Texas", "Utah", "Vermont",
            "Virginia", "Washington", "West Virginia", "Wisconsin", "Wyoming",
            "District of Columbia"
        };

        public async Task LoadUserResultAsync()
        {
            try
            {
                if (!_authService.IsLoggedIn || _authService.CurrentUserId is null)
                    return;

                var userId = _authService.CurrentUserId.Value;

                var result = await _surveyDataService.GetLatestResultAsync(userId);

                if (result is not null)
                {
                    try
                    {
                        var options = new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true,
                            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                            WriteIndented = false
                        };

                        AdvisorResult = JsonSerializer.Deserialize<BuildResult>(result.PortfolioJson, options);

                        if (AdvisorResult is null)
                        {
                            Console.Error.WriteLine("⚠️ Deserialization returned null");
                            AdvisorResult = new BuildResult { Score = result.Score };
                        }
                        else
                        {
                            AdvisorResult.Score = result.Score;
                            Console.WriteLine($"✅ Loaded BuildResult - Score: {AdvisorResult.Score}");
                            Console.WriteLine($"✅ FinancialScore: {AdvisorResult.FinancialScore?.Total}");
                            Console.WriteLine($"✅ Recommendations: {AdvisorResult.Recommendations?.Count}");
                        }

                        StateChanged?.Invoke();
                    }
                    catch (JsonException ex)
                    {
                        Console.Error.WriteLine($"❌ Failed to deserialize portfolio JSON: {ex.Message}");
                        AdvisorResult = new BuildResult { Score = result.Score };
                    }
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"❌ Failed to load user result: {ex.Message}");
            }
        }

        public async Task SubmitAsync()
        {
            try
            {
                IsSubmitting = true;

                if (!_authService.IsLoggedIn || _authService.CurrentUserId is null)
                    throw new InvalidOperationException("User must be logged in to submit survey.");

                var userId = _authService.CurrentUserId.Value;

                var submission = await _surveyDataService.SaveSurveyAsync(userId, Model);

                var financialItems = Model.Items != null
                    ? Model.Items.Select(i => new Core.Financial.FinancialItem
                    {
                        Label = i.Label ?? "Unnamed",
                        Amount = i.Amount ?? 0m,
                        MonthlyPayment = i.MonthlyPayment ?? 0m,
                        InterestRate = i.InterestRate ?? 0m,
                        IsDebt = i.IsDebt,
                        IsRetirement = i.IsRetirement
                    }).ToList()
                    : new List<Core.Financial.FinancialItem>();

                var clientProfile = new ClientProfile
                {
                    MonthlyIncome = Model.MonthlyIncome ?? 0m,
                    Age = Model.Age ?? 0,
                    MonthlyExpense = Model.MonthlyExpense ?? 0m,
                    Savings = Model.Savings ?? 0m,
                    Debt = Model.Debt ?? 0m,
                    LocationState = Model.State,
                    Items = financialItems
                };

                var scoreResult = await _scoreCalculator.Calculate(clientProfile);
                decimal score = scoreResult?.TotalScore ?? 50m;

                BuildResult? advisorResult = null;
                try
                {
                    advisorResult = await _financialSurveyService.AnalyzeAsync(Model);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Advisor analysis failed: {ex.Message}");
                    advisorResult = new BuildResult { Score = score };
                }

                if (advisorResult is null)
                {
                    advisorResult = new BuildResult { Score = score };
                }

                advisorResult.Score = score;

                string portfolioJson = JsonSerializer.Serialize(advisorResult);

                await _surveyDataService.SaveResultAsync(userId, submission.Id, score, portfolioJson);

                await _surveyDataService.DeleteOldSubmissionsAsync(userId, keepCount: 5);

                AdvisorResult = advisorResult;

                StateChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Survey submission failed: {ex.Message}");
                throw;
            }
            finally
            {
                IsSubmitting = false;
            }
        }

        public void RemoveItem(FinancialItem item)
        {
            Model.Items.Remove(item);
            StateChanged?.Invoke();
        }

        public void Reset()
        {
            Model = new();
            AdvisorResult = null;
            IsValid = false;
            StateChanged?.Invoke();
        }
    }
}