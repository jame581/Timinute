using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Timinute.Server.Controllers;
using Timinute.Server.Data;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Tests.Helpers;
using Timinute.Shared.Dtos.Tag;
using Xunit;

namespace Timinute.Server.Tests.Controllers
{
    public class TagControllerTest : ControllerTestBase<TagController>
    {
        private const string SeedUserId1 = "a1b2c3d4-e5f6-4a5b-8c7d-9e0f1a2b3c4d";
        private const string _databaseName = "TagController_Test_DB";

        private readonly IMapper _mapper;
        private readonly Mock<ILogger<TagController>> _loggerMock;

        public TagControllerTest()
        {
            var configExpression = new MapperConfigurationExpression();
            configExpression.AddProfile<MappingProfile>();
            var configuration = new MapperConfiguration(configExpression, Microsoft.Extensions.Logging.Abstractions.NullLoggerFactory.Instance);
            _mapper = new Mapper(configuration);

            _loggerMock = new Mock<ILogger<TagController>>();
        }

        [Fact]
        public async Task Get_All_Tags_Returns_Current_User_Sorted_By_Name()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetAll");

            var alpha = new Tag { TagId = "TagId1", Name = "Alpha", Color = "#111111", UserId = "ApplicationUser1" };
            var beta = new Tag { TagId = "TagId2", Name = "Beta", Color = "#222222", UserId = "ApplicationUser1" };
            var other = new Tag { TagId = "TagId3", Name = "Other", Color = "#333333", UserId = "ApplicationUser2" };

            var start = DateTimeOffset.UtcNow;
            var task = new TrackedTask
            {
                TaskId = "TrackedTaskTag1",
                Name = "Tagged task",
                UserId = "ApplicationUser1",
                StartDate = start,
                Duration = TimeSpan.FromHours(1),
                EndDate = start.AddHours(1),
                Tags = new List<Tag> { beta }
            };

            applicationDbContext.Tags.AddRange(alpha, beta, other);
            applicationDbContext.TrackedTasks.Add(task);
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.GetTags();

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);

            Assert.IsAssignableFrom<IEnumerable<TagDto>>(okResult!.Value);
            var tags = (okResult.Value as IEnumerable<TagDto>)!.ToList();

            Assert.Equal(2, tags.Count);
            Assert.Equal("Alpha", tags[0].Name);
            Assert.Equal("Beta", tags[1].Name);
            Assert.Equal(0, tags[0].TaskCount);
            Assert.Equal(1, tags[1].TaskCount);
        }

        [Fact]
        public async Task Get_Tag_By_Id_Wrong_User_Returns_NotFound()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetWrongUser");
            applicationDbContext.Tags.Add(new Tag
            {
                TagId = "TagId1",
                Name = "Alpha",
                Color = "#111111",
                UserId = "ApplicationUser1"
            });
            await applicationDbContext.SaveChangesAsync();

            TagController controller = await CreateController(applicationDbContext, "ApplicationUser2");

            var actionResult = await controller.GetTag("TagId1");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task Get_Tag_By_Id_Returns_Dto_And_Task_Count()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "GetSuccess");

            var tag = new Tag
            {
                TagId = "TagId1",
                Name = "Alpha",
                Color = "#111111",
                UserId = "ApplicationUser1"
            };
            var task = new TrackedTask
            {
                TaskId = "TrackedTaskTagGet",
                Name = "Tagged task",
                UserId = "ApplicationUser1",
                StartDate = DateTimeOffset.UtcNow,
                Duration = TimeSpan.FromHours(1),
                Tags = new List<Tag> { tag }
            };

            applicationDbContext.Tags.Add(tag);
            applicationDbContext.TrackedTasks.Add(task);
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.GetTag("TagId1");

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);

            var dto = okResult!.Value as TagDto;
            Assert.NotNull(dto);
            Assert.Equal("Alpha", dto!.Name);
            Assert.Equal(1, dto.TaskCount);
        }

        [Fact]
        public async Task Create_Tag_Returns_Dto_And_Persists()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "Create");
            TagController controller = await CreateController(applicationDbContext);

            var dto = new CreateTagDto { Name = "New tag", Color = "#ABCDEF" };
            var actionResult = await controller.CreateTag(dto);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);

            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);
            Assert.IsAssignableFrom<TagDto>(okResult!.Value);

            var created = okResult.Value as TagDto;
            Assert.NotNull(created);
            Assert.Equal(dto.Name, created!.Name);
            Assert.Equal(dto.Color, created.Color);

            var saved = await applicationDbContext.Tags.FirstOrDefaultAsync(t => t.TagId == created.TagId);
            Assert.NotNull(saved);
            Assert.Equal("ApplicationUser1", saved!.UserId);
        }

        [Fact]
        public async Task Create_Tag_Duplicate_Name_Returns_Conflict()
        {
            await using var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var applicationDbContext = await TestHelper.GetSqliteApplicationDbContext(connection);
            TagController controller = await CreateController(applicationDbContext, SeedUserId1);

            var dto = new CreateTagDto { Name = "Duplicate", Color = "#123456" };

            var first = await controller.CreateTag(dto);
            Assert.IsAssignableFrom<OkObjectResult>(first.Result);

            var second = await controller.CreateTag(dto);
            Assert.IsAssignableFrom<ConflictObjectResult>(second.Result);
        }

        [Fact]
        public async Task Update_Tag_Returns_Updated_Dto()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "Update");
            applicationDbContext.Tags.Add(new Tag
            {
                TagId = "TagId1",
                Name = "Old",
                Color = "#111111",
                UserId = "ApplicationUser1"
            });
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext);

            var update = new UpdateTagDto
            {
                TagId = "TagId1",
                Name = "Updated",
                Color = "#222222"
            };

            var actionResult = await controller.UpdateTag("TagId1", update);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<OkObjectResult>(actionResult.Result);
            var okResult = actionResult.Result as OkObjectResult;
            Assert.NotNull(okResult);

            var updated = okResult!.Value as TagDto;
            Assert.NotNull(updated);
            Assert.Equal(update.Name, updated!.Name);
            Assert.Equal(update.Color, updated.Color);

            var saved = await applicationDbContext.Tags.FirstOrDefaultAsync(t => t.TagId == update.TagId);
            Assert.NotNull(saved);
            Assert.Equal(update.Name, saved!.Name);
            Assert.Equal(update.Color, saved.Color);
        }

        [Fact]
        public async Task Update_Tag_Duplicate_Name_Returns_Conflict()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdateDuplicate");
            applicationDbContext.Tags.AddRange(
                new Tag
                {
                    TagId = "TagId1",
                    Name = "Old",
                    Color = "#111111",
                    UserId = "ApplicationUser1"
                },
                new Tag
                {
                    TagId = "TagId2",
                    Name = "Existing",
                    Color = "#222222",
                    UserId = "ApplicationUser1"
                });
            await applicationDbContext.SaveChangesAsync();
                applicationDbContext.ChangeTracker.Clear();

                TagController controller = await CreateController(applicationDbContext);

            var update = new UpdateTagDto
            {
                TagId = "TagId1",
                Name = "Existing",
                Color = "#333333"
            };

            var actionResult = await controller.UpdateTag("TagId1", update);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<ConflictObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task Update_Tag_Wrong_User_Returns_NotFound()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "UpdateWrongUser");
            applicationDbContext.Tags.Add(new Tag
            {
                TagId = "TagId1",
                Name = "Old",
                Color = "#111111",
                UserId = "ApplicationUser1"
            });
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext, "ApplicationUser2");

            var update = new UpdateTagDto
            {
                TagId = "TagId1",
                Name = "Updated",
                Color = "#222222"
            };

            var actionResult = await controller.UpdateTag("TagId1", update);

            Assert.NotNull(actionResult);
            Assert.IsAssignableFrom<NotFoundObjectResult>(actionResult.Result);
        }

        [Fact]
        public async Task Delete_Without_Force_Returns_Task_Count()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "DeleteCount");

            var tag = new Tag { TagId = "TagId1", Name = "Work", Color = "#111111", UserId = "ApplicationUser1" };
            var start = DateTimeOffset.UtcNow;
            var task1 = new TrackedTask
            {
                TaskId = "TrackedTaskTag1",
                Name = "Task 1",
                UserId = "ApplicationUser1",
                StartDate = start,
                Duration = TimeSpan.FromHours(1),
                EndDate = start.AddHours(1),
                Tags = new List<Tag> { tag }
            };
            var task2 = new TrackedTask
            {
                TaskId = "TrackedTaskTag2",
                Name = "Task 2",
                UserId = "ApplicationUser1",
                StartDate = start.AddHours(2),
                Duration = TimeSpan.FromHours(1),
                EndDate = start.AddHours(3),
                Tags = new List<Tag> { tag }
            };

            applicationDbContext.Tags.Add(tag);
            applicationDbContext.TrackedTasks.AddRange(task1, task2);
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.DeleteTag(tag.TagId);

            Assert.IsType<OkObjectResult>(actionResult);
            var okResult = actionResult as OkObjectResult;
            Assert.NotNull(okResult);
            var payload = okResult!.Value;
            Assert.NotNull(payload);

            int taskCount;
            if (payload is IDictionary<string, object> dict && dict.TryGetValue("taskCount", out var value))
            {
                taskCount = Convert.ToInt32(value);
            }
            else
            {
                var property = payload.GetType().GetProperty("taskCount");
                Assert.NotNull(property);
                taskCount = Convert.ToInt32(property!.GetValue(payload));
            }

            Assert.Equal(2, taskCount);

            var tagStillThere = await applicationDbContext.Tags.FirstOrDefaultAsync(t => t.TagId == tag.TagId);
            Assert.NotNull(tagStillThere);
        }

        [Fact]
        public async Task Delete_Without_Force_Counts_Soft_Deleted_Task_Associations()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "DeleteCountSoftDeleted");

            var tag = new Tag { TagId = "TagIdSoftDeleted", Name = "Work", Color = "#111111", UserId = "ApplicationUser1" };
            var start = DateTimeOffset.UtcNow;
            var deletedTask = new TrackedTask
            {
                TaskId = "TrackedTaskSoftDeletedTag1",
                Name = "Deleted Task",
                UserId = "ApplicationUser1",
                StartDate = start,
                Duration = TimeSpan.FromHours(1),
                EndDate = start.AddHours(1),
                DeletedAt = start,
                Tags = new List<Tag> { tag }
            };

            applicationDbContext.Tags.Add(tag);
            applicationDbContext.TrackedTasks.Add(deletedTask);
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.DeleteTag(tag.TagId);

            Assert.IsType<OkObjectResult>(actionResult);
            var okResult = actionResult as OkObjectResult;
            Assert.NotNull(okResult);
            var payload = okResult!.Value;
            Assert.NotNull(payload);

            int taskCount;
            if (payload is IDictionary<string, object> dict && dict.TryGetValue("taskCount", out var value))
            {
                taskCount = Convert.ToInt32(value);
            }
            else
            {
                var property = payload.GetType().GetProperty("taskCount");
                Assert.NotNull(property);
                taskCount = Convert.ToInt32(property!.GetValue(payload));
            }

            Assert.Equal(1, taskCount);
        }

        [Fact]
        public async Task Delete_With_Force_Removes_Tag_And_Leaves_Tasks()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "DeleteForce");

            var tag = new Tag { TagId = "TagId1", Name = "Work", Color = "#111111", UserId = "ApplicationUser1" };
            var start = DateTimeOffset.UtcNow;
            var task1 = new TrackedTask
            {
                TaskId = "TrackedTaskTag1",
                Name = "Task 1",
                UserId = "ApplicationUser1",
                StartDate = start,
                Duration = TimeSpan.FromHours(1),
                EndDate = start.AddHours(1),
                Tags = new List<Tag> { tag }
            };
            var task2 = new TrackedTask
            {
                TaskId = "TrackedTaskTag2",
                Name = "Task 2",
                UserId = "ApplicationUser1",
                StartDate = start.AddHours(2),
                Duration = TimeSpan.FromHours(1),
                EndDate = start.AddHours(3),
                Tags = new List<Tag> { tag }
            };

            applicationDbContext.Tags.Add(tag);
            applicationDbContext.TrackedTasks.AddRange(task1, task2);
            await applicationDbContext.SaveChangesAsync();
            applicationDbContext.ChangeTracker.Clear();

            TagController controller = await CreateController(applicationDbContext);

            var actionResult = await controller.DeleteTag(tag.TagId, true);

            Assert.IsType<NoContentResult>(actionResult);

            var deletedTag = await applicationDbContext.Tags.FirstOrDefaultAsync(t => t.TagId == tag.TagId);
            Assert.Null(deletedTag);

            var remainingTasks = await applicationDbContext.TrackedTasks
                .Where(t => t.TaskId == "TrackedTaskTag1" || t.TaskId == "TrackedTaskTag2")
                .ToListAsync();
            Assert.Equal(2, remainingTasks.Count);
        }

        [Fact]
        public async Task Delete_Tag_Wrong_User_Returns_NotFound()
        {
            ApplicationDbContext applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName + "DeleteWrongUser");
            applicationDbContext.Tags.Add(new Tag
            {
                TagId = "TagId1",
                Name = "Old",
                Color = "#111111",
                UserId = "ApplicationUser1"
            });
            await applicationDbContext.SaveChangesAsync();

            TagController controller = await CreateController(applicationDbContext, "ApplicationUser2");

            var actionResult = await controller.DeleteTag("TagId1", true);

            Assert.IsType<NotFoundObjectResult>(actionResult);
        }

        protected override async Task<TagController> CreateController(ApplicationDbContext? applicationDbContext = null, string userId = "ApplicationUser1")
        {
            if (applicationDbContext == null)
            {
                applicationDbContext = await TestHelper.GetDefaultApplicationDbContext(_databaseName);
            }

            var repositoryFactory = new RepositoryFactory(applicationDbContext);

            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim("sub", userId),
                new Claim(ClaimTypes.Name, "test1@email.com")
            }));

            TagController controller = new(repositoryFactory, _mapper, _loggerMock.Object, applicationDbContext)
            {
                ControllerContext = new ControllerContext
                {
                    HttpContext = new DefaultHttpContext { User = user }
                }
            };

            return controller;
        }
    }
}
