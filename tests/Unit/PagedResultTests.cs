using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class PagedResultTests
{
    [Fact]
    public void TotalPages_Should_Be_Computed_From_TotalCount()
    {
        var paged = new PagedResult<int>([1, 2, 3], 1, 3, 8);

        Assert.Equal(3, paged.TotalPages);
    }
}
