using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OAuthDemo.ResourceApi.Data;
using OAuthDemo.ResourceApi.Models;
using OAuthDemo.Shared.Constants;

namespace OAuthDemo.ResourceApi.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Policy = Policies.RequireAdminScope)]
public class AdminController : ControllerBase
{
    private readonly ResourceDbContext _context;

    public AdminController(ResourceDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get all data from all clients (requires api.admin scope)
    /// </summary>
    [HttpGet("all-data")]
    [ProducesResponseType(typeof(IEnumerable<ClientData>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<ClientData>>> GetAllData()
    {
        var data = await _context.ClientData
            .OrderBy(d => d.ClientId)
            .ThenBy(d => d.Key)
            .ToListAsync();

        return Ok(data);
    }

    /// <summary>
    /// Get data for a specific client (requires api.admin scope)
    /// </summary>
    [HttpGet("client/{clientId}")]
    [ProducesResponseType(typeof(IEnumerable<ClientData>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IEnumerable<ClientData>>> GetClientData(string clientId)
    {
        var data = await _context.ClientData
            .Where(d => d.ClientId == clientId)
            .OrderBy(d => d.Key)
            .ToListAsync();

        if (!data.Any())
        {
            return NotFound($"No data found for client '{clientId}'");
        }

        return Ok(data);
    }

    /// <summary>
    /// Get summary of all clients and their data counts (requires api.admin scope)
    /// </summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(AdminSummary), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<AdminSummary>> GetSummary()
    {
        var summary = await _context.ClientData
            .GroupBy(d => d.ClientId)
            .Select(g => new ClientSummary
            {
                ClientId = g.Key,
                DataCount = g.Count(),
                Keys = g.Select(d => d.Key).ToList()
            })
            .ToListAsync();

        return Ok(new AdminSummary
        {
            TotalClients = summary.Count,
            TotalDataItems = summary.Sum(s => s.DataCount),
            Clients = summary
        });
    }
}

public record AdminSummary
{
    public int TotalClients { get; set; }
    public int TotalDataItems { get; set; }
    public List<ClientSummary> Clients { get; set; } = new();
}

public record ClientSummary
{
    public string ClientId { get; set; } = string.Empty;
    public int DataCount { get; set; }
    public List<string> Keys { get; set; } = new();
}