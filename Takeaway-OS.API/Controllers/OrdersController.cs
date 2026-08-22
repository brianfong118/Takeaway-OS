using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Extensions;  // User.GetApplicationUserId() - reads the caller's id off the JWT
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
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized(); // authenticated but no usable subject claim - a malformed token, not a valid empty history

        return Ok(await _orderService.GetForCustomerAsync(applicationUserId.Value));
    }

    // Also a literal segment, so like "mine" it can't be mistaken for the "{id}" route.
    [HttpGet("assigned")]
    [Authorize(Roles = Roles.Driver)]
    public async Task<ActionResult<List<OrderDto>>> GetAssigned()
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        return Ok(await _orderService.GetAssignedAsync(applicationUserId.Value));
    }

    // [AllowAnonymous] is safe HERE and not on GetById because of what's in the URL: an int id is
    // guessable and sequential (order 19 implies order 18), while a v4 GUID is 122 random bits, so
    // there is no set of ids to walk. The token is the credential, exactly like the Stripe webhook's
    // signature standing in for a login.
    //
    // {token:guid} is a route constraint: a malformed token fails to match this route at all and
    // falls through to a 404, so garbage never reaches the database as a query.
    [HttpGet("by-token/{token:guid}")]
    [AllowAnonymous]
    public async Task<ActionResult<GuestOrderDto>> GetByToken(Guid token)
    {
        var order = await _orderService.GetByTokenAsync(token);

        // Bare 404 with no message: a wrong token and a deleted order are indistinguishable to the
        // caller, so nothing here confirms whether a given token was ever valid.
        if (order is null) return NotFound();

        return Ok(order);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = $"{Roles.Owner},{Roles.Customer}")] // interpolated const string - both roles satisfy it
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        OrderDto? order;

        if (User.IsInRole(Roles.Owner))
        {
            order = await _orderService.GetByIdAsync(id);
        }
        else
        {
            var applicationUserId = User.GetApplicationUserId();
            if (applicationUserId is null) return Unauthorized();

            order = await _orderService.GetByIdForCustomerAsync(id, applicationUserId.Value);
        }

        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    [AllowAnonymous] // guest checkout must stay open; logged-in customers also hit this same endpoint
    [EnableRateLimiting(RateLimitPolicies.CreateOrder)]
    public async Task<ActionResult<OrderCreateResponseDto>> Create(OrderCreateDto dto)
    {
        // AllowAnonymous -> UseAuthentication still decodes an Authorization header if one was sent
        var result = await _orderService.CreateAsync(dto, User.GetApplicationUserId());

        // Checked before the generic failure below: "we're closed" is a 409, not a 400.
        if (result.RestaurantClosed) return Conflict(result.Error);

        if (result.Order is null) return BadRequest(result.Error); // Error is always set when Order is null

        // Hand back the order AND the Stripe client secret so the browser can start the payment.
        var response = new OrderCreateResponseDto
        {
            Order = result.Order,
            ClientSecret = result.ClientSecret!, // always set when Order is non-null
            // The only time this is ever sent. A guest can't read their order back without it, and
            // this response is the one moment we know we're talking to the person who placed it.
            PublicToken = result.PublicToken
        };
        return CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, response);
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

    // Narrow endpoint for assigning a driver to a delivery order.
    [HttpPut("{id}/driver")]
    [Authorize(Roles = Roles.Owner)]
    public async Task<IActionResult> AssignDriver(int id, OrderDriverAssignmentDto dto)
    {
        var result = await _orderService.AssignDriverAsync(id, dto);

        return result switch
        {
            DriverAssignmentResult.OrderNotFound => NotFound(),
            DriverAssignmentResult.DriverNotFound => BadRequest($"Driver {dto.DriverId} does not exist."),
            DriverAssignmentResult.NotDeliveryOrder => BadRequest($"Order {id} is not a delivery order, so it can't have a driver."),
            _ => NoContent()
        };
    }

    // Driver-only: advance one of the driver's OWN orders along the delivery tail (Ready -> OutForDelivery -> Completed).
    // Separate from UpdateStatus (Owner) on purpose: different role, different allowed transitions
    [HttpPut("{id}/delivery-status")]
    [Authorize(Roles = Roles.Driver)]
    public async Task<IActionResult> UpdateDeliveryStatus(int id, OrderStatusUpdateDto dto)
    {
        var applicationUserId = User.GetApplicationUserId();
        if (applicationUserId is null) return Unauthorized();

        var result = await _orderService.UpdateStatusByDriverAsync(id, applicationUserId.Value, dto);

        return result switch
        {
            // Not-theirs and not-existent both land here (the service collapses them) -> a driver can't enumerate orders.
            OrderStatusUpdateResult.OrderNotFound => NotFound(),
            OrderStatusUpdateResult.InvalidTransition => BadRequest($"A driver can't change order {id} to status '{dto.Status}'."),
            _ => NoContent()
        };
    }
}
