namespace DiagnosticLabs.Application.Common;

/// <summary>Dates are chosen as local calendar days but stored as UTC instants.</summary>
public static class LocalTime
{
    public static DateOnly ToLocalDate(DateTime utc) => DateOnly.FromDateTime(DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime());

    public static DateTime ToUtc(DateOnly date, TimeSpan timeOfDay) =>
        new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Local).Add(timeOfDay).ToUniversalTime();

    public static (DateTime From, DateTime To) DayBoundsUtc(DateOnly date) => (ToUtc(date, TimeSpan.Zero), ToUtc(date.AddDays(1), TimeSpan.Zero));
}
