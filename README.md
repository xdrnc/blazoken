# OAuthDemo - OAuth 2.0 Client Credentials Flow Demo

A complete demonstration of OAuth 2.0 Client Credentials Flow using OpenIddict 7.7.1 with ASP.NET Core and Entity Framework Core.

## 🎯 Project Purpose

This project demonstrates a machine-to-machine (M2M) authentication scenario where:
* **AuthServer** issues JWT access tokens to clients.
* **ResourceApi** validates tokens and enforces scope-based authorization.
* Three clients with different permissions access protected resources.

---

## 🏗️ Architecture

```text
┌─────────────────┐     JWT Token      ┌──────────────────┐
│   Client App    │ ─────────────────► │   ResourceApi    │
│  (client-a/b/c) │                    │   (port 5002)    │
└────────┬────────┘                    └────────┬─────────┘
         │                                      │
         │ POST /connect/token                  │ Validates JWT
         │ grant_type=client_credentials        │ Checks scope claims
         ▼                                      ▼
┌─────────────────┐                    ┌──────────────────┐
│   AuthServer    │                    │   In-Memory DB   │
│  (port 7001)    │                    │   (Client Data)  │
│                 │                    └──────────────────┘
│ • Issues JWT    │
│ • Manages clients/scopes
│ • OpenIddict 7.7.1
└─────────────────┘
```

---

## 📁 Project Structure

```text
blazoken/
├── src/
│   ├── OAuthDemo.AuthServer/         # Authorization Server (port 7001)
│   │   ├── Program.cs                # Main entry + OpenIddict config + custom handlers
│   │   ├── Data/AuthDbContext.cs     # EF Core context for OpenIddict
│   │   ├── OAuthDemo.AuthServer.http # REST Client test requests
│   │   └── Properties/launchSettings.json
│   │
│   ├── OAuthDemo.ResourceApi/        # Protected Resource API (port 5002)
│   │   ├── Program.cs                # JWT validation + Swagger + policies
│   │   ├── Controllers/
│   │   │   ├── ClientDataController.cs # GET/POST /api/my-data (read/write scopes)
│   │   │   └── AdminController.cs      # GET /api/admin/* (admin scope)
│   │   ├── Data/
│   │   │   ├── ResourceDbContext.cs    # EF Core context for client data
│   │   │   └── DataSeeder.cs           # Seeds test data per client
│   │   ├── Models/ClientData.cs        # Data model
│   │   ├── OAuthDemo.ResourceApi.http  # REST Client test requests
│   │   └── Properties/launchSettings.json
│   │
│   └── OAuthDemo.Shared/             # Shared constants
│       └── Constants/
│           ├── Scopes.cs             # api.read, api.write, api.admin
│           └── Policies.cs           # Authorization policy names
│
├── trackrun.md        # Batch execution plan (completed)
├── trackissue.md      # Issue tracker (all resolved)
├── trackdev.md        # Development log (completed)
└── OAuthDemo.slnx     # Solution file
```

---

## 🔐 Clients & Permissions

| Client ID | Secret | Scopes | Can Access |
| :--- | :--- | :--- | :--- |
| `client-a` | `secret-a` | `api.read api.write` | `GET/POST /api/my-data` |
| `client-b` | `secret-b` | `api.read` | `GET /api/my-data` only |
| `client-c` | `secret-c` | `api.read api.write api.admin` | All endpoints including `/api/admin/*` |

---

## 🚀 Quick Start

### Prerequisites
* **.NET 10.0 SDK** (also supports `net8.0`)
* **VS Code** with REST Client extension (optional)

### Run Both Services

```bash
# Terminal 1: Start AuthServer (HTTPS on 7001)
cd src/OAuthDemo.AuthServer
dotnet run --launch-profile https

# Terminal 2: Start ResourceApi (HTTP on 5002)
cd src/OAuthDemo.ResourceApi
dotnet run --launch-profile http
```

### Trust Dev Certificate (One-time)

```bash
dotnet dev-certs https --trust
```

---

## 🧪 Testing

### Option 1: Terminal (`curl`)

```bash
# 1. Get token for client-a (read scope)
TOKEN=$(curl -k -s -X POST https://localhost:7001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=client-a&client_secret=secret-a&scope=api.read&resource=https://resource-api/" | jq -r .access_token)

# 2. Test GET /api/my-data (expect 200)
curl -H "Authorization: Bearer $TOKEN" http://localhost:5002/api/my-data

# 3. Get token with write scope
TOKEN=$(curl -k -s -X POST https://localhost:7001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=client-a&client_secret=secret-a&scope=api.read%20api.write&resource=https://resource-api/" | jq -r .access_token)

# 4. Test POST /api/my-data (expect 200)
curl -X POST -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
  -d '{"key":"test","value":"test"}' http://localhost:5002/api/my-data

# 5. Test admin endpoint with client-c (expect 200)
TOKEN=$(curl -k -s -X POST https://localhost:7001/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=client-c&client_secret=secret-c&scope=api.read%20api.write%20api.admin&resource=https://resource-api/" | jq -r .access_token)

curl -H "Authorization: Bearer $TOKEN" http://localhost:5002/api/admin/summary
```

### Option 2: VS Code REST Client
Open `.http` files and click **"Send Request"**:
* `OAuthDemo.AuthServer.http` - Token requests
* `OAuthDemo.ResourceApi.http` - API calls

### Option 3: Swagger UI
* **AuthServer:** `https://localhost:7001/swagger`
* **ResourceApi:** `http://localhost:5002/swagger` (click "Authorize" to use OAuth2 flow)

---

## 🔑 Key Technical Details

### Token Flow
1. Client sends `POST /connect/token` with `grant_type=client_credentials`.
2. `AuthServer` validates client credentials and requested scopes.
3. Custom `ClientCredentialsTokenHandler` creates JWT with:
   * `sub` / `client_id`: Client identifier
   * `scope`: Requested scopes (e.g., `api.read`)
   * `aud`: Resource identifier (`https://resource-api/`)
4. `ResourceApi` validates JWT signature, issuer, audience, and scope claims.

### Critical Implementation Details

#### Resource Identifier Format (must match exactly)
```csharp
// AuthServer - RegisterResources
options.RegisterResources("https://resource-api/");

// Scope registration - Resources must match
Resources = { "https://resource-api/" }

// Client permissions - Must match
descriptor.Permissions.Add("rsrc:https://resource-api/");

// Token request - Must match
resource=https://resource-api/

// ResourceApi validation - Must match
options.Audience = "https://resource-api/";
```

#### Claim Destinations (critical for claims to appear in token)
```csharp
// CORRECT - Use SetDestinations extension method
var claim = new Claim(Claims.Scope, scope);
claim.SetDestinations(Destinations.AccessToken);
identity.AddClaim(claim);

// WRONG - Constructor destination parameter doesn't work
var claim = new Claim(Claims.Scope, scope, ClaimValueTypes.String, Destinations.AccessToken);
```

#### Authorization Policies
```csharp
// ResourceApi - Policy-based authorization
options.AddPolicy(Policies.RequireReadScope, policy =>
    policy.RequireAuthenticatedUser().RequireClaim("scope", Scopes.ApiRead));
```

---

## 📚 Learning Outcomes

This project demonstrates:
* ✅ OpenIddict 7.7.1 configuration for Client Credentials flow
* ✅ JWT token issuance with custom claims (`scope`, `aud`)
* ✅ Resource-based scope validation
* ✅ Policy-based authorization in ASP.NET Core
* ✅ Client-specific data isolation
* ✅ Swagger/OpenAPI with OAuth2 Client Credentials flow
* ✅ In-memory EF Core for rapid prototyping
* ✅ Debug handlers for troubleshooting OpenIddict pipeline

---

## 🛠️ Tech Stack

| Component | Version |
| :--- | :--- |
| **.NET** | 10.0 (also `net8.0`) |
| **OpenIddict** | 7.7.1 |
| **ASP.NET Core** | 10.0 |
| **Entity Framework Core** | 10.0 |
| **Database** | In-Memory (demo) |

---

## 📝 Documentation Files

* `trackrun.md` - Batch execution plan (all 3 batches completed)
* `trackissue.md` - Issue tracker with all resolutions
* `trackdev.md` - Development timeline and learnings

---

## 🎓 Key Takeaways

1. **Resource identifier trailing slash matters** - OpenIddict uses `Uri.AbsoluteUri` which adds `/` for root paths.
2. **Claim destinations require `SetDestinations()`** - Not the constructor parameter.
3. **`IgnoreScopePermissions()` is a workaround** - Remove once custom handler works.
4. **Custom token handler** - Needed for Client Credentials flow to include `scope` and `aud` claims.
5. **Audience validation** - Required on `ResourceApi` to prevent token misuse across APIs.
