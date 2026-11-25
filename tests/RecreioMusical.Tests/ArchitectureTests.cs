namespace RecreioMusical.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Domain_should_not_depend_on_outer_layers()
    {
        var references = ReferencedAssembliesOf(typeof(RecreioMusical.Domain.PlaylistRules));

        Assert.DoesNotContain("RecreioMusical.Application", references);
        Assert.DoesNotContain("RecreioMusical.Infrastructure", references);
        Assert.DoesNotContain("RecreioMusical.Web", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
    }

    [Fact]
    public void Application_should_not_depend_on_infrastructure_or_web()
    {
        var references = ReferencedAssembliesOf(typeof(RecreioMusical.Application.DependencyInjection));

        Assert.DoesNotContain("RecreioMusical.Infrastructure", references);
        Assert.DoesNotContain("RecreioMusical.Web", references);
        Assert.DoesNotContain("Microsoft.EntityFrameworkCore", references);
        Assert.DoesNotContain("Microsoft.AspNetCore", references);
    }

    private static string[] ReferencedAssembliesOf(Type type) =>
        type.Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name)
            .Where(name => name is not null)
            .Select(name => name!)
            .ToArray();
}
