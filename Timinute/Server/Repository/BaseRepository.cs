using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Models.Paging;

namespace Timinute.Server.Repository
{
    public class BaseRepository<TEntity> : IRepository<TEntity> where TEntity : class
    {
        internal ApplicationDbContext context;
        internal DbSet<TEntity> dbSet;

        public BaseRepository(ApplicationDbContext context)
        {
            this.context = context;
            dbSet = context.Set<TEntity>();
        }

        public async Task Delete(TEntity entityToDelete)
        {
            if (context.Entry(entityToDelete).State == EntityState.Detached)
            {
                dbSet.Attach(entityToDelete);
            }

            dbSet.Remove(entityToDelete);
            await context.SaveChangesAsync();
        }

        public async Task Delete(object id)
        {
            TEntity? entityToDelete = await dbSet.FindAsync(id);

            if (entityToDelete != null)
                await Delete(entityToDelete);
        }

        public async Task<TEntity?> Find(object id)
        {
            return await dbSet.FindAsync(id);
        }

        public async Task<PagedList<TEntity>> GetPaged(PagingParameters parameters, Expression<Func<TEntity, bool>>? filter = null,
            string orderBy = null, string includeProperties = "")
        {
            IQueryable<TEntity> query = dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (!string.IsNullOrEmpty(parameters.Filter))
            {
                query = query.Where(parameters.Filter);
            }

            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty.Trim());
                }
            }

            if (!string.IsNullOrEmpty(parameters.OrderBy) && !string.IsNullOrEmpty(orderBy))
            {
                query = query.OrderBy($"{orderBy}, {parameters.OrderBy}");
            }
            else if (!string.IsNullOrEmpty(orderBy))
            {
                query = query.OrderBy(orderBy);
            }
            else if (!string.IsNullOrEmpty(parameters.OrderBy))
            {
                query = query.OrderBy(parameters.OrderBy);
            }

            int count = await query.CountAsync();
            var items = await query.Skip((parameters.PageNumber - 1) * parameters.PageSize).Take(parameters.PageSize).ToListAsync();

            return new PagedList<TEntity>(items, count, parameters.PageNumber, parameters.PageSize);
        }

        public async Task<IEnumerable<TEntity>> Get(Expression<Func<TEntity, bool>>? filter = null, Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null, string includeProperties = "")
        {
            IQueryable<TEntity> query = dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty.Trim());
                }
            }

            if (orderBy != null)
            {
                return await orderBy(query).ToListAsync();
            }
            else
            {
                return await query.ToListAsync();
            }
        }

        public async Task<IEnumerable<TType>> Get<TType>(Expression<Func<TEntity, TType>> select, Expression<Func<TEntity, bool>>? where = null) where TType : class
        {
            if (where == null)
            {
                return await dbSet.Select(select).ToListAsync();
            }

            return await dbSet.Where(where).Select(select).ToListAsync();
        }

        public async Task<TEntity?> GetByIdInclude(Expression<Func<TEntity, bool>>? filter = null, string includeProperties = "")
        {
            IQueryable<TEntity> query = dbSet;

            if (filter != null)
            {
                query = query.Where(filter);
            }

            if (!string.IsNullOrEmpty(includeProperties))
            {
                foreach (var includeProperty in includeProperties.Split
                (new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    query = query.Include(includeProperty.Trim());
                }
            }
            return await query.SingleOrDefaultAsync();
        }

        public async Task<TEntity?> GetById(object id)
        {
            var entity = await dbSet.FindAsync(id);
            if (entity is ISoftDeletable sd && sd.DeletedAt != null)
                return null;
            return entity;
        }

        public async Task<IEnumerable<TEntity>> GetWithRawSql(string query, params object[] parameters)
        {
            return await dbSet.FromSqlRaw(query, parameters).ToListAsync();
        }

        public async Task Insert(TEntity entity)
        {
            await dbSet.AddAsync(entity);
            await context.SaveChangesAsync();
        }

        public async Task Update(TEntity entityToUpdate)
        {
            dbSet.Update(entityToUpdate);
            await context.SaveChangesAsync();
        }

        public async Task SoftDelete(object id)
        {
            var entity = await dbSet.FindAsync(id);
            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.DeletedAt = DateTimeOffset.UtcNow;
                await context.SaveChangesAsync();
            }
            else if (entity != null)
            {
                throw new InvalidOperationException(
                    $"Entity of type {typeof(TEntity).Name} does not implement ISoftDeletable.");
            }
        }

        public async Task Restore(object id)
        {
            // FindAsync bypasses query filters and checks the identity map first,
            // returning a tracked entity so that SaveChangesAsync will persist changes.
            var entity = await context.Set<TEntity>().FindAsync(id);

            if (entity is ISoftDeletable softDeletable)
            {
                softDeletable.DeletedAt = null;
                await context.SaveChangesAsync();
            }
            else if (entity != null)
            {
                throw new InvalidOperationException(
                    $"Entity of type {typeof(TEntity).Name} does not implement ISoftDeletable.");
            }
        }

        public async Task<IEnumerable<TEntity>> GetDeleted(Expression<Func<TEntity, bool>>? filter = null)
        {
            // Build expression: e => e.DeletedAt != null (only for ISoftDeletable entities)
            var param = Expression.Parameter(typeof(TEntity), "e");
            var prop = Expression.Property(param, nameof(ISoftDeletable.DeletedAt));
            var isNotNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(DateTimeOffset?)));
            var isDeletedExpr = Expression.Lambda<Func<TEntity, bool>>(isNotNull, param);

            IQueryable<TEntity> query = dbSet.IgnoreQueryFilters().Where(isDeletedExpr);

            if (filter != null)
            {
                query = query.Where(filter);
            }

            return await query.ToListAsync();
        }

        public async Task<int> PurgeExpired(DateTimeOffset olderThan)
        {
            // Build expression: e => e.DeletedAt != null && e.DeletedAt.Value < olderThan
            var param = Expression.Parameter(typeof(TEntity), "e");
            var prop = Expression.Property(param, nameof(ISoftDeletable.DeletedAt));       // DateTimeOffset?
            var propValue = Expression.Property(prop, nameof(Nullable<DateTimeOffset>.Value)); // DateTimeOffset
            var isNotNull = Expression.NotEqual(prop, Expression.Constant(null, typeof(DateTimeOffset?)));
            var olderThanValue = Expression.Constant(olderThan, typeof(DateTimeOffset));
            var isOlderThan = Expression.LessThan(propValue, olderThanValue);
            var combined = Expression.AndAlso(isNotNull, isOlderThan);
            var predicate = Expression.Lambda<Func<TEntity, bool>>(combined, param);

            // Use AsTracking() to ensure identity map is consulted, avoiding duplicate-tracking conflicts
            var toDelete = await dbSet.IgnoreQueryFilters().AsTracking().Where(predicate).ToListAsync();

            if (toDelete.Count == 0)
                return 0;

            dbSet.RemoveRange(toDelete);
            await context.SaveChangesAsync();
            return toDelete.Count;
        }
    }
}
