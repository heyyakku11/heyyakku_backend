using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Yakku.API.Configuration;
using Yakku.API.Middleware;
using Yakku.API.Swagger;
using Yakku.Application;
using Yakku.Application.Auth;
using Yakku.Application.Common.Responses;
using Yakku.Infrastructure;
using Yakku.Infrastructure.Persistence;

EnvLoader.Load();

var builder = WebApplication.CreateBuilder(args);

var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

var jwtSecret = EnvLoader.GetRequired("JWT_SECRET").Trim().Trim('"');
if (Encoding.UTF8.GetByteCount(jwtSecret) < 32)
{
    throw new InvalidOperationException("JWT_SECRET must be at least 32 characters.");
}

builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = InvalidModelStateFactory.Create;
    });
builder.Services.AddExceptionHandler<FluentValidationExceptionHandler>();
builder.Services.AddExceptionHandler<AppExceptionHandler>();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(
                        "Unauthorized.",
                        [
                            new ApiError
                            {
                                Code = ApiErrorCodes.Unauthorized,
                                Message = "Unauthorized."
                            }
                        ]));
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ApiResponse.Fail(
                        "Forbidden.",
                        [
                            new ApiError
                            {
                                Code = ApiErrorCodes.Forbidden,
                                Message = "Forbidden."
                            }
                        ]));
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireClaim(AuthClaimTypes.Actor, AuthClaimTypes.AdminActor));
});

builder.Services.AddDbContext<YakkuDbContext>(options =>
    options.UseNpgsql(PostgresConnection.Normalize(EnvLoader.GetRequired("DB_CONNECTION_STRING"))));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Access token from POST /api/v1/auth/verify-otp"
    });
    options.OperationFilter<AuthorizeCheckOperationFilter>();
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure();
builder.Services.AddScoped<Yakku.API.Guests.GuestCookieService>();

var corsOrigins = ParseCorsOrigins(Environment.GetEnvironmentVariable("CORS_ORIGINS"));
if (corsOrigins.Length > 0)
{
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("YakkuCors", policy =>
        {
            policy.WithOrigins(corsOrigins)
                .AllowCredentials()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
    });
}

var app = builder.Build();

app.UseExceptionHandler();

if (corsOrigins.Length > 0)
{
    app.UseCors("YakkuCors");
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

static string[] ParseCorsOrigins(string? raw)
{
    if (string.IsNullOrWhiteSpace(raw))
    {
        return [];
    }

    return raw
        .Trim()
        .Trim('"')
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(StripTrailingComment)
        .Where(origin => origin.Length > 0)
        .ToArray();
}

static string StripTrailingComment(string value)
{
    // Require a preceding space so "https://" is not treated as a comment.
    var hash = value.IndexOf(" #", StringComparison.Ordinal);
    var slashSlash = value.IndexOf(" //", StringComparison.Ordinal);
    var cutAt = -1;
    if (hash >= 0)
    {
        cutAt = hash;
    }

    if (slashSlash >= 0 && (cutAt < 0 || slashSlash < cutAt))
    {
        cutAt = slashSlash;
    }

    return (cutAt >= 0 ? value[..cutAt] : value).Trim().Trim('"');
}
