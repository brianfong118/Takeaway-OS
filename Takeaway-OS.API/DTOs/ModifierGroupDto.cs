namespace Takeaway_OS.API.DTOs;

public class ModifierGroupDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public bool IsRequired { get; set; }
    public List<ModifierOptionDto> Options { get; set; } = new();
}

public class ModifierGroupCreateDto
{
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public bool IsRequired { get; set; }
}

public class ModifierGroupUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public int MinSelect { get; set; }
    public int MaxSelect { get; set; }
    public bool IsRequired { get; set; }
}