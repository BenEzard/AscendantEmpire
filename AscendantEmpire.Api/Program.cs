
using AscendantEmpire.Api.Middleware.Validators;
using AscendantEmpire.Application;
using AscendantEmpire.Application.Data;

using FluentValidation;

using MediatR;

using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.EntityFrameworkCore;

using System.Reflection;
using System.Text.RegularExpressions;

namespace AscendantEmpire.Api
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.

            builder.Services.AddControllers(options =>
            {
                // Use kebab-case for route parameters
                options.Conventions.Add(new RouteTokenTransformerConvention(new KebabCaseParameterTransformer()));
            });

            // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                c.IncludeXmlComments(xmlPath);
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

            builder.Services.AddDbContextFactory<AscendantEmpireContext>(options =>
                options.UseSqlServer(builder.Configuration.GetConnectionString("AscendantEmpireDatabase"))
            );

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
