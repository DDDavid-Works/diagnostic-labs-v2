namespace DiagnosticLabs.Application.Billing;

/// <summary>The money rules shared by registration and payment, kept in one place so both agree.</summary>
public static class BillingMath
{
    public static decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// The discount in money. A percentage (of the price) wins over a fixed amount, and a discount never exceeds the price,
    /// so the amount due can not go negative.
    /// </summary>
    public static decimal DiscountTotal(decimal price, decimal? amount, decimal? percentage)
    {
        var total = percentage is { } p ? Round(price * p / 100m) : amount ?? 0m;
        return Math.Clamp(total, 0m, Math.Max(price, 0m));
    }
}
