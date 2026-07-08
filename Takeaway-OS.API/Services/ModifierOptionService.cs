using Microsoft.EntityFrameworkCore;
using Takeaway_OS.API.Data;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.API.Services;

public class ModifierOptionService : IModifierOptionService
{
    private readonly AppDbContext _context;
    public ModifierOptionService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<ModifierOptionDto>> GetAllAsync()
    {
        return await _context.ModifierOptions
            .OrderBy(o => o.Name)
            .Select(o => new ModifierOptionDto
            {
                Id = o.Id,
                ModifierGroupId = o.ModifierGroupId,
                Name = o.Name,
                PriceDelta = o.PriceDelta,
                IsActive = o.IsActive
            })
            .ToListAsync();
    }

    public async Task<ModifierOptionDto?> GetByIdAsync(int id)
    {
        var option = await _context.ModifierOptions.FindAsync(id);
        if (option is null) return null;

        return new ModifierOptionDto
        {
            Id = option.Id,
            ModifierGroupId = option.ModifierGroupId,
            Name = option.Name,
            PriceDelta = option.PriceDelta,
            IsActive = option.IsActive
        };
    }

    public async Task<ModifierOptionDto?> CreateAsync(ModifierOptionCreateDto dto)
    {
        var groupExists = await _context.ModifierGroups.AnyAsync(g => g.Id == dto.ModifierGroupId);
        if (!groupExists) return null;

        var option = new ModifierOption
        {
            ModifierGroupId = dto.ModifierGroupId,
            Name = dto.Name,
            PriceDelta = dto.PriceDelta,
            IsActive = dto.IsActive
        };

        _context.ModifierOptions.Add(option);
        await _context.SaveChangesAsync();

        return new ModifierOptionDto
        {
            Id = option.Id,
            ModifierGroupId = option.ModifierGroupId,
            Name = option.Name,
            PriceDelta = option.PriceDelta,
            IsActive = option.IsActive
        };
    }

    public async Task<bool?> UpdateAsync(int id, ModifierOptionUpdateDto dto)
    {
        var option = await _context.ModifierOptions.FindAsync(id);
        if (option is null) return false;

        var groupExists = await _context.ModifierGroups.AnyAsync(g => g.Id == dto.ModifierGroupId);
        if (!groupExists) return null;

        option.ModifierGroupId = dto.ModifierGroupId;
        option.Name = dto.Name;
        option.PriceDelta = dto.PriceDelta;
        option.IsActive = dto.IsActive;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int id)
    {
        var option = await _context.ModifierOptions.FindAsync(id);
        if (option is null) return false;

        _context.ModifierOptions.Remove(option);
        await _context.SaveChangesAsync();
        return true;
    }
}