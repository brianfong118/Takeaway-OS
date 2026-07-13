using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    // Reading identity off the request is an HTTP concern, so it's resolved here and passed down as int
    //
    // AuthService signs the token with JwtRegisteredClaimNames.Sub ("sub"), 
    // but JwtBearer's default MapInboundClaims=true RENAMES "sub" to ClaimTypes.NameIdentifier on the way in 
    // so the claim that goes out is NOT the claim that comes back. Checking both means this keeps working whether
    // or not that mapping is ever turned off, instead of silently returning null (which would quietly
    // downgrade a logged-in customer's order to a guest order - a bug with no error message).
    private int? GetApplicationUserId()
    {
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub); 
        return int.TryParse(raw, out var id) ? id : null;
    }

    [HttpGet]
    [Authorize(Roles = Roles.Owner)] // the whole-restaurant view; customers get their own scoped list from GET /mine
    public async Task<ActionResult<List<OrderDto>>> GetAll()
    {
        return Ok(await _orderService.GetAllAsync());
    }

    // Sits above GetById's "{id}" route on purpose: "mine" is a literal segment, and if the
    // parameterised route were matched first, "mine" would be parsed as an id and 400 on the int bind.
    [HttpGet("mine")]
    [Authorize(Roles = Roles.Customer)]
    public async Task<ActionResult<List<OrderDto>>> GetMine()
    {
        var applicationUserId = GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized(); // authenticated but no usable subject claim - a malformed token, not a valid empty history

        return Ok(await _orderService.GetForCustomerAsync(applicationUserId.Value));
    }

    [HttpGet("{id}")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    [AllowAnonymous] // guest checkout must stay open; logged-in customers also hit this same endpoint
    public async Task<ActionResult<OrderDto>> Create(OrderCreateDto dto)
    {
        // AllowAnonymous -> UseAuthentication still decodes an Authorization header if one was sent
        var result = await _orderService.CreateAsync(dto, GetApplicationUserId());

        // Checked before the generic failure below: "we're closed" is a 409, not a 400.
        if (result.RestaurantClosed) return Conflict(result.Error);

        if (result.Order is null) return BadRequest(result.Error); // Error is always set when Order is null

        return CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, result.Order);
    }

    // Separate, narrow endpoint for status changes only  
    // matches OrderStatusUpdateDto having no other editable fields (see DTOs/OrderDto.cs).
    [HttpPut("{id}/status")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatusUpdateDto dto)
    {
        var result = await _orderService.UpdateStatusAsync(id, dto);

        return result switch
        {
            OrderStatusUpdateResult.OrderNotFound => NotFound(),
            OrderStatusUpdateResult.InvalidTransition => BadRequest($"Cannot change order {id} to status '{dto.Status}'."),
            _ => NoContent()
        };
    }
}
