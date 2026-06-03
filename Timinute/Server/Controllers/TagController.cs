using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Shared.Dtos.Tag;

namespace Timinute.Server.Controllers
{
    [Authorize]
    [ApiController]
    [Route("[controller]")]
    public class TagController : ControllerBase
    {
        private readonly IRepository<Tag> tagRepository;
        private readonly IMapper mapper;
        private readonly ILogger<TagController> logger;

        public TagController(IRepositoryFactory repositoryFactory, IMapper mapper, ILogger<TagController> logger)
        {
            this.mapper = mapper;
            this.logger = logger;
            tagRepository = repositoryFactory.GetRepository<Tag>();
        }

        // GET: api/Tag
        [HttpGet]
        public async Task<ActionResult<IEnumerable<TagDto>>> GetTags()
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var tags = await tagRepository.Get(
                t => t.UserId == userId,
                orderBy: query => query.OrderBy(t => t.Name),
                includeProperties: nameof(Tag.TrackedTasks));

            var dtos = tags.Select(tag =>
            {
                var dto = mapper.Map<TagDto>(tag);
                dto.TaskCount = tag.TrackedTasks?.Count ?? 0;
                return dto;
            });

            return Ok(dtos);
        }

        // GET: api/Tag/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<TagDto>> GetTag(string id)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var tag = await tagRepository.GetByIdInclude(t => t.TagId == id && t.UserId == userId, includeProperties: nameof(Tag.TrackedTasks));
            if (tag == null)
            {
                return NotFound("Tag not found!");
            }

            var dto = mapper.Map<TagDto>(tag);
            dto.TaskCount = tag.TrackedTasks?.Count ?? 0;
            return Ok(dto);
        }

        // CREATE: api/Tag
        [HttpPost]
        public async Task<ActionResult<TagDto>> CreateTag([FromBody] CreateTagDto tag)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (await tagRepository.CountAsync(t => t.UserId == userId && t.Name == tag.Name) > 0)
            {
                return Conflict(new { message = "A tag with this name already exists." });
            }

            var newTag = new Tag
            {
                Name = tag.Name,
                Color = tag.Color,
                UserId = userId
            };

            try
            {
                await tagRepository.Insert(newTag);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                logger.LogWarning(ex, "Duplicate tag name detected for user {UserId}.", userId);
                return Conflict(new { message = "A tag with this name already exists." });
            }

            var dto = mapper.Map<TagDto>(newTag);
            dto.TaskCount = 0;
            return Ok(dto);
        }

        // UPDATE: api/Tag
        [HttpPut("{id}")]
        public async Task<ActionResult<TagDto>> UpdateTag(string id, [FromBody] UpdateTagDto tag)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            if (id != tag.TagId)
            {
                return BadRequest("Route id does not match tag id.");
            }

            var existingTag = await tagRepository.GetByIdInclude(t => t.TagId == id && t.UserId == userId);

            if (existingTag == null)
            {
                return NotFound("Tag not found!");
            }

            if (await tagRepository.CountAsync(t => t.UserId == userId && t.Name == tag.Name && t.TagId != tag.TagId) > 0)
            {
                return Conflict(new { message = "A tag with this name already exists." });
            }

            existingTag.Name = tag.Name;
            existingTag.Color = tag.Color;

            try
            {
                await tagRepository.Update(existingTag);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                logger.LogWarning(ex, "Duplicate tag name detected for user {UserId}.", userId);
                return Conflict(new { message = "A tag with this name already exists." });
            }

            var updatedTag = await tagRepository.GetByIdInclude(t => t.TagId == id && t.UserId == userId, includeProperties: nameof(Tag.TrackedTasks));
            var dto = mapper.Map<TagDto>(updatedTag!);
            dto.TaskCount = updatedTag?.TrackedTasks?.Count ?? 0;
            return Ok(dto);
        }

        // DELETE: api/Tag/{id}
        [HttpDelete("{id}")]
        public async Task<ActionResult> DeleteTag(string id, [FromQuery] bool force = false)
        {
            var userId = User.FindFirstValue(Constants.Claims.UserId);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            Tag? tag = force
                ? await tagRepository.GetByIdInclude(t => t.TagId == id && t.UserId == userId)
                : await tagRepository.GetByIdInclude(t => t.TagId == id && t.UserId == userId, includeProperties: nameof(Tag.TrackedTasks));

            if (tag == null)
            {
                return NotFound("Tag not found!");
            }

            if (!force)
            {
                var taskCount = tag.TrackedTasks?.Count ?? 0;
                return Ok(new { taskCount });
            }

            await tagRepository.Delete(tag);
            return NoContent();
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.Message;
            return message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase)
                || message.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
        }
    }
}
