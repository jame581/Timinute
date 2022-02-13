using System.Linq.Expressions;

namespace Timinute.Server.Repository
{
    public interface IRepository<TEntity> where TEntity : class
    {
        Task Delete(TEntity entityToDelete);
        Task Delete(object id);
        Task<TEntity?> Find(object id);
        Task<IEnumerable<TEntity>> Get(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, string includeProperties = "");
        Task<IEnumerable<TType>> Get<TType>(Expression<Func<TEntity, TType>> select, Expression<Func<TEntity, bool>>? where = null) where TType : class;
        Task<TEntity?> GetByIdInclude(Expression<Func<TEntity, bool>>? filter = null, string includeProperties = "");
        Task<TEntity?> GetById(object id);
        Task<IEnumerable<TEntity>> GetWithRawSql(string query, params object[] parameters);
        Task Insert(TEntity entity);
        Task Update(TEntity entityToUpdate);
    }
}
