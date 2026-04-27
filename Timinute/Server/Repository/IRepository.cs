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
    }
}
