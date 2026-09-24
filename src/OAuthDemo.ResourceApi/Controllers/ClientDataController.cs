using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OAuthDemo.ResourceApi.Data;
using OAuthDemo.ResourceApi.Models;
using OAuthDemo.Shared.Constants;

namespace OAuthDemo.ResourceApi.Controllers;

[ApiController]
[Route("api/my-data")]
[Authorize(Policy = Policies.RequireReadScope)]
public class ClientDataController : ControllerBase
{
    private readonly ResourceDbContext _context;

    public ClientDataController(ResourceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all data for the authenticated client (based on client_id claim in token)
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<ClientData>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ClientData>>> GetMyData()
    {
        var clientId = GetClientIdFromToken();
        if (string.IsNullOrEmpty(clientId))
        {
            return Forbid("Client ID not found in token");
        }

        var data = await _context.ClientData
            .Where(d => d.ClientId == clientId)
            .OrderBy(d => d.Key)
            .ToListAsync();

        return Ok(data);
    }

    /// <summary>
    /// Get a specific data item for the authenticated client
    /// </summary>
    [HttpGet("{key}")]
    [ProducesResponseType(typeof(ClientData), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientData>> GetMyDataItem(string key)
    {
        var clientId = GetClientIdFromToken();
        if (string.IsNullOrEmpty(clientId))
        {
            return Forbid("Client ID not found in token");
        }

        var data = await _context.ClientData
            .FirstOrDefaultAsync(d => d.ClientId == clientId && d.Key == key);

        if (data == null)
        {
            return NotFound($"Data with key '{key}' not found for client '{clientId}'");
        }

        return Ok(data);
    }

    /// <summary>
    /// Create or update data for the authenticated client (requires api.write scope)
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireWriteScope)]
    [ProducesResponseType(typeof(ClientData), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ClientData), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<ClientData>> CreateOrUpdateMyData([FromBody] ClientDataRequest request)
    {
        var clientId = GetClientIdFromToken();
        if (string.IsNullOrEmpty(clientId))
        {
            return Forbid("Client ID not found in token");
        }

        var existingData = await _context.ClientData
            .FirstOrDefaultAsync(d => d.ClientId == clientId && d.Key == request.Key);

        if (existingData != null)
        {
            existingData.Value = request.Value;
            existingData.CreatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(existingData);
        }

        var newData = new ClientData
        {
            ClientId = clientId,
            Key = request.Key,
            Value = request.Value,
            CreatedAt = DateTime.UtcNow
        };

        _context.ClientData.Add(newData);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyDataItem), new { key = request.Key }, newData);
    }

    /// <summary>
    /// Delete data for the authenticated client (requires api.write scope)
    /// </summary>
    [HttpDelete("{key}")]
    [Authorize(Policy = Policies.RequireWriteScope)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteMyData(string key)
    {
        var clientId = GetClientIdFromToken();
        if (string.IsNullOrEmpty(clientId))
        {
            return Forbid("Client ID not found in token");
        }

        var data = await _context.ClientData
            .FirstOrDefaultAsync(d => d.ClientId == clientId && d.Key == key);

        if (data == null)
        {
            return NotFound($"Data with key '{key}' not found for client '{clientId}'");
        }

        _context.ClientData.Remove(data);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private string? GetClientIdFromToken()
    {
        // In Client Credentials flow, the client_id is in the "client_id" claim
        // or "sub" claim depending on configuration
        return User.FindFirst("client_id")?.Value 
            ?? User.FindFirst("sub")?.Value
            ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
    }
}

public record ClientDataRequest(string Key, string Value);