using System.IO.Compression;
using System.Xml;
using System.Xml.Linq;

namespace Utilities.Infrastructure.External.Conversion;

public static class DocxValidator
{
    private const long MaximumContentTypesXmlBytes = 1024 * 1024;
    private const long MaximumDocumentXmlBytes = 64L * 1024 * 1024;
    private const string ContentTypesEntryName = "[Content_Types].xml";
    private const string DocumentEntryName = "word/document.xml";
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string TransitionalWordNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string StrictWordNamespace = "http://purl.oclc.org/ooxml/wordprocessingml/main";
    private const string DocumentContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml";

    public static async Task ValidateAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);

            if (stream.Length == 0)
            {
                throw new InvalidDataException("The DOCX file is empty.");
            }

            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: false);
            ValidateEntryPaths(archive);

            var contentTypesEntry = GetUniqueRequiredEntry(archive, ContentTypesEntryName);
            var documentEntry = GetUniqueRequiredEntry(archive, DocumentEntryName);

            var contentTypes = await LoadXmlAsync(
                contentTypesEntry,
                MaximumContentTypesXmlBytes,
                cancellationToken);
            var document = await LoadXmlAsync(
                documentEntry,
                MaximumDocumentXmlBytes,
                cancellationToken);

            ValidateContentTypes(contentTypes);
            ValidateDocumentRoot(document);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException or
                                          NotSupportedException or
                                          UnauthorizedAccessException or
                                          XmlException)
        {
            throw new InvalidDataException("The output is not a valid DOCX package.", exception);
        }
    }

    private static void ValidateEntryPaths(ZipArchive archive)
    {
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName;
            if (string.IsNullOrEmpty(path) ||
                path.StartsWith('/') ||
                path.Contains('\\') ||
                path.Split('/').Any(segment => segment is "." or ".."))
            {
                throw new InvalidDataException("The DOCX package contains an invalid entry path.");
            }
        }
    }

    private static ZipArchiveEntry GetUniqueRequiredEntry(
        ZipArchive archive,
        string entryName)
    {
        var entries = archive.Entries
            .Where(entry => string.Equals(entry.FullName, entryName, StringComparison.Ordinal))
            .Take(2)
            .ToArray();

        return entries.Length == 1
            ? entries[0]
            : throw new InvalidDataException(
                $"The DOCX package must contain exactly one '{entryName}' entry.");
    }

    private static async Task<XDocument> LoadXmlAsync(
        ZipArchiveEntry entry,
        long maximumBytes,
        CancellationToken cancellationToken)
    {
        if (entry.Length <= 0 || entry.Length > maximumBytes)
        {
            throw new InvalidDataException(
                $"The DOCX entry '{entry.FullName}' has an invalid size.");
        }

        await using var entryStream = entry.Open();
        using var reader = XmlReader.Create(entryStream, new XmlReaderSettings
        {
            Async = true,
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = maximumBytes,
        });

        return await XDocument.LoadAsync(reader, LoadOptions.None, cancellationToken);
    }

    private static void ValidateContentTypes(XDocument contentTypes)
    {
        var root = contentTypes.Root;
        if (root?.Name != XName.Get("Types", ContentTypesNamespace))
        {
            throw new InvalidDataException("The DOCX content types manifest is invalid.");
        }

        var hasDocumentOverride = root
            .Elements(XName.Get("Override", ContentTypesNamespace))
            .Any(element =>
                string.Equals(
                    (string?)element.Attribute("PartName"),
                    "/word/document.xml",
                    StringComparison.Ordinal) &&
                string.Equals(
                    (string?)element.Attribute("ContentType"),
                    DocumentContentType,
                    StringComparison.Ordinal));

        if (!hasDocumentOverride)
        {
            throw new InvalidDataException(
                "The DOCX content types manifest does not declare the main document.");
        }
    }

    private static void ValidateDocumentRoot(XDocument document)
    {
        var root = document.Root;
        if (root is null ||
            root.Name.LocalName != "document" ||
            root.Name.NamespaceName is not (TransitionalWordNamespace or StrictWordNamespace))
        {
            throw new InvalidDataException("The DOCX main document XML is invalid.");
        }
    }
}
