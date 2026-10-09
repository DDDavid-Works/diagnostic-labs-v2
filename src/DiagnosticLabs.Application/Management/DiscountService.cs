using DiagnosticLabs.Application.Abstractions;
using DiagnosticLabs.Application.Common;
using DiagnosticLabs.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace DiagnosticLabs.Application.Management;

/// <summary>One option of a discount: exactly one of <see cref="Amount"/> or <see cref="Percentage"/> is set.</summary>
public sealed record DiscountLine(long Id, decimal? Amount, decimal? Percentage);

public sealed record DiscountListItem(long Id, string DiscountName, string DiscountDescription, bool IsActive) : IHasId;

public sealed record DiscountDetails(
    long Id, string DiscountName, string DiscountDescription, bool IsActive, IReadOnlyList<DiscountLine> Lines, byte[] RowVersion) : IHasId;

public sealed record DiscountInput(
    long Id, string? DiscountName, string? DiscountDescription, bool IsActive, IReadOnlyList<DiscountLine> Lines, byte[]? RowVersion) : IReferenceInput;

public interface IDiscountService : ICrudService<DiscountListItem, DiscountDetails, DiscountInput>;

public sealed class DiscountService(IAppDbContext db, ICurrentUser user)
    : ReferenceCrudService<Discount, DiscountListItem, DiscountDetails, DiscountInput>(db, user, ModuleIds.Discounts, "Discount"),
      IDiscountService
{
    protected override DbSet<Discount> Set => Db.Discounts;

    protected override IQueryable<Discount> ForEditing(IQueryable<Discount> q) => q.Include(d => d.Details);

    protected override IQueryable<Discount> Matches(IQueryable<Discount> q, string word) =>
        q.Where(d => d.DiscountName.Contains(word) || d.DiscountDescription.Contains(word));

    protected override IOrderedQueryable<Discount> Order(IQueryable<Discount> q) => q.OrderBy(d => d.DiscountName).ThenBy(d => d.Id);

    protected override DiscountListItem ToListItem(Discount d) => new(d.Id, d.DiscountName, d.DiscountDescription, d.IsActive);

    protected override DiscountDetails ToDetails(Discount d) =>
        new(d.Id, d.DiscountName, d.DiscountDescription, d.IsActive,
            [.. d.Details.Where(x => !x.IsDeleted).OrderBy(x => x.Id).Select(x => new DiscountLine(x.Id, x.Amount, x.Percentage))],
            d.RowVersion);

    protected override IReadOnlyList<string> Validate(DiscountInput input)
    {
        var errors = new List<string>();
        Rules.Required(errors, input.DiscountName, "Name", 50);
        Rules.Required(errors, input.DiscountDescription, "Description", 200);

        foreach (var line in input.Lines)
        {
            if (line.Amount is null == line.Percentage is null)
                errors.Add("Each discount option needs either an amount or a percentage (not both, not neither).");
            else if (line.Amount < 0)
                errors.Add("A discount amount must not be negative.");
            else if (line.Percentage is < 0 or > 100)
                errors.Add("A discount percentage must be between 0 and 100.");
        }

        return [.. errors.Distinct()];
    }

    protected override void ApplyFields(Discount discount, DiscountInput input)
    {
        discount.DiscountName = input.DiscountName!.Trim();
        discount.DiscountDescription = input.DiscountDescription!.Trim();

        SyncChildren(
            discount.Details,
            input.Lines,
            line => line.Id,
            child => child.Id,
            (child, line) => false,
            (child, line) =>
            {
                child.Amount = line.Amount;
                child.Percentage = line.Percentage;
            },
            child => Db.DiscountDetails.Remove(child));
    }
}
