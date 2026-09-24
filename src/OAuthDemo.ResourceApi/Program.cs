using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using OAuthDemo.ResourceApi.Data;
using OAuthDemo.ResourceApi.Models;
using OAuthDemo.Shared.Constants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// Configure EF Core with in-memory database for client-specific data
builder.Services.AddDbContext<ResourceDbContext>(options =>
{
    options.UseInMemoryDatabase("ResourceDb");
});

// Configure JWT Bearer authentication (validates tokens from AuthServer)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = "https://localhost:7001"; // AuthServer URL
        options.Audience = "https://resource-api/"; // Must match the resource registered in AuthServer (with trailing slash)
        options.RequireHttpsMetadata = true;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
        };
    });

// Configure authorization policies based on scopes
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy(Policies.RequireReadScope, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", Scopes.ApiRead));
    
    options.AddPolicy(Policies.RequireWriteScope, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", Scopes.ApiWrite));
    
    options.AddPolicy(Policies.RequireAdminScope, policy =>
        policy.RequireAuthenticatedUser().RequireClaim("scope", Scopes.ApiAdmin));
});

// Configure Swagger with OAuth2 Client Credentials flow
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "OAuth Demo Resource API",
        Version = "v1",
        Description = "Protected API demonstrating OAuth 2.0 Client Credentials flow with client-specific data isolation"
    });

    // Add OAuth2 security scheme for Client Credentials flow
    options.AddSecurityDefinition("oauth2", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.OAuth2,
        Flows = new OpenApiOAuthFlows
        {
            ClientCredentials = new OpenApiOAuthFlow
            {
                TokenUrl = new Uri("https://localhost:7001/connect/token", UriKind.Absolute),
                Scopes = new Dictionary<string, string>
                {
                    { Scopes.ApiRead, "Read access to API resources" },
                    { Scopes.ApiWrite, "Write access to API resources" },
                    { Scopes.ApiAdmin, "Admin access to all data" }
                }
            }
        },
        Description = "OAuth 2.0 Client Credentials flow"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "oauth2"
                }
            },
            new[] { Scopes.ApiRead }
        }
    });
});

// Add hosted service to seed test data
builder.Services.AddHostedService<DataSeeder>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "OAuth Demo Resource API v1");
        options.OAuthClientId("swagger-ui");
        options.OAuthClientSecret("swagger-secret");
        options.OAuthUsePkce();
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
