using System.Text.RegularExpressions;

namespace Takeaway_OS.API.Services;

// Parsing/normalising UK postcodes, kept as pure static functions rather than an injected
// service: there is no state and no database, so there is nothing to fake in a test and
// nothing to register in Program.cs. DeliveryAreaService depends on this; this depends on
// nothing.
public static class UkPostcode
{
    // Compiled: these two run on the hot path of every delivery order, and the patterns are
    // fixed at startup, so paying the compile cost once beats re-interpreting per call.
    private static readonly Regex FullPattern =
        new(@"^[A-Z]{1,2}\d[A-Z\d]?\d[A-Z]{2}$", RegexOptions.Compiled);

    // The same expression with the inward half removed - what an Owner types into the
    // delivery-area list.
    private static readonly Regex OutwardPattern =
        new(@"^[A-Z]{1,2}\d[A-Z\d]?$", RegexOptions.Compiled);

    // Uppercase + every whitespace character removed, NOT just Trim().
    // Customers type "e1 6an", "E1  6AN" and " E16AN " and all three mean one postcode; the
    // stored form has to be identical for all of them or the unique index on
    // DeliveryArea.OutwardCode stops being able to catch duplicates.
    // Removing the internal space is also what makes "last three characters" a safe split -
    // it means the code never has to trust the customer to have put the space in the right
    // place, or to have put one in at all.
    public static string Normalise(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return string.Empty;

        // char.IsWhiteSpace rather than Replace(" ", "") so tabs and non-breaking spaces
        // pasted in from a website are handled too.
        return new string(raw.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    }

    // Validates a full postcode and splits it. Returns false rather than throwing, because
    // "the customer typed something that isn't a postcode" is an expected outcome on a public
    // checkout form, not an exceptional one.
    //
    // outwardCode: what the delivery-area check compares - "E1".
    // formatted:   the display form with the single canonical space back in - "E1 6AN".
    //              Snapshotted onto Order.DeliveryPostcode, so the Owner's order list shows a
    //              postcode that reads normally rather than the squashed "E16AN".
    public static bool TryParse(string raw, out string outwardCode, out string formatted)
    {
        outwardCode = string.Empty;
        formatted = string.Empty;

        var normalised = Normalise(raw);
        if (!FullPattern.IsMatch(normalised)) return false;

        // The universal split described at the top of the class. Safe without a length check
        // because FullPattern already guarantees at least five characters.
        outwardCode = normalised[..^3];
        var inward = normalised[^3..];
        formatted = $"{outwardCode} {inward}";
        return true;
    }

    // Whether a string is a well-formed outward code on its own - the Owner adding a district.
    // Takes the raw input and normalises internally so the caller can't forget to.
    public static bool IsValidOutwardCode(string raw) => OutwardPattern.IsMatch(Normalise(raw));
}
