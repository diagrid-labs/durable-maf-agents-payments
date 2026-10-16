using InvoicePayments.ApiService.Checks;
using InvoicePayments.ApiService.Models;

namespace InvoicePayments.Tests;

public class PaymentPolicyTests
{
    private static readonly CheckResult Clean = new([]);
    private static readonly InvoiceReview Pay = new("pay", "s", "looks fine");
    private static readonly InvoiceReview Hold = new("hold", "s", "gift cards");

    [Fact]
    public void Pays_when_checks_pass_and_reviewer_says_pay() =>
        Assert.True(PaymentPolicy.Decide(Clean, Pay).Approved);

    [Fact]
    public void The_agent_cannot_override_a_failed_check() =>
        Assert.False(PaymentPolicy.Decide(new CheckResult(["bank-account-changed"]), Pay).Approved);

    [Fact]
    public void The_agent_can_hold_an_invoice_the_rules_allow() =>
        Assert.False(PaymentPolicy.Decide(Clean, Hold).Approved);

    [Fact]
    public void A_missing_review_holds_the_invoice() =>
        Assert.False(PaymentPolicy.Decide(Clean, null).Approved);
}
