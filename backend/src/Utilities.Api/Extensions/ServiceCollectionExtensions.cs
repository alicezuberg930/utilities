using System.Text.Json.Serialization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Utilities.Api.Filters;
using Utilities.Api.Middleware;
using Utilities.Api.Models.Common;
using Utilities.Application.Interfaces;
using Utilities.Application.Services;

namespace Utilities.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApi(this IServiceCollection services, IConfiguration configuration)
    {
        services
            .AddControllers(options => options.Filters.Add<ResponseEnvelopeFilter>())
            .AddJsonOptions(options =>
            { options.JsonSerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow; });

        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(new ApiErrorResponse(StatusCodes.Status400BadRequest, "The request body is invalid."));
        });

        services.AddScoped<IYoutubeService, YoutubeService>();
        services.AddExceptionHandler<ApiExceptionHandler>();
        services.AddProblemDetails();

        services.AddCors(options =>
        {
            options.AddPolicy("Frontend", policy =>
            {
                var origins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
                var additionalOrigins = configuration["CORS_ORIGINS"]?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? [];

                origins = [.. origins, .. additionalOrigins];
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        });

        return services;
    }
}
