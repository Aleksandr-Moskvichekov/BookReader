using System.IO.Compression;
using System.Text;
using System.Xml;

namespace BookReader.Services;

public static class UploadedBookFormatValidator
{
    private const string EpubMimeType = "application/epub+zip";
    private const string ContainerNamespace = "urn:oasis:names:tc:opendocument:xmlns:container";
    private const string PackageNamespace = "http://www.idpf.org/2007/opf";

    public static string GetContentType(string fileName) => Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant() switch
    {
        ".fb2" => "application/x-fictionbook+xml",
        ".epub" => EpubMimeType,
        ".pdf" => "application/pdf",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };

    public static bool IsValid(string fileName, Stream content)
    {
        var extension = Path.GetExtension(Path.GetFileName(fileName));
        if (string.IsNullOrEmpty(extension))
            return false;

        try
        {
            content.Position = 0;
            return extension.ToLowerInvariant() switch
            {
                ".fb2" => IsFb2(content),
                ".epub" => IsEpub(content),
                ".pdf" => IsPdf(content),
                ".txt" => IsText(content),
                _ => false
            };
        }
        catch (Exception exception) when (exception is InvalidDataException
            or XmlException
            or DecoderFallbackException
            or IOException
            or ArgumentException)
        {
            return false;
        }
    }

    public static bool IsValid(string fileName, string contentType, Stream content)
    {
        var expectedContentType = GetContentType(fileName);
        var suppliedContentType = contentType.Split(';', 2)[0].Trim();
        return expectedContentType != "application/octet-stream"
            && string.Equals(expectedContentType, suppliedContentType, StringComparison.OrdinalIgnoreCase)
            && IsValid(fileName, content);
    }

    public static string? GetCoverContentType(string fileName) => Path.GetExtension(Path.GetFileName(fileName)).ToLowerInvariant() switch
    {
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".webp" => "image/webp",
        _ => null
    };

    public static bool IsValidCover(string fileName, string contentType, Stream content)
    {
        var expectedContentType = GetCoverContentType(fileName);
        var suppliedContentType = contentType.Split(';', 2)[0].Trim();
        if (expectedContentType is null
            || !string.Equals(expectedContentType, suppliedContentType, StringComparison.OrdinalIgnoreCase))
            return false;

        try
        {
            content.Position = 0;
            return expectedContentType switch
            {
                "image/jpeg" => StartsWith(content, [0xFF, 0xD8, 0xFF]),
                "image/png" => StartsWith(content, [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A]),
                "image/webp" => IsWebp(content),
                _ => false
            };
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static bool StartsWith(Stream content, byte[] signature)
    {
        var actual = new byte[signature.Length];
        var bytesRead = 0;
        while (bytesRead < actual.Length)
        {
            var count = content.Read(actual, bytesRead, actual.Length - bytesRead);
            if (count == 0)
                return false;
            bytesRead += count;
        }

        return actual.AsSpan().SequenceEqual(signature);
    }

    private static bool IsWebp(Stream content)
    {
        var header = new byte[12];
        var bytesRead = 0;
        while (bytesRead < header.Length)
        {
            var count = content.Read(header, bytesRead, header.Length - bytesRead);
            if (count == 0)
                return false;
            bytesRead += count;
        }

        return header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
    }

    private static bool IsFb2(Stream content)
    {
        using var reader = XmlReader.Create(content, CreateXmlSettings());
        string? rootName = null;

        while (reader.Read())
        {
            if (rootName is null && reader.NodeType == XmlNodeType.Element)
                rootName = reader.LocalName;
        }

        return rootName == "FictionBook";
    }

    private static bool IsEpub(Stream content)
    {
        using var archive = new ZipArchive(content, ZipArchiveMode.Read, leaveOpen: true);
        if (archive.Entries.Count == 0)
            return false;

        var mimeEntry = archive.Entries[0];
        if (mimeEntry.FullName != "mimetype"
            || mimeEntry.CompressedLength != mimeEntry.Length
            || mimeEntry.Length != EpubMimeType.Length)
            return false;

        using (var mimeStream = mimeEntry.Open())
        using (var reader = new StreamReader(mimeStream, Encoding.ASCII))
        {
            if (reader.ReadToEnd() != EpubMimeType)
                return false;
        }

        var containerEntry = archive.GetEntry("META-INF/container.xml");
        if (containerEntry is null)
            return false;

        string? packagePath;
        using (var containerStream = containerEntry.Open())
        {
            var container = ReadXmlRoot(containerStream, out packagePath);
            if (container.Name != "container" || container.Namespace != ContainerNamespace)
                return false;
        }

        if (string.IsNullOrWhiteSpace(packagePath)
            || packagePath.StartsWith('/')
            || packagePath.Contains('\\')
            || packagePath.Split('/').Any(segment => segment == ".."))
            return false;

        var packageEntry = archive.GetEntry(packagePath);
        if (packageEntry is null)
            return false;

        using var packageStream = packageEntry.Open();
        var package = ReadXmlRoot(packageStream, out _);
        return package.Name == "package" && package.Namespace == PackageNamespace;
    }

    private static bool IsPdf(Stream content)
    {
        Span<byte> signature = stackalloc byte[5];
        return content.Read(signature) == signature.Length
            && signature.SequenceEqual("%PDF-"u8);
    }

    private static bool IsText(Stream content)
    {
        var prefix = new byte[4];
        var prefixLength = content.Read(prefix, 0, prefix.Length);
        content.Position = 0;

        Encoding encoding = prefixLength >= 4 && prefix.AsSpan(0, 4).SequenceEqual(new byte[] { 0x00, 0x00, 0xFE, 0xFF })
            ? new UTF32Encoding(true, true, true)
            : prefixLength >= 4 && prefix.AsSpan(0, 4).SequenceEqual(new byte[] { 0xFF, 0xFE, 0x00, 0x00 })
                ? new UTF32Encoding(false, true, true)
                : prefixLength >= 2 && prefix[0] == 0xFE && prefix[1] == 0xFF
                    ? new UnicodeEncoding(true, true, true)
                    : prefixLength >= 2 && prefix[0] == 0xFF && prefix[1] == 0xFE
                        ? new UnicodeEncoding(false, true, true)
                        : new UTF8Encoding(true, true);

        using var reader = new StreamReader(content, encoding, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var buffer = new char[4096];
        int charsRead;
        while ((charsRead = reader.Read(buffer, 0, buffer.Length)) > 0)
        {
            for (var i = 0; i < charsRead; i++)
            {
                if (char.IsControl(buffer[i]) && buffer[i] is not ('\r' or '\n' or '\t' or '\f'))
                    return false;
            }
        }

        return true;
    }

    private static (string Name, string Namespace) ReadXmlRoot(Stream content, out string? packagePath)
    {
        using var reader = XmlReader.Create(content, CreateXmlSettings());
        string? rootName = null;
        var rootNamespace = string.Empty;
        packagePath = null;

        while (reader.Read())
        {
            if (reader.NodeType != XmlNodeType.Element)
                continue;

            if (rootName is null)
            {
                rootName = reader.LocalName;
                rootNamespace = reader.NamespaceURI;
            }
            else if (rootName == "container"
                && reader.LocalName == "rootfile"
                && reader.NamespaceURI == ContainerNamespace)
            {
                packagePath ??= reader.GetAttribute("full-path");
            }
        }

        return (rootName ?? string.Empty, rootNamespace);
    }

    private static XmlReaderSettings CreateXmlSettings() => new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        CloseInput = false
    };
}
