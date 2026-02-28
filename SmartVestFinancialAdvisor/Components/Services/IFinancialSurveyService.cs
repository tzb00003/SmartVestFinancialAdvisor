using SmartVestFinancialAdvisor.Components.Models;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface IFinancialSurveyService
    {
        Task SaveAsync(FinancialSurveyModel model, CancellationToken ct);
    }

    /// <summary>
    /// Example no-op implementation; replace with API call.
    /// </summary>
    public sealed class FinancialSurveyService : IFinancialSurveyService
    {
        public Task SaveAsync(FinancialSurveyModel model, CancellationToken ct)
        {
            // TODO: call your API here (e.g., HttpClient POST)
            // This is just a stub to show the pattern.
            return Task.CompletedTask;
        }
    }
}