using Asp.Versioning;
using Duende.IdentityServer.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.CookiePolicy;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Serilog;
using Serilog.Formatting.Compact;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Timinute.Server;
using Timinute.Server.Areas.Identity;
using Timinute.Server.Data;
using Timinute.Server.Helpers;
using Timinute.Server.Models;
using Timinute.Server.Repository;
using Timinute.Server.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Local-dev convenience: let MSSQL_SA_PASSWORD (the same var the container and
// docker-compose use) drive the app's SA password too, so one variable unifies
// everything. Guarded to the built-in dev default only — an explicit connection
// override (production) or the compose string (already password-substituted) has a
// different password and is left untouched.
var saPassword = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
if (!string.IsNullOrEmpty(saPassword) && !string.IsNullOrEmpty(connectionString))
{
    var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(connectionString);
    if (csb.Password == "TiminuteAdmin.")
    {
        csb.Password = saPassword;
        connectionString = csb.ConnectionString;
    }
}

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var dpKeysPath = builder.Configuration["DataProtection:KeyPath"];
if (!string.IsNullOrEmpty(dpKeysPath))
{
    builder.Services.AddDataProtection()
        .PersistKeysToFileSystem(new DirectoryInfo(dpKeysPath));
}

// Identity configuration
IdentitySetup();

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = "ApplicationDefinedPolicy";
        options.DefaultChallengeScheme = "ApplicationDefinedPolicy";
    })
    .AddJwtBearer(options =>
    {
        options.Authority = builder.Configuration["IdentityServer:Authority"] ?? Constants.Api.DefaultAuthority;
        options.Audience = Constants.Api.ResourceName;
        options.MapInboundClaims = false;
        options.TokenValidationParameters.NameClaimType = "name";
        options.TokenValidationParameters.RoleClaimType = Constants.Claims.Role;
    })
    // displayName must stay null — schemes with a display name are listed by the
    // Identity UI as external login providers ("or continue with" buttons).
    .AddPolicyScheme("ApplicationDefinedPolicy", displayName: null, options =>
    {
        options.ForwardDefaultSelector = context =>
        {
            string? authorization = context.Request.Headers.Authorization;
            if (!string.IsNullOrWhiteSpace(authorization) && authorization.TrimStart().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return JwtBearerDefaults.AuthenticationScheme;

            return IdentityConstants.ApplicationScheme;
        };
    });

builder.Host.UseSerilog((context, loggerConfiguration) =>
{
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext();

    // Console: human-readable in Development, compact JSON everywhere else
    // (one JSON object per line — captured by Docker's json-file driver).
    if (context.HostingEnvironment.IsDevelopment())
    {
        loggerConfiguration.WriteTo.Console();
    }
    else
    {
        loggerConfiguration.WriteTo.Console(new CompactJsonFormatter());
    }

    // Rolling file sink — opt-in via Serilog:File:Enabled (Docker env-var friendly).
    if (context.Configuration.GetValue("Serilog:File:Enabled", false))
    {
        var path = context.Configuration.GetValue<string>("Serilog:File:Path") ?? "/logs/timinute-.log";
        var retained = context.Configuration.GetValue("Serilog:File:RetainedFileCountLimit", 14);
        loggerConfiguration.WriteTo.File(
            new CompactJsonFormatter(),
            path,
            rollingInterval: RollingInterval.Day,
            retainedFileCountLimit: retained,
            fileSizeLimitBytes: 50 * 1024 * 1024,
            rollOnFileSizeLimit: true);
    }
});

builder.Services.AddRazorPages();

builder.Services.AddResponseCaching();

builder.Services.AddControllers(options =>
{
    options.ReturnHttpNotAcceptable = true;
    options.CacheProfiles.Add(Constants.CacheProfiles.Default120, new CacheProfile() { Duration = 120, Location = ResponseCacheLocation.Client });


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

// Preparatory only (spec v2.3): every existing route keeps working as
// implicit v1.0; no /v1/ URL segments because client + server ship together.
// AddMvc() is required to attach versioning to MVC controllers — without it
// the options above are registered but never enforced or reported.
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddMvc();

// DI Configuration
DependecyInjection();

// Auto Mapper Configurations
builder.Services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

// Swagger Configuration
SwaggerSetup();

// Reverse-proxy support. Docker users terminate TLS upstream; we trust
// X-Forwarded-* from any source inside the private container network.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor
                             | ForwardedHeaders.XForwardedProto
                             | ForwardedHeaders.XForwardedHost;

    // Only clear the known-proxy trust list when the operator explicitly opts in.
    // The default ASP.NET Core behavior (loopback-only trust) is safer for
    // accidentally-internet-exposed deployments; Docker sets this true in its
    // ENV because the container talks to a proxy on a private docker network.
    if (builder.Configuration.GetValue("ForwardedHeaders:AllowAnyProxy", false))
    {
        options.KnownIPNetworks.Clear();
        options.KnownProxies.Clear();
    }
});

// Same-origin auth → SameSite=Lax is correct and works in both HTTP-only
// (local smoke / dev) and HTTPS-behind-reverse-proxy (production). The
// default SameSite=None setting on Identity / IdentityServer cookies is
// dropped silently by browsers when the request is not HTTPS, which breaks
// the login flow in any non-TLS deployment. Secure flag mirrors the request
// scheme so it's set in production-https and unset in http-smoke.
builder.Services.Configure<CookiePolicyOptions>(options =>
{
    options.MinimumSameSitePolicy = SameSiteMode.Lax;
    options.Secure = CookieSecurePolicy.SameAsRequest;
});

var app = builder.Build();

if (app.Configuration.GetValue("DatabaseMigrationOnStartup", false))
{
    using var scope = app.Services.CreateScope();
    scope.ServiceProvider.GetRequiredService<ApplicationDbContext>()
         .Database.Migrate();
}

app.UseForwardedHeaders();

app.UseMiddleware<Timinute.Server.Middleware.CorrelationIdMiddleware>();

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

// Baseline security headers on every response. This runs before the static-file
// and Blazor-framework middleware so it covers those responses too.
//
// X-Frame-Options is SAMEORIGIN, NOT DENY: Blazor WASM's OIDC stack renews the
// access token silently by loading the authorize endpoint in a hidden same-origin
// iframe. DENY blocks same-origin framing as well, which would break token renewal
// and log users out on expiry. SAMEORIGIN still stops cross-origin clickjacking.
//
// Content-Security-Policy is deliberately absent — it needs wasm-unsafe-eval for
// Blazor and its frame-src/frame-ancestors interact with that same silent-renew
// iframe. It needs its own testing pass; see the v2.3.1 spec.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "SAMEORIGIN";
    headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    await next();
});

app.UseCookiePolicy();
app.UseHttpsRedirection();

app.UseResponseCaching();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

// One structured event per request (method, path, status, elapsed ms), enriched
// with CorrelationId from LogContext. Bodies/headers are never logged. Placed after
// the static-file middleware so static/framework asset hits short-circuit before
// reaching it (cutting always-on asset log noise on cold Blazor WASM loads); it stays
// inside CorrelationIdMiddleware so the CorrelationId LogContext still wraps it.
app.UseSerilogRequestLogging(options =>
{
    // Never log the query string. Password-reset / email-confirmation links and the
    // OIDC login-callback carry secrets in the query string, so leaking the query into
    // the console (docker logs) or file sink is an account-takeover vector.
    //
    // Serilog derives the RequestPath property — used in BOTH the rendered message and
    // the structured JSON — from a single value: IHttpRequestFeature.Path (the path
    // WITHOUT the query) when IncludeQueryInRequestPath is false, or the raw target
    // (path + query) when true. It defaults to false, but we pin it explicitly so a
    // future flip can never silently start writing those tokens to logs.
    //
    // NOTE: overwriting "RequestPath" via EnrichDiagnosticContext does NOT work on
    // Serilog.AspNetCore 10.0.0 — the built-in RequestPath property is appended after
    // the diagnostic-context properties and wins the LogEvent last-writer merge, so a
    // Set("RequestPath", ...) is silently discarded. This option is the real control.
    options.IncludeQueryInRequestPath = false;
});

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

    var configuredUrl = builder.Configuration["IdentityServer:Authority"] ?? Constants.Api.DefaultAuthority;
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
            new ApiScope(Constants.Api.ResourceName, "Timinute Server API")
        })
        .AddInMemoryApiResources(new List<ApiResource>
        {
            new ApiResource(Constants.Api.ResourceName, "Timinute Server API")
            {
                Scopes = { Constants.Api.ResourceName }
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
                AllowedScopes = { "openid", "profile", Constants.Api.ResourceName },
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
        var keyPath = builder.Configuration["IdentityServer:KeyManagement:KeyPath"];

        // Duende IdentityServer enables automatic key management by default.
        // KeyPath defaults to "keys" relative to CWD; set explicitly via config
        // for any deployment with a stable persistent directory.
        // NOTE: We configure IdentityServerOptions (not KeyManagementOptions) because
        // Duende registers an IPostConfigureOptions<KeyManagementOptions> that copies
        // values from IdentityServerOptions.KeyManagement.* and would otherwise
        // overwrite anything we set directly on KeyManagementOptions.
        identityServerBuilder.Services.Configure<Duende.IdentityServer.Configuration.IdentityServerOptions>(options =>
        {
            options.KeyManagement.Enabled = true;
            if (!string.IsNullOrEmpty(keyPath))
            {
                options.KeyManagement.KeyPath = keyPath;
            }
        });
    }
}

void DependecyInjection()
{
    // DI
    builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, ApplicationUserClaimsPrincipalFactory>();
    builder.Services.AddTransient<IRepositoryFactory, RepositoryFactory>();
    builder.Services.AddSingleton<IExportService, ExportService>();
    builder.Services.AddHostedService<TrashPurgeService>();
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

// Exposes the implicit Program class to WebApplicationFactory<Program>
// in Timinute.Server.Tests integration tests.
public partial class Program { }
