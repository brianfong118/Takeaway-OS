namespace Takeaway_OS.API.DTOs;

public class DriverDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}

// No DriverCreateDto - Drivers are created via POST /api/auth/register (role: "Driver")
// see RegisterRequest in AuthDto.cs.

public class DriverUpdateDto
{
    public string Name { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public bool IsAvailable { get; set; }
}