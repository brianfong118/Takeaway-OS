namespace Takeaway_OS.API.Services;

// Shared across any service whose entity has a Restrict-delete FK pointing at it
// (Category <- MenuItems, ModifierGroup <- ModifierOptions/MenuItemModifierGroups).
// A plain bool can't distinguish "not found" (404) from "blocked by dependents" (409),
// same reasoning as LinkResult in IMenuItemModifierGroupService.cs.
public enum DeleteResult
{
    Success,
    NotFound,
    HasDependents
}
