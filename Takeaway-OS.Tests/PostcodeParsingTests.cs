using Takeaway_OS.API.Services;

namespace Takeaway_OS.Tests;

// Pure static functions with no database, so these are plain unit tests with no fixture.
public class PostcodeParsingTests
{
    // [Theory] + [InlineData] rather than six near-identical [Fact]s
    // Each InlineData row runs as its own test case, so a failure names the exact input.
    //
    // These six rows are the six shapes a UK postcode can take
    [Theory]
    [InlineData("E1 6AN", "E1")]        // A9 9AA   - shortest outward code
    [InlineData("M60 1NW", "M60")]      // A99 9AA
    [InlineData("CR2 6XH", "CR2")]      // AA9 9AA
    [InlineData("DN55 1PT", "DN55")]    // AA99 9AA - longest outward code
    [InlineData("W1A 0AX", "W1A")]      // A9A 9AA  - letter in the third position
    [InlineData("EC1A 1BB", "EC1A")]    // AA9A 9AA
    public void Parses_all_six_postcode_shapes(string input, string expectedOutward)
    {
        var parsed = UkPostcode.TryParse(input, out var outwardCode, out _);

        Assert.True(parsed);
        Assert.Equal(expectedOutward, outwardCode);
    }

    [Fact]
    public void E1_and_E14_are_different_districts()
    {
        UkPostcode.TryParse("E1 6AN", out var whitechapel, out _);
        UkPostcode.TryParse("E14 5AB", out var canaryWharf, out _);

        Assert.Equal("E1", whitechapel);
        Assert.Equal("E14", canaryWharf);
        Assert.NotEqual(whitechapel, canaryWharf);
        Assert.StartsWith(whitechapel, canaryWharf);
    }

    // Customers type postcodes untidily
    [Theory]
    [InlineData("e1 6an")]      // lowercase
    [InlineData("E16AN")]       // no space at all
    [InlineData("  E1  6AN  ")] // leading, trailing and doubled internal whitespace
    [InlineData("E1\t6AN")]     // a tab, which Trim() alone would not fix mid-string
    public void Normalises_untidy_input_to_one_result(string input)
    {
        var parsed = UkPostcode.TryParse(input, out var outwardCode, out var formatted);

        Assert.True(parsed);
        Assert.Equal("E1", outwardCode);
        Assert.Equal("E1 6AN", formatted); // the canonical single space is put back for display
    }

    // Unparseable must be false, never a lucky pass. 
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("hello")]
    [InlineData("E1")]          // outward code only - a district, not a deliverable address
    [InlineData("E1 6A")]       // inward code too short
    [InlineData("E1 6ANX")]     // inward code too long
    [InlineData("123 456")]     // no letters at all
    public void Rejects_anything_that_is_not_a_full_postcode(string input)
    {
        var parsed = UkPostcode.TryParse(input, out var outwardCode, out var formatted);

        Assert.False(parsed);
        // The outs are cleared rather than left undefined, so a caller that ignores the bool
        // gets an empty string (which matches no district) rather than a stale value.
        Assert.Equal(string.Empty, outwardCode);
        Assert.Equal(string.Empty, formatted);
    }

    // What the Owner types into the delivery-area list: an outward code on its own.
    [Theory]
    [InlineData("E1", true)]
    [InlineData("EC1A", true)]
    [InlineData("DN55", true)]
    [InlineData("cr2", true)]       // normalised before validating, so lowercase is fine
    [InlineData(" SW1A ", true)]    // as is untidy spacing
    [InlineData("E1 6AN", false)]   // a FULL postcode is not a district - one letterbox, not an area
    [InlineData("", false)]
    [InlineData("LONDON", false)]
    [InlineData("1E", false)]       // digit before letter - not a postcode shape
    public void Validates_outward_codes_for_the_owner_list(string input, bool expected)
    {
        Assert.Equal(expected, UkPostcode.IsValidOutwardCode(input));
    }
}
