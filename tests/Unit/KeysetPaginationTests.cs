using EgitimUssu.Shared.Infrastructure.Persistence;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Tests.Unit;

public sealed class KeysetPaginationTests
{
    private static readonly DateTime Now = new(2026, 11, 15, 12, 0, 0, DateTimeKind.Utc);
    private sealed record Row(Guid Id, DateTime Time);

    [Theory]
    [InlineData(null, null, true)]
    [InlineData(null, 1, true)]
    [InlineData(null, 100, true)]
    [InlineData(null, 0, false)]
    [InlineData(null, 101, false)]
    [InlineData("broken", 50, false)]
    [InlineData("", 50, false)]
    public void Bounds_and_cursor_are_validated(string? cursor, int? limit, bool expected)
        => Assert.Equal(expected, KeysetPage.TryCreate(cursor, limit, out _));

    [Fact]
    public void Cursor_roundtrips_microseconds_and_guid()
    {
        var id = Guid.NewGuid();
        var time = Now.AddTicks(1234560);
        Assert.True(KeysetPage.TryCreate(KeysetPage.Encode(time, id), null, out var page));
        Assert.Equal(time, page.BeforeUtc);
        Assert.Equal(id, page.BeforeId);
        Assert.Equal(50, page.Limit);
    }

    [Fact]
    public void Equal_times_deleted_boundary_and_new_insert_do_not_skip_or_duplicate()
    {
        var rows = Enumerable.Range(1, 105).Select(i => new Row(new Guid(i, 0, 0, new byte[8]), Now)).ToList();
        var firstRequest = new KeysetPage(50);
        var first = firstRequest.Finish(rows.AsQueryable().Seek(firstRequest, x => x.Time, x => x.Id).ToArray(), x => x.Time, x => x.Id);
        Assert.Equal(50, first.Items.Count);
        Assert.NotNull(first.NextCursor);
        Assert.Null(first.TotalCount);
        Assert.Null(first.TotalPages);
        rows.Remove(first.Items.Last());
        rows.Add(new(Guid.NewGuid(), Now.AddSeconds(1)));
        Assert.True(KeysetPage.TryCreate(first.NextCursor, 50, out var request));
        var second = request.Finish(rows.AsQueryable().Seek(request, x => x.Time, x => x.Id).ToArray(), x => x.Time, x => x.Id);
        Assert.Equal(50, second.Items.Count);
        Assert.Empty(second.Items.Select(x => x.Id).Intersect(first.Items.Select(x => x.Id)));
        Assert.True(KeysetPage.TryCreate(second.NextCursor, 50, out request));
        var third = request.Finish(rows.AsQueryable().Seek(request, x => x.Time, x => x.Id).ToArray(), x => x.Time, x => x.Id);
        Assert.Equal(5, third.Items.Count);
        Assert.Null(third.NextCursor);
    }

    [Fact]
    public void Empty_and_exact_limit_pages_are_terminal()
    {
        var page = new KeysetPage(1);
        Assert.Null(page.Finish(Array.Empty<Row>(), x => x.Time, x => x.Id).NextCursor);
        Assert.Null(page.Finish(new[] { new Row(Guid.NewGuid(), Now) }, x => x.Time, x => x.Id).NextCursor);
    }
}
