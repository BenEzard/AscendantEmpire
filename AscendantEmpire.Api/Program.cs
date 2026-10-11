
using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Api.Middleware.ExceptionHandling;
using AscendantEmpire.Api.Utilities;
using AscendantEmpire.Application;
using AscendantEmpire.Application.Common.Messaging;
using AscendantEmpire.Application.Data;
using AscendantEmpire.Domain.Enums.Authentication;

using Serilog;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace AscendantEmpire.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

            // Add services to the container.

            builder.Services.AddControllers(options =>
            {
                // Use kebab-case for route parameters
                options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));

                // Remove in F-18. Drops the [DevelopmentOnly] controllers — currently just DevController — from the application model outside Development,
                // so their routes do not exist rather than merely refusing callers.
                if (builder.Environment.IsDevelopment() == false)
                {
                    options.Conventions.Add(new DevelopmentOnlyControllerConvention());
                }
            })
            .AddJsonOptions(options =>
            {
                // Serialize/deserialize enums by their name (e.g. "Classification") rather than the
                // default numeric value. The React frontend sends and reads enum *names* across the
                // whole app (TagType, ComponentType, etc.), so without this the names fail to bind and
                // ASP.NET returns a 400 before any handler runs.
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);

                // Add a "Authorize" button to Swagger UI so a bearer token can be pasted in and
                // sent on protected endpoints while testing.
                c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    In = ParameterLocation.Header,
                    Description = "Paste the JWT (without the 'Bearer ' prefix) returned by /auth/login.",
                });

                // Swashbuckle 10 takes a factory so the requirement can reference the document.
                c.AddSecurityRequirement(doc => new OpenApiSecurityRequirement
                {
                    {
                        // In Microsoft.OpenApi v2 a scheme is referenced by id via this reference type.
                        new OpenApiSecuritySchemeReference("Bearer", doc, null),
                        new List<string>()
                    }
                });
            });

            // Add CORS policy to allow requests from the React app (note line further down to set it up)
            builder.Services.AddCors(options =>
            {
                options.AddPolicy("AllowReactApp",
                    policy => policy.WithOrigins("http://localhost:5173")
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    // AllowAnyHeader governs which *request* headers the browser may send. A response header stays invisible to JavaScript unless it is named
                    // here, so without this line the correlation id on an error response could be read by curl and Swagger but not by the app that needs it.
                    .WithExposedHeaders(GlobalExceptionHandler.CorrelationIdHeaderName));
            });

            // Auto-registers every request handler in the Application assembly, plus the mediator that dispatches to them.
            builder.Services.AddMediator(typeof(ApplicationStartup).Assembly);

            // Request validators and the pipeline behaviour that runs them are registered by AddApplicationServices below, alongside the requests they
            // validate.

            // Turns anything that escapes a handler into the { errors: [] } contract the frontend reads, instead of the framework default (a text/plain stack
            // trace in Development, an empty 500 in Production). Registered as a singleton by AddExceptionHandler, so it may only depend on singletons.
            builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

            builder.Services.AddApplicationServices();

            // Strongly-typed options for the Application layer.
            builder.Services.Configure<Application.Options.ExportSettings>(builder.Configuration.GetSection("ExportSettings"));
            builder.Services.Configure<Application.Options.ImportSettings>(builder.Configuration.GetSection("ImportSettings"));
            builder.Services.Configure<Application.Options.MapGenerationSettings>(builder.Configuration.GetSection("MapGenerationSettings"));
            builder.Services.Configure<Application.Options.BackgroundProcessingSettings>(builder.Configuration.GetSection("BackgroundProcessingSettings"));
            builder.Services.Configure<Application.Options.GameSettings>(builder.Configuration.GetSection("GameSettings"));

            // The base appsettings.json deliberately carries no connection string, so that no single machine's SQL Server becomes the silent fallback for
            // every environment. That only helps if a missing value is noticed: UseSqlServer accepts null and defers the failure to the first query, which
            // would surface as a broken request long after startup. Check it here instead, matching the fail-fast treatment Jwt:SigningKey gets below.
            string? databaseConnectionString = builder.Configuration.GetConnectionString("AscendantEmpireDatabase");

            if (string.IsNullOrWhiteSpace(databaseConnectionString))
            {
                throw new InvalidOperationException(
                    "ConnectionStrings:AscendantEmpireDatabase is not configured. Set it via user-secrets, an environment variable "
                    + "(ConnectionStrings__AscendantEmpireDatabase), or the environment's appsettings file.");
            }

            builder.Services.AddDbContextFactory<AscendantEmpireContext>(options =>
                options.UseSqlServer(databaseConnectionString)
            );

            // --- Authentication ----------------------------------------------------------------
            // Bind the "Jwt" settings section and register the service that builds tokens.
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
            builder.Services.AddSingleton<ITokenService, JwtTokenService>();

            // Grants every permission to the configured administrator when nobody holds SystemPanel yet, so a
            // database with no administrator can still be administered. No-ops when the setting is blank.
            builder.Services.Configure<BootstrapSettings>(builder.Configuration.GetSection(BootstrapSettings.SectionName));
            builder.Services.AddHostedService<PermissionBootstrapService>();

            JwtSettings jwtSettings = builder.Configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                ?? throw new InvalidOperationException("Missing 'Jwt' configuration section.");

            if (string.IsNullOrWhiteSpace(jwtSettings.SigningKey))
            {
                // Fail fast with a clear message rather than producing unverifiable tokens.
                throw new InvalidOperationException(
                    "Jwt:SigningKey is not configured. Set it via user-secrets or an environment variable.");
            }

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(options =>
                {
                    // Validate everything we put into the token so a tampered or foreign token is rejected.
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtSettings.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtSettings.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey)),
                        ValidateLifetime = true,
                        // Don't grant the usual 5-minute grace period on expiry.
                        ClockSkew = TimeSpan.Zero,
                    };
                });

            // Deny by default. Without a fallback policy ASP.NET treats an action with no [Authorize] attribute as anonymous, which is how most of the API
            // ended up publicly writable. Anything genuinely public now has to opt out in the open with [AllowAnonymous] — currently only register, login
            // and refresh on AuthController, since you can't require a token to ask for one.
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .Build();

                // One policy per permission, named after the enum member, so a controller can say
                // [Authorize(Policy = nameof(PermissionType.MapEditor))] and get a compile-checked name rather than a
                // string literal. Registering them in a loop means a new enum member can never be forgotten here.
                foreach (PermissionType permission in Enum.GetValues<PermissionType>())
                {
                    options.AddPolicy(permission.ToString(),
                        policy => policy.RequireClaim(AuthClaimTypes.Permission, permission.ToString()));
                }
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline.

            // First, so it wraps every middleware below it as well as the endpoints themselves. Keeping UseCors inside it is what lets the browser read an
            // error response at all: the CORS middleware applies its headers from an OnStarting callback, and that callback survives the Response.Clear() the
            // exception handling middleware performs before it hands over.
            //
            // The options object is not optional. UseExceptionHandler() throws at startup unless ExceptionHandler, ExceptionHandlingPath or a registered
            // IProblemDetailsService is present, and registered IExceptionHandler services do not satisfy that check. Naming our own fallback rather than
            // calling AddProblemDetails() keeps every error body in the DTApiErrorResult shape; ProblemDetails would emit application/problem+json, which the
            // frontend cannot read. GlobalExceptionHandler still runs first and handles everything, so the fallback is a safety net rather than a code path.
            app.UseExceptionHandler(new ExceptionHandlerOptions
            {
                ExceptionHandler = GlobalExceptionHandler.WriteFallbackResponseAsync,
            });

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                
                app.UseSwaggerUI(c =>
                {
                    c.DefaultModelsExpandDepth(-1); // Disable schemas from being shown in Swagger UI
                });
            }

            app.UseHttpsRedirection();

            // Use CORS - this line must be placed before UseAuthorization
            app.UseCors("AllowReactApp");

            // Authentication must run before authorization so the user identity is established
            // from the bearer token before [Authorize] checks happen.
            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }

    /// <summary>
    /// Transforms parameter names from camelCase to kebab-case for URL routing.
    /// </summary>
    public class KebabCaseParameterTransformer : IOutboundParameterTransformer
    {
        public string? TransformOutbound(object? value)
        {
            if (value == null) return null;

            // Split camel case and lowercase it
            return Regex.Replace(value.ToString()!, "([a-z])([A-Z])", "$1-$2").ToLower();
        }
    }
}
