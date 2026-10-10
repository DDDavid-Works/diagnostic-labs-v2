namespace DiagnosticLabs.Application.Billing;

/// <summary>One step of a discount: a fixed amount or a percentage (exactly one of them is set).</summary>
public sealed record DiscountStep(decimal? Amount, decimal? Percentage);

/// <summary>What one step took off, and what is left after it.</summary>
public sealed record DiscountStepResult(DiscountStep Step, decimal Cut, decimal Remaining);

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

    /// <summary>
    /// A discount made of several steps, applied one after another in the order given: each step is taken off what is left after the
    /// one before (a percentage is a percentage of what is left). No step takes more than what is left, so the amount due never goes below zero.
    /// </summary>
    public static IReadOnlyList<DiscountStepResult> ApplySteps(decimal price, IEnumerable<DiscountStep> steps)
    {
        var remaining = Math.Max(price, 0m);
        var results = new List<DiscountStepResult>();
        foreach (var step in steps)
        {
            var cut = step.Percentage is { } p ? Round(remaining * p / 100m) : step.Amount ?? 0m;
            cut = Math.Clamp(cut, 0m, remaining);
            remaining -= cut;
            results.Add(new DiscountStepResult(step, cut, remaining));
        }

        return results;
    }

    /// <summary>The total discount of a discount made of steps (see <see cref="ApplySteps"/>).</summary>
    public static decimal DiscountTotal(decimal price, IEnumerable<DiscountStep> steps) =>
        ApplySteps(price, steps).Sum(r => r.Cut);
}
