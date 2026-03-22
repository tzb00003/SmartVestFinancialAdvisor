using SmartVestFinancialAdvisor.Components.Models;
using SmartVestFinancialAdvisor.Core.Constraints;

namespace SmartVestFinancialAdvisor.Components.Services
{
    public interface IRecommendationCatalog
    {
        /// <summary>All static/base recommendations available in the catalog.</summary>
        IReadOnlyList<Recommendation> All { get; }

        /// <summary>Add a new recommendation to the catalog at runtime.</summary>
        void Add(Recommendation item);

        /// <summary>Remove by Type (case-insensitive). Returns true if any removed.</summary>
        bool Remove(string type);

        /// <summary>
        /// Returns a tailored/ordered set of recommendations based on the computed BuildResult.
        /// You can refine this logic to incorporate score, constraints, and category.
        /// </summary>
        IReadOnlyList<Recommendation> For(BuildResult result);
    }
}