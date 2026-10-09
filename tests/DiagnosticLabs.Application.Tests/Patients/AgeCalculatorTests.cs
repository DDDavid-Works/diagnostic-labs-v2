using DiagnosticLabs.Domain.Patients;

namespace DiagnosticLabs.Application.Tests.Patients;

public class AgeCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 10, 9);

    [Theory]
    [InlineData("1990-10-09", "36 years old")] // birthday today
    [InlineData("1990-10-10", "35 years old")] // birthday tomorrow
    [InlineData("2025-10-09", "1 year old")]
    [InlineData("2026-04-09", "6 months old")]
    [InlineData("2026-09-09", "1 month old")]
    [InlineData("2026-10-01", "8 days old")]
    [InlineData("2026-10-08", "1 day old")]
    [InlineData("2026-10-09", "0 days old")]
    public void Describes_the_age_in_the_largest_whole_unit(string birth, string expected)
    {
        Assert.Equal(expected, AgeCalculator.Describe(DateOnly.Parse(birth), Today));
    }

    [Fact]
    public void Missing_or_future_birth_dates_have_no_age()
    {
        Assert.Null(AgeCalculator.Describe(null, Today));
        Assert.Null(AgeCalculator.Describe(Today.AddDays(1), Today));
    }

    [Fact]
    public void Leap_day_birthdays_are_handled()
    {
        Assert.Equal("1 year old", AgeCalculator.Describe(new DateOnly(2024, 2, 29), new DateOnly(2025, 2, 28)));
        Assert.Equal("11 months old", AgeCalculator.Describe(new DateOnly(2024, 2, 29), new DateOnly(2025, 2, 27)));
    }
}
