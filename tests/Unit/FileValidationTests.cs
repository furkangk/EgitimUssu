using EgitimUssu.Shared.Kernel.Storage;

namespace EgitimUssu.Tests.Unit;

/// <summary>
/// P04/K-03: Yükleme sınırı ve tür beyaz listesi. Sunucu tarafı kural — istemciye güvenilmez.
/// </summary>
public sealed class FileValidationTests
{
    [Fact]
    public void Should_Reject_Unsupported_Extension()
    {
        var result = FileValidation.Validate("virus.exe", 1024);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.unsupported_type", result.Error.Code);
    }

    [Fact]
    public void Should_Reject_File_Without_Extension()
    {
        var result = FileValidation.Validate("odev", 1024);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.unsupported_type", result.Error.Code);
    }

    [Fact]
    public void Should_Reject_Too_Large_File()
    {
        var result = FileValidation.Validate("odev.pdf", FileValidation.MaxSizeBytes + 1);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.file_too_large", result.Error.Code);
    }

    [Fact]
    public void Should_Reject_Empty_File()
    {
        var result = FileValidation.Validate("odev.pdf", 0);

        Assert.True(result.IsFailure);
        Assert.Equal("storage.empty_file", result.Error.Code);
    }

    [Fact]
    public void Should_Accept_Pdf_Within_Limit()
        => Assert.True(FileValidation.Validate("odev.pdf", 1024).IsSuccess);

    [Fact]
    public void Should_Accept_File_At_Exact_Limit()
        => Assert.True(FileValidation.Validate("odev.pdf", FileValidation.MaxSizeBytes).IsSuccess);

    [Fact]
    public void Should_Be_Case_Insensitive()
        => Assert.True(FileValidation.Validate("FOTO.JPG", 1024).IsSuccess);

    [Theory]
    [InlineData(".pdf")]
    [InlineData(".png")]
    [InlineData(".jpg")]
    [InlineData(".jpeg")]
    [InlineData(".webp")]
    [InlineData(".doc")]
    [InlineData(".docx")]
    [InlineData(".xls")]
    [InlineData(".xlsx")]
    [InlineData(".ppt")]
    [InlineData(".pptx")]
    [InlineData(".txt")]
    [InlineData(".zip")]
    public void Should_Accept_Every_Allowed_Extension(string extension)
        => Assert.True(FileValidation.Validate($"dosya{extension}", 1024).IsSuccess);
}
