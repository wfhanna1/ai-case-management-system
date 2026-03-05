using SharedKernel;
using Xunit;

namespace SharedKernel.Tests;

public class ValueObjectTests
{
    private sealed class Money : ValueObject
    {
        public decimal Amount { get; }
        public string Currency { get; }

        public Money(decimal amount, string currency)
        {
            Amount = amount;
            Currency = currency;
        }

        protected override IEnumerable<object> GetEqualityComponents()
        {
            yield return Amount;
            yield return Currency;
        }
    }

    [Fact]
    public void ValueObject_WithSameComponents_AreEqual()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(10.00m, "USD");

        Assert.Equal(a, b);
    }

    [Fact]
    public void ValueObject_WithDifferentComponents_AreNotEqual()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(20.00m, "USD");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueObject_DifferentCurrency_AreNotEqual()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(10.00m, "EUR");

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void ValueObject_EqualsOperator_TrueForSameValues()
    {
        var a = new Money(5.00m, "GBP");
        var b = new Money(5.00m, "GBP");

        Assert.True(a == b);
    }

    [Fact]
    public void ValueObject_NotEqualsOperator_TrueForDifferentValues()
    {
        var a = new Money(5.00m, "GBP");
        var b = new Money(5.00m, "USD");

        Assert.True(a != b);
    }

    [Fact]
    public void ValueObject_GetHashCode_SameForEqualObjects()
    {
        var a = new Money(10.00m, "USD");
        var b = new Money(10.00m, "USD");

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ValueObject_EqualToNull_ReturnsFalse()
    {
        var a = new Money(10.00m, "USD");

        Assert.False(a.Equals(null));
    }
}
