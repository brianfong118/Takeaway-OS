using Takeaway_OS.API.Models;
using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Unit tests for OrderService.FormatAddress -> turns a saved Address into the single delivery line the order snapshots. 
// blank parts (e.g. no Line2) are dropped, never rendered as an empty ", ," gap. 
// Label is intentionally excluded (it's the customer's nickname, not postal data).
public class AddressFormattingTests
{
    [Fact]
    public void Full_address_joins_all_parts_in_order()
    {
        var address = new Address { Label = "Home", Line1 = "12 High St", Line2 = "Flat 2", City = "Leeds", Postcode = "LS1 1AA" };

        var line = OrderService.FormatAddress(address);

        Assert.Equal("12 High St, Flat 2, Leeds, LS1 1AA", line); // note: no "Home" — the label is left out
    }

    [Fact]
    public void Missing_Line2_produces_no_empty_gap()
    {
        var address = new Address { Line1 = "12 High St", Line2 = "", City = "Leeds", Postcode = "LS1 1AA" };

        var line = OrderService.FormatAddress(address);

        Assert.Equal("12 High St, Leeds, LS1 1AA", line); // the empty Line2 is dropped, not rendered as ", ,"
    }

    [Fact]
    public void Whitespace_only_parts_are_treated_as_empty()
    {
        var address = new Address { Line1 = "12 High St", Line2 = "   ", City = "Leeds", Postcode = "LS1 1AA" };

        var line = OrderService.FormatAddress(address);

        Assert.Equal("12 High St, Leeds, LS1 1AA", line); // whitespace-only counts as blank (IsNullOrWhiteSpace)
    }
}
