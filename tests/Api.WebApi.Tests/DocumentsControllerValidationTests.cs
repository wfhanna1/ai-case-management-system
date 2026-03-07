using Api.Application.Commands;
using Api.Application.DTOs;
using Api.Application.Queries;
using Api.WebApi;
using Api.WebApi.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel;

namespace Api.WebApi.Tests;

public sealed class DocumentsControllerValidationTests
{
    private readonly DocumentsController _controller;

    public DocumentsControllerValidationTests()
    {
        var tenantContext = new StubTenantContext(Guid.NewGuid());
        _controller = new DocumentsController(
            new SubmitDocumentHandler(null!, null!, null!, NullLogger<SubmitDocumentHandler>.Instance),
            new GetDocumentByIdHandler(null!),
            new ListDocumentsByTenantHandler(null!),
            new SearchDocumentsHandler(null!),
            new GetDashboardStatsHandler(null!, null!),
            tenantContext);
    }

    [Fact]
    public async Task Submit_null_file_returns_400_with_missing_file()
    {
        var result = await _controller.Submit(null!, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<DocumentDto>>(objectResult.Value);
        Assert.Equal("MISSING_FILE", response.Error!.Code);
    }

    [Fact]
    public async Task Submit_empty_file_returns_400_with_empty_file()
    {
        var file = new FormFile(Stream.Null, 0, 0, "file", "empty.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf"
        };

        var result = await _controller.Submit(file, CancellationToken.None);

        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ApiResponse<DocumentDto>>(objectResult.Value);
        Assert.Equal("EMPTY_FILE", response.Error!.Code);
    }

    private sealed class StubTenantContext : ITenantContext
    {
        public TenantId? TenantId { get; }
        public StubTenantContext(Guid id) => TenantId = new TenantId(id);
    }
}
