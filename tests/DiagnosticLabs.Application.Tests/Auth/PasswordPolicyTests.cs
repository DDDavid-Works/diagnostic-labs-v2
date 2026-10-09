using DiagnosticLabs.Application.Auth;

namespace DiagnosticLabs.Application.Tests.Auth;

public class PasswordPolicyTests
{
    [Theory]
    [InlineData("12345678")]
    [InlineData("a long passphrase with spaces")]
    public void Passwords_of_8_or_more_characters_are_accepted(string password) =>
        Assert.Empty(PasswordPolicy.Validate(password, "someone"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1234567")]
    public void Empty_or_too_short_passwords_are_refused(string? password) =>
        Assert.NotEmpty(PasswordPolicy.Validate(password));

    [Fact]
    public void The_password_may_not_be_the_username_in_any_case() =>
        Assert.NotEmpty(PasswordPolicy.Validate("Administrator", "administrator"));

    [Fact]
    public void Over_long_passwords_are_refused() =>
        Assert.NotEmpty(PasswordPolicy.Validate(new string('x', PasswordPolicy.MaxLength + 1)));

    [Fact]
    public void Generated_passwords_always_satisfy_the_policy_and_are_not_repeated()
    {
        var generated = Enumerable.Range(0, 50).Select(_ => PasswordPolicy.Generate()).ToList();

        Assert.All(generated, p => Assert.Empty(PasswordPolicy.Validate(p)));
        Assert.Equal(generated.Count, generated.Distinct().Count());
        Assert.All(generated, p => Assert.DoesNotContain(p, c => "0O1lI".Contains(c)));
    }

    [Fact]
    public void A_generated_password_is_never_shorter_than_the_minimum() =>
        Assert.Equal(PasswordPolicy.MinLength, PasswordPolicy.Generate(3).Length);
}
