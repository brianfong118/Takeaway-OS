namespace Takeaway_OS.API.Models;

public enum OrderStatus
{
    Pending,
    Paid,
    Preparing,
    Ready,
    OutForDelivery,
    Completed,
    Cancelled
}