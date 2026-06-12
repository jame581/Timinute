using System.Linq.Expressions;
using Timinute.Server.Models.Paging;

namespace Timinute.Server.Repository
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task Delete(TEntity entityToDelete);
        Task Delete(object id);
        Task<TEntity?> Find(object id);
        Task<PagedList<TEntity>> GetPaged(PagingParameters parameters, Expression<Func<TEntity, bool>>? filter = null,
            string orderBy = null, string includeProperties = "");
        Task<IEnumerable<TEntity>> Get(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, string includeProperties = "");
        Task<IEnumerable<TType>> Get<TType>(Expression<Func<TEntity, TType>> select, Expression<Func<TEntity, bool>>? where = null) where TType : class;
        Task<TEntity?> GetByIdInclude(Expression<Func<TEntity, bool>>? filter = null, string includeProperties = "");
        Task<TEntity?> GetById(object id);
        Task<IEnumerable<TEntity>> GetWithRawSql(string query, params object[] parameters);
        Task Insert(TEntity entity);
        Task Update(TEntity entityToUpdate);

        Task SoftDelete(object id);
        Task Restore(object id);
        Task<IEnumerable<TEntity>> GetDeleted(Expression<Func<TEntity, bool>>? filter = null);
        Task<int> PurgeExpired(DateTimeOffset olderThan);

        /// <summary>
        /// Counts entities matching <paramref name="filter"/>, including soft-deleted rows.
        /// Use when a counter must be monotonic across deletions — e.g. round-robin
        /// palette assignment that should not collide with a still-active sibling color.
        /// </summary>
        Task<int> CountAll(Expression<Func<TEntity, bool>>? filter = null);

        /// <summary>
        /// Asynchronously counts entities matching the optional filter.
        /// Honors EF global query filters (e.g. soft delete) — use <see cref="CountAll"/>
        /// when you need the unfiltered (including soft-deleted) count.
        /// </summary>
        Task<int> CountAsync(Expression<Func<TEntity, bool>>? filter = null);

        /// <summary>
        /// Asynchronously sums the projected <see cref="long"/> values for
        /// entities matching the optional filter. The projection is materialized
        /// and summed in memory rather than via a server-side SQL <c>SUM</c>:
        /// the intended selector (<c>TimeSpan.Ticks</c> over a SQL Server
        /// <c>time</c> column) has no SQL aggregate translation, and EF Core
        /// cannot client-evaluate an aggregate. The query is still scoped by
        /// <paramref name="filter"/> and honors EF global query filters
        /// (e.g. soft delete); only the projected column is transferred.
        /// Returns 0 for an empty set.
        /// </summary>
        /// <remarks>
        /// The sum is computed client-side, so callers should pass a
        /// <paramref name="filter"/> that bounds the row set — with no filter,
        /// every matching row's projected value is loaded into memory.
        /// </remarks>
        Task<long> SumAsync(
            Expression<Func<TEntity, long>> selector,
            Expression<Func<TEntity, bool>>? filter = null);
    }
}
