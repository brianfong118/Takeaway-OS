using System.ComponentModel.DataAnnotations;
using Takeaway_OS.API.DTOs;
using Takeaway_OS.API.Models;

namespace Takeaway_OS.Tests;

// Runs the same DataAnnotations + IValidatableObject checks [ApiController] runs before a controller
// action, but directly, without booting the app. A failure here is a 400 for the real request.
public class DtoValidationTests
{
    // Validator.TryValidateObject is the engine model binding uses. validateAllProperties: true makes
    // it check every attribute (not only [Required]); IValidatableObject.Validate then runs too, but
    // ONLY if every attribute passed, which is also how the real pipeline behaves.
    private static List<ValidationResult> Validate(object dto)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, new ValidationContext(dto), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void A_modifier_group_whose_max_is_below_its_min_is_rejected()
    {
        // No selection count can be both >= 2 and <= 1, so an item using this group could never be ordered.
        var dto = new ModifierGroupCreateDto { Name = "Sauces", MinSelect = 2, MaxSelect = 1 };

        var errors = Validate(dto);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(ModifierGroupCreateDto.MaxSelect)));
    }

    [Fact]
    public void A_modifier_group_allowing_zero_selections_is_rejected()
    {
        var dto = new ModifierGroupUpdateDto { Name = "Sauces", MinSelect = 0, MaxSelect = 0, IsRequired = true };

        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void A_normal_optional_multi_select_group_is_accepted()
    {
        var dto = new ModifierGroupCreateDto { Name = "Add Extras", MinSelect = 0, MaxSelect = 3 };

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void A_menu_item_with_a_negative_price_is_rejected()
    {
        var dto = new MenuItemCreateDto { CategoryId = 1, Name = "Chips", Price = -1.00m };

        Assert.Contains(Validate(dto), e => e.MemberNames.Contains(nameof(MenuItemCreateDto.Price)));
    }

    [Fact]
    public void A_negative_modifier_price_is_rejected()
    {
        var dto = new ModifierOptionCreateDto { ModifierGroupId = 1, Name = "No rice", PriceDelta = -0.50m };

        Assert.NotEmpty(Validate(dto));
    }

    // The other side of the decimal [Range]: an ordinary price must still pass.
    [Fact]
    public void Ordinary_prices_are_accepted()
    {
        Assert.Empty(Validate(new ModifierOptionCreateDto { ModifierGroupId = 1, Name = "Extra chicken", PriceDelta = 1.50m }));
        Assert.Empty(Validate(new MenuItemCreateDto { CategoryId = 1, Name = "Curry", Price = 8.50m }));
        Assert.Empty(Validate(new RestaurantSettingsUpdateDto { DeliveryFee = 2.50m }));
    }

    // The trap this pins down: [Range(0, 100)] on a decimal uses the INT overload, which rounds the
    // value to a whole number first, so -0.40 became 0 and passed. A fee under 50p negative must fail.
    [Fact]
    public void A_small_negative_delivery_fee_is_rejected()
    {
        var dto = new RestaurantSettingsUpdateDto { DeliveryFee = -0.40m };

        Assert.NotEmpty(Validate(dto));
    }

    [Fact]
    public void A_customer_registration_needs_a_name_and_phone()
    {
        var dto = new RegisterRequest { Email = "a@example.test", Password = "Password1!", Role = Roles.Customer };

        var errors = Validate(dto);

        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterRequest.Name)));
        Assert.Contains(errors, e => e.MemberNames.Contains(nameof(RegisterRequest.Phone)));
    }

    [Fact]
    public void The_owner_bootstrap_does_not_need_a_name_or_phone()
    {
        var dto = new RegisterRequest { Email = "owner@example.test", Password = "Password1!", Role = Roles.Owner };

        Assert.Empty(Validate(dto));
    }

    [Fact]
    public void A_registration_without_a_real_email_is_rejected()
    {
        var dto = new RegisterRequest { Email = "bob", Password = "Password1!", Role = Roles.Owner };

        Assert.Contains(Validate(dto), e => e.MemberNames.Contains(nameof(RegisterRequest.Email)));
    }
}
