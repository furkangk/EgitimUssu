using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using EgitimUssu.Shared.Kernel;

namespace EgitimUssu.Shared.Infrastructure.Persistence;

/// <summary>Filtrelenmiş SQL kaynağını en fazla limit+1 satırla sınırlar.</summary>
public static class KeysetQuery
{
    public static IQueryable<T> Seek<T>(this IQueryable<T> query, KeysetPage page,
        Expression<Func<T, DateTime>> time, Expression<Func<T, Guid>> id, bool rowComparison = false)
    {
        if (page.BeforeUtc is { } before && page.BeforeId is { } beforeId)
        {
            var key = new ReplaceParameter(id.Parameters[0], time.Parameters[0]).Visit(id.Body)!;
            var older = Expression.LessThan(time.Body, Expression.Constant(before));
            var equal = Expression.Equal(time.Body, Expression.Constant(before));
            var smallerId = Expression.LessThan(Expression.Call(key, nameof(Guid.CompareTo), null, Expression.Constant(beforeId)), Expression.Constant(0));
            Expression predicate = Expression.OrElse(older, Expression.AndAlso(equal, smallerId));
            if (rowComparison)
            {
                // PostgreSQL doğrudan bileşik indeksin cursor aralığına atlar; derin sayfada OR filtresi taranmaz.
                Expression<Func<DateTime, Guid, bool>> row = (stamp, guid) =>
                    EF.Functions.LessThan(ValueTuple.Create(stamp, guid), ValueTuple.Create(before, beforeId));
                predicate = new ReplaceParameter(row.Parameters[0], time.Body).Visit(row.Body)!;
                predicate = new ReplaceParameter(row.Parameters[1], key).Visit(predicate)!;
            }
            query = query.Where(Expression.Lambda<Func<T, bool>>(predicate, time.Parameters));
        }
        return query.OrderByDescending(time).ThenByDescending(id).Take(page.Limit + 1);
    }

    private sealed class ReplaceParameter(ParameterExpression source, Expression target) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == source ? target : base.VisitParameter(node);
    }
}
