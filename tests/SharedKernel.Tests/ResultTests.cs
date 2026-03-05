using SharedKernel;
using Xunit;

namespace SharedKernel.Tests;

public class ResultTests
{
    [Fact]
    public void Result_Success_IsSuccess()
    {
        var result = Result<int>.Success(42);

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Result_Success_HasCorrectValue()
    {
        var result = Result<string>.Success("hello");

        Assert.Equal("hello", result.Value);
    }

    [Fact]
    public void Result_Failure_IsFailure()
    {
        var result = Result<int>.Failure(new Error("NOT_FOUND", "Item not found"));

        Assert.True(result.IsFailure);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Result_Failure_HasCorrectError()
    {
        var error = new Error("INVALID", "Invalid input");
        var result = Result<int>.Failure(error);

        Assert.Equal(error.Code, result.Error.Code);
        Assert.Equal(error.Message, result.Error.Message);
    }

    [Fact]
    public void Result_Success_AccessingError_ThrowsInvalidOperation()
    {
        var result = Result<int>.Success(1);

        Assert.Throws<InvalidOperationException>(() => _ = result.Error);
    }

    [Fact]
    public void Result_Failure_AccessingValue_ThrowsInvalidOperation()
    {
        var result = Result<int>.Failure(new Error("ERR", "error"));

        Assert.Throws<InvalidOperationException>(() => _ = result.Value);
    }

    [Fact]
    public void Error_HasCodeAndMessage()
    {
        var error = new Error("E001", "Something went wrong");

        Assert.Equal("E001", error.Code);
        Assert.Equal("Something went wrong", error.Message);
    }

    [Fact]
    public void Result_Success_WithNullValue_IsAllowed()
    {
        var result = Result<string?>.Success(null);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }
}
