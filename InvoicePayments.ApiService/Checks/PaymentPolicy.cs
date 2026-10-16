using InvoicePayments.ApiService.Models;

namespace InvoicePayments.ApiService.Checks;

// Combines the deterministic checks with the agent's recommendation. Pure and side-effect
// free, so it is safe to call from workflow code: every replay produces the same decision.
public static class PaymentPolicy
{
    public static PaymentDecision Decide(CheckResult checks, InvoiceReview? review)
    {
        if (!checks.Passed)
            return new PaymentDecision(false, $"Failed checks: {string.Join(", ", checks.Flags)}");

        if (review is null)
            return new PaymentDecision(false, "Agent review unavailable, routed to manual review");

        if (!string.Equals(review.Recommendation, "pay", StringComparison.OrdinalIgnoreCase))
            return new PaymentDecision(false, $"Held by reviewer: {review.Rationale}");

        return new PaymentDecision(true, "Checks passed and reviewer recommends payment");
    }
}
