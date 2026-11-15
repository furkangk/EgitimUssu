namespace EgitimUssu.Shared.Kernel;

public sealed record PagedResult<TItem>(IReadOnlyCollection<TItem> Items, int Page, int PageSize, int? TotalCount, string? NextCursor = null)
{
    public int? TotalPages => TotalCount is null ? null : PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount.Value / (double)PageSize);
}
