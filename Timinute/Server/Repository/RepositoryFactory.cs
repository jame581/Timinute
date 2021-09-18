using Timinute.Server.Data;

namespace Timinute.Server.Repository
{
    public class RepositoryFactory : IRepositoryFactory
    {
        private readonly ApplicationDbContext _context;

        public RepositoryFactory(ApplicationDbContext context)
        {
            _context = context;
        }

        public IRepository<TEntity> GetRepository<TEntity>() where TEntity : class, new()
            => new BaseRepository<TEntity>(_context);
    }
}
