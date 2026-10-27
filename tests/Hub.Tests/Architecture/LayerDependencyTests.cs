using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Hub.Tests.Architecture;

public sealed class LayerDependencyTests
{
    private static readonly string Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../"));

    [Fact]
    public void Domain_has_no_project_or_package_dependencies()
    {
        var project = XDocument.Load(Path.Combine(Root, "src/Hub.Domain/Hub.Domain.csproj"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("PackageReference"));
        var references = typeof(Document).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "");
        Assert.DoesNotContain(references, n => n.StartsWith("Hub.") || n.StartsWith("Microsoft.EntityFrameworkCore") || n.StartsWith("Microsoft.AspNetCore"));
    }

    [Fact]
    public void Application_depends_only_on_domain_and_framework_abstractions()
    {
        var references = typeof(DocumentService).Assembly.GetReferencedAssemblies().Select(a => a.Name ?? "").ToList();
        Assert.Contains("Hub.Domain", references);
        Assert.DoesNotContain(references, n => n.StartsWith("Hub.Infrastructure") || n.StartsWith("Hub.Web") || n.StartsWith("Microsoft.EntityFrameworkCore") || n.StartsWith("Microsoft.AspNetCore") || n is "Minio" or "StackExchange.Redis" or "UglyToad.PdfPig");
        var project = XDocument.Load(Path.Combine(Root, "src/Hub.Application/Hub.Application.csproj"));
        Assert.Single(project.Descendants("ProjectReference"));
        Assert.EndsWith("Hub.Domain.csproj", project.Descendants("ProjectReference").Single().Attribute("Include")!.Value);
    }

    [Fact]
    public void Hosts_do_not_access_database_or_provider_sdks()
    {
        foreach (var host in new[] { "Hub.Web", "Hub.Gateway", "Hub.Tools", "Hub.Worker" })
            foreach (var file in SourceFiles(Path.Combine(Root, "src", host)))
            {
                var code = File.ReadAllText(file);
                Assert.DoesNotContain("Microsoft.EntityFrameworkCore", code);
                Assert.DoesNotContain("IDbContextFactory", code);
                Assert.DoesNotContain("HubDb", code);
                Assert.DoesNotContain("using Minio", code);
                Assert.DoesNotContain("using StackExchange.Redis", code);
            }
    }

    [Fact]
    public void Every_source_type_has_its_own_named_file()
    {
        var declaration = new Regex(@"^\s*(?:(?:public|internal|private|protected|sealed|abstract|static|partial|readonly)\s+)*(?:class|record(?:\s+(?:class|struct))?|interface|enum|struct)\s+(\w+)", RegexOptions.Multiline);
        foreach (var file in SourceFiles(Path.Combine(Root, "src")).Concat(SourceFiles(Path.Combine(Root, "tests"))))
        {
            var types = declaration.Matches(File.ReadAllText(file)).Select(m => m.Groups[1].Value).ToList();
            Assert.True(types.Count <= 1, $"Multiple types in {file}: {string.Join(", ", types)}");
            if (types.Count == 1) Assert.StartsWith(types[0], Path.GetFileNameWithoutExtension(file));
        }
    }

    private static IEnumerable<string> SourceFiles(string directory) => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
        .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(part => part is "obj" or "bin"));
}
