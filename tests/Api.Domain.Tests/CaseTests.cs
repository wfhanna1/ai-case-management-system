using Api.Domain.Aggregates;
using SharedKernel;

namespace Api.Domain.Tests;

public sealed class CaseTests
{
    private readonly TenantId _tenantId = TenantId.New();

    [Fact]
    public void Create_sets_TenantId_SubjectName_CreatedAt_and_UpdatedAt()
    {
        var before = DateTimeOffset.UtcNow;

        var caseAggregate = Case.Create(_tenantId, "Jane Doe");

        Assert.Equal(_tenantId, caseAggregate.TenantId);
        Assert.Equal("Jane Doe", caseAggregate.SubjectName);
        Assert.True(caseAggregate.CreatedAt >= before);
        Assert.True(caseAggregate.UpdatedAt >= before);
        Assert.Empty(caseAggregate.Documents);
    }

    [Fact]
    public void Create_with_null_subjectName_throws_ArgumentException()
    {
        Assert.Throws<ArgumentNullException>(() => Case.Create(_tenantId, null!));
    }

    [Fact]
    public void Create_with_whitespace_subjectName_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Case.Create(_tenantId, "   "));
    }

    [Fact]
    public void Create_with_empty_subjectName_throws_ArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Case.Create(_tenantId, ""));
    }

    [Fact]
    public void LinkDocument_adds_document_and_bumps_UpdatedAt()
    {
        var caseAggregate = Case.Create(_tenantId, "Jane Doe");
        var originalUpdatedAt = caseAggregate.UpdatedAt;
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        caseAggregate.LinkDocument(doc);

        Assert.Single(caseAggregate.Documents);
        Assert.Equal(doc.Id, caseAggregate.Documents[0].Id);
        Assert.True(caseAggregate.UpdatedAt >= originalUpdatedAt);
    }

    [Fact]
    public void LinkDocument_with_same_document_twice_does_not_add_duplicate()
    {
        var caseAggregate = Case.Create(_tenantId, "Jane Doe");
        var doc = IntakeDocument.Submit(_tenantId, "test.pdf", "storage/key");

        caseAggregate.LinkDocument(doc);
        caseAggregate.LinkDocument(doc);

        Assert.Single(caseAggregate.Documents);
    }

    [Fact]
    public void LinkDocument_with_null_throws_ArgumentNullException()
    {
        var caseAggregate = Case.Create(_tenantId, "Jane Doe");

        Assert.Throws<ArgumentNullException>(() => caseAggregate.LinkDocument(null!));
    }
}
