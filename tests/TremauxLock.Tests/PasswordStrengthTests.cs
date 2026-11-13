public sealed class PasswordStrengthTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("curta")]
    [InlineData("aaaaaaaaaa")]
    [InlineData("1234567890")]
    [InlineData("abcdefghij")]
    [InlineData("senha12345")]
    [InlineData("password12")]
    public void Evaluate_ClassifiesPredictableOrShortPasswordsAsVeryWeak(string? password)
    {
        Assert.Equal(PasswordStrengthLevel.VeryWeak, PasswordStrength.Evaluate(password));
    }

    [Theory]
    [InlineData("x7k2m9q4p1")]
    [InlineData("senha diferente")]
    public void Evaluate_IdentifiesWeakButUsablePasswords(string password)
    {
        PasswordStrengthLevel level = PasswordStrength.Evaluate(password);

        Assert.True(level is PasswordStrengthLevel.Weak or PasswordStrengthLevel.Medium);
    }

    [Theory]
    [InlineData("SenhaForte!2026")]
    [InlineData("Tr3maux!Cofre#2026")]
    public void Evaluate_IdentifiesStrongPasswords(string password)
    {
        Assert.Equal(PasswordStrengthLevel.Strong, PasswordStrength.Evaluate(password));
    }

    [Fact]
    public void GetLabel_ReturnsLocalizedLabels()
    {
        Assert.Equal("Muito fraca", PasswordStrength.GetLabel(PasswordStrengthLevel.VeryWeak));
        Assert.Equal("Fraca", PasswordStrength.GetLabel(PasswordStrengthLevel.Weak));
        Assert.Equal("Média", PasswordStrength.GetLabel(PasswordStrengthLevel.Medium));
        Assert.Equal("Forte", PasswordStrength.GetLabel(PasswordStrengthLevel.Strong));
    }
}
