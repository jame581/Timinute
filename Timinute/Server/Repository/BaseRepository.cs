using Microsoft.EntityFrameworkCore;
using System.Linq.Dynamic.Core;
using System.Linq.Expressions;
using Timinute.Server.Data;
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

        public async Task<PagedList<TEntity>> GetPaged(PagingParameters parameters, string includeProperties = "")
        {
            IQueryable<TEntity> query = dbSet;

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

            if (!string.IsNullOrEmpty(parameters.OrderBy))
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
            return await dbSet.FindAsync(id);
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
    }
}
