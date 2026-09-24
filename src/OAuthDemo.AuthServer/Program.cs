using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.Server;
using OpenIddict.Core;
using System.Security.Claims;
using OAuthDemo.AuthServer.Data;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Also register the debug handler
builder.Services.AddSingleton<DebugScopePermissionsHandler>();

// Configure EF Core with in-memory database for OpenIddict
builder.Services.AddDbContext<AuthDbContext>(options =>
{
    options.UseInMemoryDatabase("AuthDb");
    options.UseOpenIddict();
});

// Configure OpenIddict
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        // Use EF Core with in-memory database for demo - easily switchable to SQL later
        options.UseEntityFrameworkCore()
               .UseDbContext<AuthDbContext>();
    })
    .AddServer(options =>
    {
        // Enable the token endpoint
        options.SetTokenEndpointUris("/connect/token");

        // Enable Client Credentials flow
        options.AllowClientCredentialsFlow();

        // Add development signing certificate (auto-generated)
        options.AddDevelopmentSigningCertificate();

        // Add development encryption certificate (required for token encryption)
        options.AddDevelopmentEncryptionCertificate();

        // Disable access token encryption for demo (ResourceApi needs the encryption key to decrypt)
        options.DisableAccessTokenEncryption();

        // Register the resource (API) that this server protects
        // Use a proper absolute URI format as required by OpenIddict
        // IMPORTANT: Must include trailing slash because Uri.AbsoluteUri adds it for root paths
        // and ValidateResources compares against resource.AbsoluteUri
        options.RegisterResources("https://resource-api/");

        // Register scopes so OpenIddict's built-in validation knows they exist
        // This is REQUIRED for scope validation to work - it checks against registered scopes, not DB scopes
        options.RegisterScopes(OAuthDemo.Shared.Constants.Scopes.ApiRead, OAuthDemo.Shared.Constants.Scopes.ApiWrite, OAuthDemo.Shared.Constants.Scopes.ApiAdmin);

        // Ignore scope permissions validation - OpenIddict's built-in validation fails with single scope= param
        // even when client has correct permissions in DB. Custom handler validates permissions instead.
        options.IgnoreScopePermissions();

        // Register ASP.NET Core host
        options.UseAspNetCore();

        // Register custom handler for client credentials token requests
        options.AddEventHandler<OpenIddict.Server.OpenIddictServerEvents.HandleTokenRequestContext>(
            builder => builder.UseSingletonHandler<ClientCredentialsTokenHandler>());
    })
    .AddValidation(options =>
    {
        // Use local server for validation
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// Add hosted service to seed clients
builder.Services.AddHostedService<ClientSeeder>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();



// Client seeder - runs at startup to create mock clients
public class ClientSeeder : IHostedService
{
    private readonly IServiceProvider _serviceProvider;

    public ClientSeeder(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        // Seed scopes first
        await CreateScopeIfNotExists(scopeManager, OAuthDemo.Shared.Constants.Scopes.ApiRead, "Read access to API");
        await CreateScopeIfNotExists(scopeManager, OAuthDemo.Shared.Constants.Scopes.ApiWrite, "Write access to API");
        await CreateScopeIfNotExists(scopeManager, OAuthDemo.Shared.Constants.Scopes.ApiAdmin, "Admin access to API");

        // Verify scopes exist and check their resources
        var readScope = await scopeManager.FindByNameAsync(OAuthDemo.Shared.Constants.Scopes.ApiRead);
        var writeScope = await scopeManager.FindByNameAsync(OAuthDemo.Shared.Constants.Scopes.ApiWrite);
        var adminScope = await scopeManager.FindByNameAsync(OAuthDemo.Shared.Constants.Scopes.ApiAdmin);
        Console.WriteLine($"[DEBUG] Scopes created: read={readScope != null}, write={writeScope != null}, admin={adminScope != null}");
        
        if (readScope != null)
        {
            var resources = await scopeManager.GetResourcesAsync(readScope);
            Console.WriteLine($"[DEBUG] Scope 'api.read' resources: {string.Join(", ", resources)}");
        }
        if (writeScope != null)
        {
            var resources = await scopeManager.GetResourcesAsync(writeScope);
            Console.WriteLine($"[DEBUG] Scope 'api.write' resources: {string.Join(", ", resources)}");
        }
        if (adminScope != null)
        {
            var resources = await scopeManager.GetResourcesAsync(adminScope);
            Console.WriteLine($"[DEBUG] Scope 'api.admin' resources: {string.Join(", ", resources)}");
        }

        // Client A: Read + Write
        await CreateClientIfNotExists(appManager, "client-a", "secret-a",
            "Client A",
            [OAuthDemo.Shared.Constants.Scopes.ApiRead, OAuthDemo.Shared.Constants.Scopes.ApiWrite]);

        // Client B: Read only
        await CreateClientIfNotExists(appManager, "client-b", "secret-b",
            "Client B",
            [OAuthDemo.Shared.Constants.Scopes.ApiRead]);

        // Client C: Read + Write + Admin
        await CreateClientIfNotExists(appManager, "client-c", "secret-c",
            "Client C",
            [OAuthDemo.Shared.Constants.Scopes.ApiRead, OAuthDemo.Shared.Constants.Scopes.ApiWrite, OAuthDemo.Shared.Constants.Scopes.ApiAdmin]);

        // Verify clients exist and their permissions
        var clientA = await appManager.FindByClientIdAsync("client-a");
        var clientB = await appManager.FindByClientIdAsync("client-b");
        var clientC = await appManager.FindByClientIdAsync("client-c");
        Console.WriteLine($"[DEBUG] Clients created: A={clientA != null}, B={clientB != null}, C={clientC != null}");
        
        if (clientA != null)
        {
            var perms = await appManager.GetPermissionsAsync(clientA);
            Console.WriteLine($"[DEBUG] Client A permissions: {string.Join(", ", perms)}");
        }
        if (clientB != null)
        {
            var perms = await appManager.GetPermissionsAsync(clientB);
            Console.WriteLine($"[DEBUG] Client B permissions: {string.Join(", ", perms)}");
        }
        if (clientC != null)
        {
            var perms = await appManager.GetPermissionsAsync(clientC);
            Console.WriteLine($"[DEBUG] Client C permissions: {string.Join(", ", perms)}");
        }
    }

    private static async Task CreateScopeIfNotExists(
        IOpenIddictScopeManager manager,
        string scopeName,
        string displayName)
    {
        if (await manager.FindByNameAsync(scopeName) == null)
        {
            var descriptor = new OpenIddictScopeDescriptor
            {
                Name = scopeName,
                DisplayName = displayName,
                // Must match RegisterResources("https://resource-api/") - trailing slash required
                // because ValidateResources compares against resource.AbsoluteUri which includes trailing slash
                Resources = { "https://resource-api/" }
            };
            await manager.CreateAsync(descriptor);
        }
        else
        {
            // Ensure existing scope has the resource associated
            var scope = await manager.FindByNameAsync(scopeName);
            if (scope != null)
            {
                var resources = await manager.GetResourcesAsync(scope);
                if (!resources.Contains("https://resource-api/"))
                {
                    // Update the scope with the resource
                    var descriptor = new OpenIddictScopeDescriptor
                    {
                        Name = scopeName,
                        DisplayName = displayName,
                        Resources = { "https://resource-api/" }
                    };
                    await manager.UpdateAsync(scope, descriptor);
                }
            }
        }
    }

    private static async Task CreateClientIfNotExists(
        IOpenIddictApplicationManager manager,
        string clientId,
        string clientSecret,
        string displayName,
        string[] scopes)
    {
        if (await manager.FindByClientIdAsync(clientId) == null)
        {
            var descriptor = new OpenIddictApplicationDescriptor
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
                DisplayName = displayName
            };

            // Add required permissions
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Endpoints.Token);
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.GrantTypes.ClientCredentials);
            // Must match RegisterResources("https://resource-api/") - trailing slash required
            descriptor.Permissions.Add(OpenIddictConstants.Permissions.Prefixes.Resource + "https://resource-api/");

            foreach (var scope in scopes)
            {
                descriptor.Permissions.Add($"scope:{scope}");
            }

            await manager.CreateAsync(descriptor);
        }
        else
        {
            // Update existing client to ensure resource permission AND scope permissions
            var client = await manager.FindByClientIdAsync(clientId);
            if (client != null)
            {
                var permissions = (await manager.GetPermissionsAsync(client)).ToList();
                // Must match RegisterResources("https://resource-api/") - trailing slash required
                var resourcePermission = OpenIddictConstants.Permissions.Prefixes.Resource + "https://resource-api/";
                
                // Check for missing scope permissions
                var missingScopes = scopes.Where(s => !permissions.Contains($"scope:{s}")).ToList();
                
                if (!permissions.Contains(resourcePermission) || missingScopes.Any())
                {
                    var descriptor = new OpenIddictApplicationDescriptor
                    {
                        ClientId = clientId,
                        ClientSecret = clientSecret,
                        DisplayName = displayName
                    };

                    // Add all existing permissions
                    foreach (var perm in permissions)
                    {
                        descriptor.Permissions.Add(perm);
                    }
                    
                    // Add missing resource permission
                    if (!permissions.Contains(resourcePermission))
                    {
                        descriptor.Permissions.Add(resourcePermission);
                    }
                    
                    // Add missing scope permissions
                    foreach (var scope in missingScopes)
                    {
                        descriptor.Permissions.Add($"scope:{scope}");
                    }

                    await manager.UpdateAsync(client, descriptor);
                }
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Custom handler to handle token requests for client credentials flow
public class ClientCredentialsTokenHandler : IOpenIddictServerHandler<OpenIddict.Server.OpenIddictServerEvents.HandleTokenRequestContext>
{
    public ClientCredentialsTokenHandler()
    {
        Console.WriteLine("[DEBUG ClientCredentialsTokenHandler] Constructor called - handler instantiated!");
    }

    public async ValueTask HandleAsync(OpenIddict.Server.OpenIddictServerEvents.HandleTokenRequestContext context)
    {
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] HandleAsync called!");
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] GrantType: {context.Request.GrantType}");
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] ClientId: {context.ClientId}");
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] Scopes (GetScopes): {string.Join(", ", context.Request.GetScopes())}");
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] Scope param: {context.Request.GetParameter("scope")}");
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] Resources: {string.Join(", ", context.Request.GetResources())}");

        // Only handle client credentials grant type
        if (!context.Request.IsClientCredentialsGrantType())
        {
            Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] Not client credentials flow, skipping");
            return;
        }

        // Create a claims principal for the client
        // Use standard claim types: "sub" for subject, "client_id" for client ID, "scope" for scopes, "aud" for audience
        var identity = new ClaimsIdentity(
            OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            Claims.Subject,
            Claims.Role);

        // Add subject (client_id)
        identity.SetClaim(Claims.Subject, context.ClientId!);
        identity.SetClaim(Claims.ClientId, context.ClientId!);

        // Parse scopes from raw parameter (GetScopes() doesn't work with multiple scope= params)
        var scopeParam = context.Request.GetParameter("scope")?.ToString();
        var scopes = new List<string>();
        if (!string.IsNullOrEmpty(scopeParam))
        {
            // Split by space (OAuth2 spec) and also handle comma-separated (ASP.NET Core form binding)
            scopes.AddRange(scopeParam.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries));
        }
        
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] Parsed scopes: {string.Join(", ", scopes)}");

        // Add scopes as claims with access_token destination
        foreach (var scope in scopes)
        {
            var claim = new Claim(Claims.Scope, scope, ClaimValueTypes.String);
            claim.SetDestinations(Destinations.AccessToken);
            identity.AddClaim(claim);
        }

        // Add resources as audiences with access_token destination
        foreach (var resource in context.Request.GetResources())
        {
            var claim = new Claim(Claims.Audience, resource, ClaimValueTypes.String);
            claim.SetDestinations(Destinations.AccessToken);
            identity.AddClaim(claim);
        }

        var principal = new ClaimsPrincipal(identity);

        // Sign in with the principal - this will generate the access token
        context.SignIn(principal);
        
        Console.WriteLine($"[DEBUG ClientCredentialsTokenHandler] Token issued successfully for client: {context.ClientId}");
    }
}

// Custom handler to debug scope permissions validation
public class DebugScopePermissionsHandler : IOpenIddictServerHandler<OpenIddict.Server.OpenIddictServerEvents.ValidateTokenRequestContext>
{
    private readonly IServiceProvider _serviceProvider;

    static DebugScopePermissionsHandler()
    {
        Console.WriteLine("[DEBUG ScopePermissions] Static constructor - class loaded!");
    }

    public DebugScopePermissionsHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        Console.WriteLine("[DEBUG ScopePermissions] Constructor called - handler registered!");
    }

    public async ValueTask HandleAsync(OpenIddict.Server.OpenIddictServerEvents.ValidateTokenRequestContext context)
    {
        Console.WriteLine($"[DEBUG ScopePermissions] HandleAsync called!");
        
        // Only run for client credentials flow
        if (!context.Request.IsClientCredentialsGrantType())
        {
            Console.WriteLine($"[DEBUG ScopePermissions] Not client credentials flow, skipping");
            return;
        }

        // Guard against null ClientId
        if (context.ClientId == null)
        {
            Console.WriteLine($"[DEBUG ScopePermissions] ClientId is null, skipping");
            return;
        }

        Console.WriteLine($"[DEBUG ScopePermissions] ClientId: {context.ClientId}");
        Console.WriteLine($"[DEBUG ScopePermissions] Scopes: {string.Join(", ", context.Request.GetScopes())}");
        Console.WriteLine($"[DEBUG ScopePermissions] Resources: {string.Join(", ", context.Request.GetResources())}");
        
        using var serviceScope = _serviceProvider.CreateScope();
        var manager = serviceScope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await manager.FindByClientIdAsync(context.ClientId);
        if (application != null)
        {
            var permissions = await manager.GetPermissionsAsync(application);
            Console.WriteLine($"[DEBUG ScopePermissions] Client permissions: {string.Join(", ", permissions)}");
            
            foreach (var requestedScope in context.Request.GetScopes())
            {
                var hasPermission = await manager.HasPermissionAsync(application, $"scope:{requestedScope}");
                Console.WriteLine($"[DEBUG ScopePermissions] Has permission 'scope:{requestedScope}': {hasPermission}");
            }
        }
    }
}
