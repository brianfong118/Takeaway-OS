using Microsoft.AspNetCore.Mvc;
using Takeaway_OS.API.DTOs;
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
    public async Task<ActionResult<List<OrderDto>>> GetAll()
    {
        return Ok(await _orderService.GetAllAsync());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<OrderDto>> GetById(int id)
    {
        var order = await _orderService.GetByIdAsync(id);
        if (order is null) return NotFound();
        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDto>> Create(OrderCreateDto dto)
    {
        var result = await _orderService.CreateAsync(dto);

        if (result.Order is null) return BadRequest(result.Error); // Error is always set when Order is null

        return CreatedAtAction(nameof(GetById), new { id = result.Order.Id }, result.Order);
    }

    // Separate, narrow endpoint for status changes only  
    // matches OrderStatusUpdateDto having no other editable fields (see DTOs/OrderDto.cs).
    [HttpPut("{id}/status")]
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
