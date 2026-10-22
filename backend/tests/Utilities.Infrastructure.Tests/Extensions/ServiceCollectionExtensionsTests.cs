using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Utilities.Infrastructure.Configuration;
using Utilities.Infrastructure.Extensions;
using Xunit;

namespace Utilities.Infrastructure.Tests.Extensions;

public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddInfrastructure_DefaultCadCommand_UsesVendorReleaseWorkerWhenPresent()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        var localWorkerPath = files.CreateFile(Path.Combine(
            "vendor",
            "cad-converter-worker",
            "target",
            "release",
            DefaultCadCommandName));

        using var provider = CreateProvider(contentRoot);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(Path.GetFullPath(localWorkerPath), options.CadConverterExecutablePath);
    }

    [Fact]
    public void AddInfrastructure_DefaultCadCommandWithoutSiblingWorker_LeavesPathLookup()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        using var provider = CreateProvider(contentRoot);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(DefaultCadCommandName, options.CadConverterExecutablePath);
    }

    [Fact]
    public void AddInfrastructure_NonDefaultBareCadCommand_DoesNotUseVendorWorker()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        files.CreateFile(Path.Combine(
            "vendor",
            "cad-converter-worker",
            "target",
            "release",
            DefaultCadCommandName));
        const string customCommandName = "custom-cad-converter";
        using var provider = CreateProvider(contentRoot, customCommandName);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(customCommandName, options.CadConverterExecutablePath);
    }

    [Fact]
    public void AddInfrastructure_ExplicitCadPath_DoesNotUseSiblingWorker()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        files.CreateFile(Path.Combine(
            "vendor",
            "cad-converter-worker",
            "target",
            "release",
            DefaultCadCommandName));
        var configuredWorkerPath = files.CreateFile(Path.Combine(
            "configured",
            DefaultCadCommandName));
        using var provider = CreateProvider(contentRoot, configuredWorkerPath);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(configuredWorkerPath, options.CadConverterExecutablePath);
    }

    [Fact]
    public void AddInfrastructure_DefaultPdfDocxCommand_UsesVendorVirtualEnvironmentWhenPresent()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        var localPythonPath = files.CreateFile(Path.Combine(
            "vendor",
            "pdf-docx-converter-worker",
            VirtualEnvironmentPythonPath));
        var localWorkerScript = files.CreateFile(Path.Combine(
            "vendor",
            "pdf-docx-converter-worker",
            "worker.py"));

        using var provider = CreateProvider(contentRoot);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(Path.GetFullPath(localPythonPath), options.PdfDocxConverterExecutablePath);
        Assert.Equal(Path.GetFullPath(localWorkerScript), options.PdfDocxConverterScriptPath);
    }

    [Fact]
    public void AddInfrastructure_DefaultPdfDocxCommand_RequiresBothPythonAndWorkerScript()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        files.CreateFile(Path.Combine(
            "vendor",
            "pdf-docx-converter-worker",
            VirtualEnvironmentPythonPath));

        using var provider = CreateProvider(contentRoot);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(DefaultPdfDocxCommandName, options.PdfDocxConverterExecutablePath);
        Assert.Null(options.PdfDocxConverterScriptPath);
    }

    [Fact]
    public void AddInfrastructure_DefaultPdfDocxCommandWithoutSiblingWorker_LeavesPathLookup()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        using var provider = CreateProvider(contentRoot);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(DefaultPdfDocxCommandName, options.PdfDocxConverterExecutablePath);
        Assert.Null(options.PdfDocxConverterScriptPath);
    }

    [Fact]
    public void AddInfrastructure_ExplicitPdfDocxPath_DoesNotUseSiblingWorker()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        files.CreateFile(Path.Combine(
            "vendor",
            "pdf-docx-converter-worker",
            VirtualEnvironmentPythonPath));
        files.CreateFile(Path.Combine(
            "vendor",
            "pdf-docx-converter-worker",
            "worker.py"));
        var configuredWorkerPath = files.CreateFile(Path.Combine(
            "configured",
            DefaultPdfDocxCommandName));

        using var provider = CreateProvider(
            contentRoot,
            pdfDocxConverterExecutablePath: configuredWorkerPath);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal(configuredWorkerPath, options.PdfDocxConverterExecutablePath);
        Assert.Null(options.PdfDocxConverterScriptPath);
    }

    [Fact]
    public void AddInfrastructure_ExplicitPdfDocxScript_ResolvesFromContentRoot()
    {
        using var files = new TemporaryDirectory();
        var contentRoot = files.CreateDirectory(Path.Combine("src", "Utilities.Api"));
        const string relativeScriptPath = "workers/pdf-docx/worker.py";

        using var provider = CreateProvider(
            contentRoot,
            pdfDocxConverterExecutablePath: "python",
            pdfDocxConverterScriptPath: relativeScriptPath);

        var options = provider.GetRequiredService<IOptions<FileConversionOptions>>().Value;

        Assert.Equal("python", options.PdfDocxConverterExecutablePath);
        Assert.Equal(
            Path.GetFullPath(Path.Combine(contentRoot, relativeScriptPath)),
            options.PdfDocxConverterScriptPath);
    }

    private static string DefaultCadCommandName => OperatingSystem.IsWindows()
        ? "utilities-cad-converter.exe"
        : "utilities-cad-converter";

    private static string DefaultPdfDocxCommandName => OperatingSystem.IsWindows()
        ? "utilities-pdf-docx-converter.exe"
        : "utilities-pdf-docx-converter";

    private static string VirtualEnvironmentPythonPath => OperatingSystem.IsWindows()
        ? Path.Combine(".venv", "Scripts", "python.exe")
        : Path.Combine(".venv", "bin", "python");

    private static ServiceProvider CreateProvider(
        string contentRoot,
        string? cadConverterExecutablePath = null,
        string? pdfDocxConverterExecutablePath = null,
        string? pdfDocxConverterScriptPath = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=utilities;Username=test;Password=test",
        };
        if (cadConverterExecutablePath is not null)
        {
            settings["FileConversion:CadConverterExecutablePath"] = cadConverterExecutablePath;
        }

        if (pdfDocxConverterExecutablePath is not null)
        {
            settings["FileConversion:PdfDocxConverterExecutablePath"] = pdfDocxConverterExecutablePath;
        }

        if (pdfDocxConverterScriptPath is not null)
        {
            settings["FileConversion:PdfDocxConverterScriptPath"] = pdfDocxConverterScriptPath;
        }

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build();
        var environment = new TestHostEnvironment
        {
            ContentRootPath = contentRoot,
        };

        return new ServiceCollection()
            .AddInfrastructure(configuration, environment)
            .BuildServiceProvider();
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "Utilities.Infrastructure.Tests";
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(
            Path.GetTempPath(),
            "utilities-infrastructure-tests",
            Guid.NewGuid().ToString("N"));

        public TemporaryDirectory() => Directory.CreateDirectory(_path);

        public string CreateDirectory(string relativePath)
        {
            var path = Path.Combine(_path, relativePath);
            Directory.CreateDirectory(path);
            return path;
        }

        public string CreateFile(string relativePath)
        {
            var path = Path.Combine(_path, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllBytes(path, []);
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_path))
            {
                Directory.Delete(_path, recursive: true);
            }
        }
    }
}
