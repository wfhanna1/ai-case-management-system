using Api.Domain.Aggregates;

namespace Api.Domain.Tests;

public sealed class FormTemplateIdTests
{
    [Fact]
    public void New_creates_unique_ids()
    {
        var id1 = FormTemplateId.New();
        var id2 = FormTemplateId.New();

        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void Constructor_rejects_empty_guid()
    {
        Assert.Throws<ArgumentException>(() => new FormTemplateId(Guid.Empty));
    }

    [Fact]
    public void Equal_ids_are_equal()
    {
        var guid = Guid.NewGuid();
        var id1 = new FormTemplateId(guid);
        var id2 = new FormTemplateId(guid);

        Assert.Equal(id1, id2);
    }

    [Fact]
    public void ToString_returns_guid_string()
    {
        var guid = Guid.NewGuid();
        var id = new FormTemplateId(guid);

        Assert.Equal(guid.ToString(), id.ToString());
    }
}
