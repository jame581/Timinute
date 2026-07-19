using System.ComponentModel;
using ModelContextProtocol;
using ModelContextProtocol.Server;
using Timinute.Server.Services.App;
using Timinute.Shared.Dtos.Project;

namespace Timinute.Server.Mcp
{
    /// <summary>
    /// MCP tools over the caller's projects. Constructed per tool call inside the HTTP
    /// request's DI scope; <see cref="McpUserContext.UserId"/> is resolved per call, never
    /// cached in a field. Domain exceptions are rethrown as <see cref="McpException"/> so the
    /// client sees a clean message rather than a stack trace.
    /// </summary>
    [McpServerToolType]
    public class ProjectTools
    {
        private readonly IProjectAppService projects;
        private readonly McpUserContext user;

        public ProjectTools(IProjectAppService projects, McpUserContext user)
        {
            this.projects = projects;
            this.user = user;
        }

        [McpServerTool(Name = "list_projects"), Description("List the current user's projects (id, name, color).")]
        public async Task<object> ListProjects()
            => await projects.ListAsync(user.UserId);

        [McpServerTool(Name = "create_project"), Description("Create a project for the current user. Requires a read_write token.")]
        public async Task<object> CreateProject(
            [Description("Project name (2-100 characters).")] string name,
            [Description("Optional hex color in the form #RRGGBB, e.g. #6366F1. Omit to auto-assign from the palette.")] string? color = null)
        {
            user.RequireWrite();

            try
            {
                return await projects.CreateAsync(user.UserId, new CreateProjectDto { Name = name, Color = color });
            }
            catch (ProjectNameConflictException ex)
            {
                throw new McpException(ex.Message);
            }
        }
    }
}
