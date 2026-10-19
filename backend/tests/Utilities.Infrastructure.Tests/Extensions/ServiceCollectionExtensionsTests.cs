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

    private static string DefaultCadCommandName => OperatingSystem.IsWindows()
        ? "utilities-cad-converter.exe"
        : "utilities-cad-converter";

    private static ServiceProvider CreateProvider(
        string contentRoot,
        string? cadConverterExecutablePath = null)
    {
        var settings = new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=utilities;Username=test;Password=test",
        };
        if (cadConverterExecutablePath is not null)
        {
            settings["FileConversion:CadConverterExecutablePath"] = cadConverterExecutablePath;
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
