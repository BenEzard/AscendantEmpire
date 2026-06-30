
using AscendantEmpire.Api.Authentication;
using AscendantEmpire.Api.Middleware.Validators;
using AscendantEmpire.Application;
using AscendantEmpire.Application.Data;

using FluentValidation;

using Serilog;

using MediatR;

using Microsoft.AspNetCore.Authentication.JwtBearer;
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
                    .AllowAnyHeader());
            });

            // Auto-register MediatR handlers 
            builder.Services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(MediatrStartup).Assembly));

            // Auto-validate
            builder.Services.AddValidatorsFromAssembly(typeof(MediatrStartup).Assembly, includeInternalTypes: true);

            builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehaviour<,>));

            builder.Services.AddApplicationServices();

            // Strongly-typed options for the Application layer.
            builder.Services.Configure<AscendantEmpire.Application.Options.ExportSettings>(
                builder.Configuration.GetSection("ExportSettings"));

            builder.Services.Configure<AscendantEmpire.Application.Options.ImportSettings>(
                builder.Configuration.GetSection("ImportSettings"));

            builder.Services.Configure<AscendantEmpire.Application.Options.MapGenerationSettings>(
                builder.Configuration.GetSection("MapGenerationSettings"));

            builder.Services.Configure<AscendantEmpire.Application.Options.BackgroundProcessingSettings>(
                builder.Configuration.GetSection("BackgroundProcessingSettings"));

            builder.Services.AddDbContextFactory<AscendantEmpireContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("AscendantEmpireDatabase"))
            );

            // --- Authentication ----------------------------------------------------------------
            // Bind the "Jwt" settings section and register the service that builds tokens.
            builder.Services.Configure<JwtSettings>(builder.Configuration.GetSection(JwtSettings.SectionName));
            builder.Services.AddSingleton<ITokenService, JwtTokenService>();

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

            builder.Services.AddAuthorization();

            IServiceCollection services = builder.Services;

            var app = builder.Build();

            // Configure the HTTP request pipeline.
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
