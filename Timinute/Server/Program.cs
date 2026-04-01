using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Timinute.Server;
using Timinute.Server.Areas.Identity;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Identity configuration
IdentitySetup();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "ApplicationDefinedPolicy";
        options.DefaultChallengeScheme = "ApplicationDefinedPolicy";
    })
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"] ?? "https://localhost:7047";
        options.Audience = "Timinute.ServerAPI";
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = Constants.Claims.Role;
    })
    .AddPolicyScheme("ApplicationDefinedPolicy", "ApplicationDefinedPolicy", options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            string? authorization = context.Request.Headers.Authorization;
            if (!string.IsNullOrWhiteSpace(authorization) && authorization.TrimStart().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;

            return IdentityConstants.ApplicationScheme;
        };
    });

builder.Logging.AddConsole();

builder.Services.AddRazorPages();

builder.Services.AddResponseCaching();

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
    options.CacheProfiles.Add("Default120", new CacheProfile() { Duration = 120, Location = ResponseCacheLocation.Client });


}).AddJsonOptions(options =>
{
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.PropertyNameCaseInsensitive = true;

})
    .AddXmlDataContractSerializerFormatters()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var problemDetails = new ValidationProblemDetails(context.ModelState)
            {
                Type = "https://timinute.azurewebsites.net/modelvalidationproblem",
                Title = "One or more model validation errors occurred.",
                Status = StatusCodes.Status422UnprocessableEntity,
                Detail = "See the errors property for details.",
                Instance = context.HttpContext.Request.Path
            };

            problemDetails.Extensions.Add("traceId", context.HttpContext.TraceIdentifier);

            return new UnprocessableEntityObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" }
            };
        };
    });

// DI Configuration
DependecyInjection();

// Auto Mapper Configurations
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Swagger Configuration
SwaggerSetup();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
    app.UseWebAssemblyDebugging();
    app.UseDeveloperExceptionPage();

    // Swagger middleware
    app.UseSwagger();

    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Timinute API V1");
    });
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseResponseCaching();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseRouting();

app.UseIdentityServer();
app.UseAuthentication();
app.UseAuthorization();


app.MapRazorPages();
app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();

void IdentitySetup()
{
    builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.SignIn.RequireConfirmedAccount = true;

        options.Password.RequiredLength = 8;
        options.Password.RequiredUniqueChars = 4;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = true;

        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;

    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddRoles<ApplicationRole>()
    .AddDefaultTokenProviders()
    .AddDefaultUI()
    .AddSignInManager<AppSingInManager>()
    .AddClaimsPrincipalFactory<ApplicationUserClaimsPrincipalFactory>();

    builder.Services.Configure<IdentityOptions>(options =>
    {
        options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
        options.ClaimsIdentity.UserNameClaimType = ClaimTypes.Name;
        options.ClaimsIdentity.RoleClaimType = Constants.Claims.Role;
    });

    var configuredUrl = builder.Configuration["IdentityServer:Authority"] ?? "https://localhost:7047";
    var authorityUri = new Uri(configuredUrl);
    var baseUrl = authorityUri.GetLeftPart(UriPartial.Authority);

    var identityServerBuilder = builder.Services.AddIdentityServer(options =>
    {
        options.IssuerUri = baseUrl;
    })
        .AddAspNetIdentity<ApplicationUser>()
        .AddInMemoryIdentityResources(new List<IdentityResource>
        {
            new IdentityResources.OpenId
            {
                UserClaims = { "sub", Constants.Claims.Fullname, Constants.Claims.LastLogin, Constants.Claims.Role }
            },
            new IdentityResources.Profile(),
        })
        .AddInMemoryApiScopes(new List<ApiScope>
        {
            new ApiScope("Timinute.ServerAPI", "Timinute Server API")
        })
        .AddInMemoryApiResources(new List<ApiResource>
        {
            new ApiResource("Timinute.ServerAPI", "Timinute Server API")
            {
                Scopes = { "Timinute.ServerAPI" }
            }
        })
        .AddInMemoryClients(new List<Client>
        {
            new Client
            {
                ClientId = "Timinute.Client",
                AllowedGrantTypes = GrantTypes.Code,
                RequirePkce = true,
                RequireClientSecret = false,
                AllowedCorsOrigins = { baseUrl },
                AllowedScopes = { "openid", "profile", "Timinute.ServerAPI" },
                RedirectUris = { $"{baseUrl}/authentication/login-callback" },
                PostLogoutRedirectUris = { $"{baseUrl}/authentication/logout-callback" },
            }
        });

    if (builder.Environment.IsDevelopment())
    {
        identityServerBuilder.AddDeveloperSigningCredential();
    }
    else
    {
        // Duende IdentityServer enables automatic key management by default.
        // Keys are generated, rotated, and persisted to the /keys directory.
        identityServerBuilder.Services.Configure<Duende.IdentityServer.Configuration.KeyManagementOptions>(options =>
        {
            options.Enabled = true;
        });
    }
}

void DependecyInjection()
{
    // DI
    builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
    builder.Services.AddTransient<IRepositoryFactory, RepositoryFactory>();
}


void SwaggerSetup()
{
    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen(c =>
    {
        c.SwaggerDoc("v1", new OpenApiInfo
        {
            Title = "Timinute API",
            Description = "Docs for Timinute API",
            Version = "v1",
            Contact = new OpenApiContact { Email = "jame_581@windowslive.com", Name = "Jan Mesarc" }
        });
        c.ResolveConflictingActions(apiDescriptors => apiDescriptors.First());
    });
}
