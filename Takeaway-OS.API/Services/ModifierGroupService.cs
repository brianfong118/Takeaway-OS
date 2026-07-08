using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class ModifierGroupService : IModifierGroupService
{
    private readonly AppDbContext _context;
    public ModifierGroupService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ModifierGroupDto>> GetAllAsync()
    {
        return await _context.ModifierGroups
            .Include(g => g.ModifierOptions)
            .OrderBy(g => g.Name)
            .Select(g => new ModifierGroupDto
            {
                Id = g.Id,
                Name = g.Name,
                MinSelect = g.MinSelect,
                MaxSelect = g.MaxSelect,
                IsRequired = g.IsRequired,
                Options = g.ModifierOptions.Select(o => new ModifierOptionDto
                {
                    Id = o.Id,
                    ModifierGroupId = o.ModifierGroupId,
                    Name = o.Name,
                    PriceDelta = o.PriceDelta,
                    IsActive = o.IsActive
                }).ToList()
            })
            .ToListAsync();
    }

    public async Task<ModifierGroupDto?> GetByIdAsync(int id)
    {
        var group = await _context.ModifierGroups
            .Include(g => g.ModifierOptions)
            .FirstOrDefaultAsync(g => g.Id == id);
        // FindAsync can't combine with .Include() — same as MenuItemService

        if (group is null) return null;

        return new ModifierGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MinSelect = group.MinSelect,
            MaxSelect = group.MaxSelect,
            IsRequired = group.IsRequired,
            Options = group.ModifierOptions.Select(o => new ModifierOptionDto
            {
                Id = o.Id,
                ModifierGroupId = o.ModifierGroupId,
                Name = o.Name,
                PriceDelta = o.PriceDelta,
                IsActive = o.IsActive
            }).ToList()
        };
    }

    public async Task<ModifierGroupDto> CreateAsync(ModifierGroupCreateDto dto)
    {
        var group = new ModifierGroup
        {
            Name = dto.Name,
            MinSelect = dto.MinSelect,
            MaxSelect = dto.MaxSelect,
            IsRequired = dto.IsRequired
        };

        _context.ModifierGroups.Add(group);
        await _context.SaveChangesAsync();

        return new ModifierGroupDto
        {
            Id = group.Id,
            Name = group.Name,
            MinSelect = group.MinSelect,
            MaxSelect = group.MaxSelect,
            IsRequired = group.IsRequired,
            Options = new List<ModifierOptionDto>() 
        };
    }

    public async Task<bool> UpdateAsync(int id, ModifierGroupUpdateDto dto)
    {
        var group = await _context.ModifierGroups.FindAsync(id);
        if (group is null) return false;

        group.Name = dto.Name;
        group.MinSelect = dto.MinSelect;
        group.MaxSelect = dto.MaxSelect;
        group.IsRequired = dto.IsRequired;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<DeleteResult> DeleteAsync(int id)
    {
        var group = await _context.ModifierGroups.FindAsync(id);
        if (group is null) return DeleteResult.NotFound;

        _context.ModifierGroups.Remove(group);

        try
        {
            await _context.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // Blocked by the Restrict FK on ModifierOptions and/or MenuItemModifierGroups
            // still pointing at this group (see AppDbContext.OnModelCreating).
            return DeleteResult.HasDependents;
        }

        return DeleteResult.Success;
    }
}