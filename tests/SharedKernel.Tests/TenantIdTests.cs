using SharedKernel;
using Xunit;

namespace SharedKernel.Tests;

public class TenantIdTests
{
    [Fact]
    public void TenantId_WithSameGuid_AreEqual()
    {
        var guid = Guid.NewGuid();
        var a = new TenantId(guid);
        var b = new TenantId(guid);

        Assert.Equal(a, b);
    }

    [Fact]
    public void TenantId_WithDifferentGuids_AreNotEqual()
    {
        var a = new TenantId(Guid.NewGuid());
        var b = new TenantId(Guid.NewGuid());

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void TenantId_EmptyGuid_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => new TenantId(Guid.Empty));
    }

    [Fact]
    public void TenantId_Value_ReturnsUnderlyingGuid()
    {
        var guid = Guid.NewGuid();
        var tenantId = new TenantId(guid);

        Assert.Equal(guid, tenantId.Value);
    }

    [Fact]
    public void TenantId_New_CreatesNonEmptyId()
    {
        var tenantId = TenantId.New();

        Assert.NotEqual(Guid.Empty, tenantId.Value);
    }

    [Fact]
    public void TenantId_ToString_ReturnsGuidString()
    {
        var guid = Guid.NewGuid();
        var tenantId = new TenantId(guid);

        Assert.Equal(guid.ToString(), tenantId.ToString());
    }

    [Fact]
    public void TenantId_IsValueObject_EqualityByValue()
    {
        var guid = Guid.NewGuid();
        var a = new TenantId(guid);
        var b = new TenantId(guid);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }
}
