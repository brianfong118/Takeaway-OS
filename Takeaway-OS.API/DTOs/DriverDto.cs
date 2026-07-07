namespace Takeaway_OS.API.DTOs;

public class DriverDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}

public class DriverCreateDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; } = true;
}

public class DriverUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}