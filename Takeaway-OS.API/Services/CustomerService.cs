using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class CustomerService : ICustomerService
{
    private readonly AppDbContext _context;

    public CustomerService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CustomerProfileDto?> GetProfileAsync(int applicationUserId)
    { 
        var customer = await _context.Customers
            .AsNoTracking() // read-only, so EF doesn't waste time tracking a row we won't mutate
            .Include(c => c.ApplicationUser) // Email lives on the linked ApplicationUser, avoids a second query
            .FirstOrDefaultAsync(c => c.ApplicationUserId == applicationUserId); // resolve profile from the login, never a passed-in id

        return customer is null ? null : MapToDto(customer);
    }

    public async Task<CustomerProfileDto?> UpdateProfileAsync(int applicationUserId, CustomerProfileUpdateDto dto)
    {
        var customer = await _context.Customers
            .Include(c => c.ApplicationUser)
            .FirstOrDefaultAsync(c => c.ApplicationUserId == applicationUserId);

        if (customer is null) return null;

        customer.Name = dto.Name;
        customer.Phone = dto.Phone;
        await _context.SaveChangesAsync();

        return MapToDto(customer);
    }

    private static CustomerProfileDto MapToDto(Customer customer)
    {
        return new CustomerProfileDto
        {
            Id = customer.Id,
            Name = customer.Name,
            Phone = customer.Phone,
            Email = customer.ApplicationUser.Email ?? string.Empty // Identity's Email is nullable; a saved account always has one
        };
    }
}
