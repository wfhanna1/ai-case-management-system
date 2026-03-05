using SharedKernel;
using Xunit;

namespace SharedKernel.Tests;

public class EntityTests
{
    private sealed class TestEntity : Entity<Guid>
    {
        public TestEntity(Guid id) : base(id) { }
    }

    [Fact]
    public void Entity_WithSameId_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);

        Assert.Equal(a, b);
    }

    [Fact]
    public void Entity_WithDifferentIds_AreNotEqual()
    {
        var a = new TestEntity(Guid.NewGuid());
        var b = new TestEntity(Guid.NewGuid());

        Assert.NotEqual(a, b);
    }

    [Fact]
    public void Entity_EqualsOperator_WorksForSameId()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);

        Assert.True(a == b);
    }

    [Fact]
    public void Entity_NotEqualsOperator_WorksForDifferentId()
    {
        var a = new TestEntity(Guid.NewGuid());
        var b = new TestEntity(Guid.NewGuid());

        Assert.True(a != b);
    }

    [Fact]
    public void Entity_GetHashCode_SameForEqualEntities()
    {
        var id = Guid.NewGuid();
        var a = new TestEntity(id);
        var b = new TestEntity(id);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Entity_Id_IsSetFromConstructor()
    {
        var id = Guid.NewGuid();
        var entity = new TestEntity(id);

        Assert.Equal(id, entity.Id);
    }

    [Fact]
    public void Entity_EqualToNull_ReturnsFalse()
    {
        var entity = new TestEntity(Guid.NewGuid());

        Assert.False(entity.Equals(null));
    }

    [Fact]
    public void Entity_EqualToDifferentType_ReturnsFalse()
    {
        var entity = new TestEntity(Guid.NewGuid());

        Assert.False(entity.Equals("not an entity"));
    }
}
