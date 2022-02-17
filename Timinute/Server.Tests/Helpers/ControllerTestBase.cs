using System.Threading.Tasks;
using Timinute.Server.Data;

namespace Timinute.Server.Tests.Helpers
{
    public abstract class ControllerTestBase<T>
    {
        protected abstract Task<T> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1");

    }
}
