using System.IO.Compression;
using System.Text;
using Utilities.Infrastructure.External.Conversion;
using Xunit;

namespace Utilities.Infrastructure.Tests.External.Conversion;

public sealed class DocxValidatorTests
{
    [Fact]
    public async Task ValidateAsync_MinimalDocx_Succeeds()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("document.docx");
        WriteDocx(path);

        await DocxValidator.ValidateAsync(path, CancellationToken.None);
    }

    [Fact]
    public async Task ValidateAsync_MissingMainDocument_ThrowsInvalidData()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("missing-document.docx");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
        }

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DocxValidator.ValidateAsync(path, CancellationToken.None));
    }

    [Fact]
    public async Task ValidateAsync_InvalidMainDocumentXml_ThrowsInvalidData()
    {
        using var files = new TemporaryDirectory();
        var path = files.GetPath("invalid-document.docx");
        WriteDocx(path, "<not-document />");

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            DocxValidator.ValidateAsync(path, CancellationToken.None));
    }

    internal static void WriteDocx(
        string path,
        string documentXml = "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body /></w:document>")
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        WriteEntry(archive, "[Content_Types].xml", ContentTypesXml);
        WriteEntry(archive, "word/document.xml", documentXml);
    }

    private const string ContentTypesXml =
        "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
        "<Override PartName=\"/word/document.xml\" " +
        "ContentType=\"application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml\" />" +
        "</Types>";

    private static void WriteEntry(
        ZipArchive archive,
        string name,
        string content)
    {
        var entry = archive.CreateEntry(name);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-docx-validator-tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string GetPath(string fileName) => Path.Combine(_path, fileName);

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
