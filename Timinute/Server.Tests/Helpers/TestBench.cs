using System;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Timinute.Server;
using Timinute.Server.Data;
using Timinute.Server.Repository;

namespace Timinute.Server.Tests.Helpers
{
    /// <summary>
    /// Lightweight construction helpers for the app-service unit tests. Mirrors how
    /// <c>Server.Tests/Repositories</c> and the controller tests build their
    /// <see cref="IRepositoryFactory"/> + AutoMapper <see cref="IMapper"/> stacks
    /// (same <see cref="MappingProfile"/>, same <see cref="MapperConfiguration"/>
    /// shape) so services and controllers exercise the identical mapping config.
    /// </summary>
    public static class TestBench
    {
        /// <summary>Builds an AutoMapper <see cref="IMapper"/> over <see cref="MappingProfile"/>.</summary>
        public static IMapper NewMapper()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, NullLoggerFactory.Instance);
            return new Mapper(configuration);
        }

        /// <summary>
        /// Builds an <see cref="IRepositoryFactory"/> + <see cref="IMapper"/> over a
        /// fresh, isolated EF InMemory <see cref="ApplicationDbContext"/>.
        /// </summary>
        public static (IRepositoryFactory factory, IMapper mapper) NewProjectStack()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: "AppServiceParity_" + Guid.NewGuid())
                .UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
                .Options;

            var context = new ApplicationDbContext(options);
            return NewProjectStack(context);
        }

        /// <summary>
        /// Builds an <see cref="IRepositoryFactory"/> + <see cref="IMapper"/> over a
        /// caller-supplied context (e.g. the SQLite helper used by parity tests that
        /// must exercise the filtered unique index).
        /// </summary>
        public static (IRepositoryFactory factory, IMapper mapper) NewProjectStack(ApplicationDbContext context)
        {
            return (new RepositoryFactory(context), NewMapper());
        }
    }
}
