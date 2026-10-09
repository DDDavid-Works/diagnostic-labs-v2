namespace DiagnosticLabs.Domain.Patients;

/// <summary>Turns a date of birth into the age text printed on forms ("34 years old"), computed on demand instead of stored.</summary>
public static class AgeCalculator
{
    public static string? Describe(DateOnly? dateOfBirth, DateOnly today)
    {
        if (dateOfBirth is not { } birth || birth > today)
            return null;

        var years = today.Year - birth.Year;
        if (birth.AddYears(years) > today)
            years--;

        if (years >= 1)
            return years == 1 ? "1 year old" : $"{years} years old";

        var months = (today.Year - birth.Year) * 12 + today.Month - birth.Month;
        if (birth.AddMonths(months) > today)
            months--;

        if (months >= 1)
            return months == 1 ? "1 month old" : $"{months} months old";

        var days = today.DayNumber - birth.DayNumber;
        return days == 1 ? "1 day old" : $"{days} days old";
    }
}
