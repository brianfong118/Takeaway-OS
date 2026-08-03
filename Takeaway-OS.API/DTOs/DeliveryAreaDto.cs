using System.ComponentModel.DataAnnotations;

namespace Takeaway_OS.API.DTOs;

// Shape returned by GET /api/deliveryareas.
// Id is included even though the customer has no use for it, because the same endpoint feeds
// the Owner's settings card and DELETE needs something to address a row by.
public class DeliveryAreaDto
{
    public int Id { get; set; }
    public string OutwardCode { get; set; } = string.Empty;
}

// Shape accepted by POST /api/deliveryareas (Owner).
public class DeliveryAreaCreateDto
{
    // [Required] catches null/blank here, but the FORMAT check deliberately isn't an
    // attribute, which breaks the pattern used by RestaurantSettingsUpdateDto's [Range].
    //
    // The reason: attributes judge the raw value as typed, and this value has to be
    // normalised (uppercased, whitespace stripped) BEFORE it can be judged - " e1 " is a
    // perfectly good district written badly. A [RegularExpression] here would have to either
    // reject that, or duplicate the pattern in a looser form that then disagrees with
    // UkPostcode about what is valid. So normalise-then-validate happens together in
    // DeliveryAreaService, and this attribute only enforces what is true of the raw string.
    //
    // MaxLength(8) rather than 4 (the longest real outward code) for the same reason: the
    // input hasn't been stripped yet, so it must leave room for the spaces the Owner may
    // have typed. The real length limit is enforced by OutwardPattern after normalising.
    [Required]
    [MaxLength(8)]
    public string OutwardCode { get; set; } = string.Empty;
}
