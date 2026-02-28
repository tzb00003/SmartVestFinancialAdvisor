using System.ComponentModel;
using System.Runtime.CompilerServices;
using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Components.Services;

namespace SmartVestFinancialAdvisor.Components.ViewModels
{
    /// <summary>
    /// ViewModel encapsulating UI state, operations, and computed metrics.
    /// </summary>
    public class FinancialSurveyViewModel : INotifyPropertyChanged
    {
        private readonly IFinancialSurveyService? _service;

        public FinancialSurveyViewModel(IFinancialSurveyService? service = null)
        {
            _service = service;
        }

        public FinancialSurveyModel Model { get; } = new();

        private bool _isValid;
        public bool IsValid
        {
            get => _isValid;
            set => Set(ref _isValid, value);
        }

        private bool _isSubmitting;
        public bool IsSubmitting
        {
            get => _isSubmitting;
            set => Set(ref _isSubmitting, value);
        }

        private bool _submitted;
        public bool Submitted
        {
            get => _submitted;
            set => Set(ref _submitted, value);
        }

        private string? _serverError;
        public string? ServerError
        {
            get => _serverError;
            set => Set(ref _serverError, value);
        }

        public IReadOnlyList<string> States { get; } = new List<string>
        {
            "AL","AK","AZ","AR","CA","CO","CT","DE","FL","GA","HI","ID","IL","IN",
            "IA","KS","KY","LA","ME","MD","MA","MI","MN","MS","MO","MT","NE","NV",
            "NH","NJ","NM","NY","NC","ND","OH","OK","OR","PA","RI","SC","SD","TN",
            "TX","UT","VT","VA","WA","WV","WI","WY","DC"
        };

        #region Commands

        public void AddItem() => Model.Items.Add(new FinancialItem());

        public void RemoveItem(FinancialItem item)
        {
            if (item is not null) Model.Items.Remove(item);
        }

        public void Reset()
        {
            ServerError = null;
            Submitted = false;

            Model.MonthlyIncome = null;
            Model.Savings = null;
            Model.Debt = null;
            Model.MonthlyExpense = null;
            Model.RiskLevel = RiskLevel.Low;
            Model.Age = null;
            Model.State = null;

            // Start with NO default empty row to avoid blocking form validity.
            Model.Items.Clear();

            OnStateChanged();
        }

        /// <summary>
        /// Submit & persist via injected service if available.
        /// Returns true if submission completed (even if persistence not configured).
        /// </summary>
        public async Task<bool> SubmitAsync()
        {
            ServerError = null;
            Submitted = false;

            try
            {
                IsSubmitting = true;

                if (_service is not null)
                {
                    await _service.SaveAsync(Model, CancellationToken.None);
                }

                Submitted = true;
                return true;
            }
            catch (Exception)
            {
                ServerError = "An unexpected error occurred while submitting. Please try again.";
                return false;
            }
            finally
            {
                IsSubmitting = false;
                OnStateChanged();
            }
        }

        #endregion

        #region Metrics & Formatting

        /// <summary>
        /// Returns Debt Payments: use Model.Debt if provided; otherwise sum item monthly payments for debts.
        /// </summary>
        public decimal? EffectiveDebtPayments()
        {
            if (Model.Debt.HasValue) return Model.Debt;
            var sum = Model.Items.Where(i => i.IsDebt).Sum(i => i.MonthlyPayment ?? 0);
            return sum == 0 ? (decimal?)null : sum;
        }

        /// <summary>DTI = Debt Payments (monthly) / Monthly Income (gross)</summary>
        public static decimal? ComputeDti(decimal? monthlyIncome, decimal? debtPayments)
        {
            if (monthlyIncome is null || monthlyIncome <= 0 || debtPayments is null) return null;
            return debtPayments / monthlyIncome;
        }

        /// <summary>Net Cash Flow = Monthly Income - (Monthly Expense + Debt Payments)</summary>
        public static decimal? ComputeNetCashFlow(decimal? monthlyIncome, decimal? monthlyExpense, decimal? debtPayments)
        {
            if (monthlyIncome is null || monthlyExpense is null || debtPayments is null) return null;
            return monthlyIncome - (monthlyExpense + debtPayments);
        }

        /// <summary>Weighted average interest rate for debts by balance (Amount).</summary>
        public decimal? WeightedDebtRate()
        {
            var debts = Model.Items.Where(i => i.IsDebt && (i.Amount ?? 0) > 0 && i.InterestRate is not null).ToList();
            if (!debts.Any()) return null;
            var total = debts.Sum(i => i.Amount ?? 0);
            if (total == 0) return null;
            var weighted = debts.Sum(i => (i.Amount ?? 0) * (i.InterestRate ?? 0)) / total;
            return weighted;
        }

        public decimal TotalDebtBalanceFromItems =>
            Model.Items.Where(i => i.IsDebt).Sum(i => i.Amount ?? 0);

        public decimal TotalAssetBalanceFromItems =>
            Model.Items.Where(i => !i.IsDebt).Sum(i => i.Amount ?? 0);

        public decimal TotalRetirementBalanceFromItems =>
            Model.Items.Where(i => i.IsRetirement && !i.IsDebt).Sum(i => i.Amount ?? 0);

        public static string FormatCurrency(decimal? value) =>
            value.HasValue ? value.Value.ToString("C") : "-";

        public static string FormatPercent(decimal? value) =>
            value.HasValue ? $"{value:P2}" : "-";

        public IEnumerable<KeyValuePair<string, string>> SummaryRows()
        {
            var effDebt = EffectiveDebtPayments();
            var dti = ComputeDti(Model.MonthlyIncome, effDebt);
            var ncf = ComputeNetCashFlow(Model.MonthlyIncome, Model.MonthlyExpense, effDebt);

            return new[]
            {
                Kvp("Monthly Income (Gross)",      FormatCurrency(Model.MonthlyIncome)),
                Kvp("Savings (Liquid)",            FormatCurrency(Model.Savings)),
                Kvp("Debt Payments (Monthly)",     FormatCurrency(Model.Debt)),
                Kvp("Monthly Living Expenses",     FormatCurrency(Model.MonthlyExpense)),
                Kvp("Risk Level",                  Model.RiskLevel.ToString()),
                Kvp("Age",                         Model.Age?.ToString() ?? "-"),
                Kvp("State",                       Model.State ?? "-"),
                Kvp("Estimated DTI (Debt / Monthly Income)", dti is { } d ? $"{d:P1}" : "-"),
                Kvp("Net Monthly Cash Flow",       ncf is { } net ? FormatCurrency(net) : "-"),
            };
        }

        public IEnumerable<KeyValuePair<string, string>> ItemSummaryRows()
        {
            return new[]
            {
                Kvp("Assets (Total)",                 FormatCurrency(TotalAssetBalanceFromItems)),
                Kvp("Liabilities (Total Balance)",    FormatCurrency(TotalDebtBalanceFromItems)),
                Kvp("Retirement Assets (Total)",      FormatCurrency(TotalRetirementBalanceFromItems)),
                Kvp("Debt Payments from Items",       FormatCurrency(Model.Items.Where(i => i.IsDebt).Sum(i => i.MonthlyPayment ?? 0))),
                Kvp("Weighted Debt Interest Rate",    FormatPercent(WeightedDebtRate()))
            };
        }

        private static KeyValuePair<string, string> Kvp(string k, string v) => new(k, v);

        #endregion

        #region Change Notification

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? StateChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

        protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            OnStateChanged();
            return true;
        }

        private void OnStateChanged() => StateChanged?.Invoke();

        #endregion
    }
}
