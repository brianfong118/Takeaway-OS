-- Dev seed for the menu, modifier groups and options. NOT a migration, same reasoning as
-- seed-opening-hours.sql: this is the owner's business data, not schema.
--
-- Purpose is to give the frontend enough shape to actually exercise its edge cases:
--   * several categories, so DisplayOrder ordering is visible
--   * enough items for the menu grid to wrap into multiple columns
--   * a very long description (the 2-line clamp on MenuCard) and an empty one
--   * a disabled item, which must NOT appear on the public menu
--   * a required pick-one group, an optional pick-one group, and a capped multi-select
--   * price deltas that are positive, zero and negative (all three render differently)
--
-- Additive and re-runnable: every insert is guarded by NOT EXISTS, and nothing is ever
-- deleted, so existing rows and any order history referencing them are left alone.
--
-- Run: psql -h localhost -p 5432 -U postgres -d "Takeaway-OS" -f Data/seed-dev-menu.sql

-- Categories -----------------------------------------------------------------------------
-- "Mains" is expected to exist already; it is listed so a fresh database also works.
INSERT INTO "Categories" ("Name", "DisplayOrder")
SELECT v.name, v.display_order
FROM (VALUES
    ('Starters', 0),
    ('Mains',    1),
    ('Sides',    2),
    ('Desserts', 3)
) AS v(name, display_order)
WHERE NOT EXISTS (SELECT 1 FROM "Categories" c WHERE c."Name" = v.name);

-- Menu items -----------------------------------------------------------------------------
-- Category is resolved by name so this does not depend on generated ids.
INSERT INTO "MenuItems" ("CategoryId", "Name", "Description", "Price", "IsAvailable")
SELECT c."Id", v.name, v.description, v.price, v.is_available
FROM (VALUES
    ('Starters', 'Prawn Crackers',      'Light, crisp and salted.',                        2.50, true),
    ('Starters', 'Vegetable Spring Rolls', 'Two rolls filled with shredded cabbage, carrot and glass noodles, wrapped in a thin pastry and fried until the outside shatters when you bite it. Served with a sweet chilli dip.', 4.20, true),
    ('Starters', 'Salt and Pepper Squid', '',                                              6.50, true),
    ('Mains',    'Sweet and Sour Chicken', 'Battered chicken with peppers, pineapple and a tangy sauce.', 9.20, true),
    ('Mains',    'Beef in Black Bean Sauce', 'Sliced beef stir fried with peppers and onions.', 9.80, true),
    ('Mains',    'Kung Pao Chicken',    'Diced chicken, peanuts and dried chillies.',       9.50, true),
    ('Mains',    'Crispy Duck (Half)',  'Served off the bone with pancakes, cucumber and spring onion.', 18.00, false),
    ('Sides',    'Egg Fried Rice',      'Wok fried with egg and spring onion.',             3.80, true),
    ('Sides',    'Salt and Pepper Chips', 'Chips tossed with chilli, garlic and spring onion.', 4.50, true),
    ('Desserts', 'Banana Fritters',     'With syrup and vanilla ice cream.',                4.00, true)
) AS v(category, name, description, price, is_available)
JOIN "Categories" c ON c."Name" = v.category
WHERE NOT EXISTS (SELECT 1 FROM "MenuItems" m WHERE m."Name" = v.name);

-- Modifier groups ------------------------------------------------------------------------
-- Spice Level : IsRequired + Min 1 / Max 1  -> radios, "Required" badge, blocks Add to basket
-- Sauce       : Min 0 / Max 1               -> radios, optional (hence an explicit "No sauce")
-- Extra Toppings : Min 0 / Max 3            -> checkboxes, unpicked options disable at the cap
INSERT INTO "ModifierGroups" ("Name", "MinSelect", "MaxSelect", "IsRequired")
SELECT v.name, v.min_select, v.max_select, v.is_required
FROM (VALUES
    ('Spice Level',    1, 1, true),
    ('Sauce',          0, 1, false),
    ('Extra Toppings', 0, 3, false)
) AS v(name, min_select, max_select, is_required)
WHERE NOT EXISTS (SELECT 1 FROM "ModifierGroups" g WHERE g."Name" = v.name);

-- Modifier options -----------------------------------------------------------------------
-- Deltas deliberately cover all three display cases: positive (+£1.50), zero (renders as
-- nothing) and negative (-£0.50). IsActive = false on one option, which the API filters out.
INSERT INTO "ModifierOptions" ("ModifierGroupId", "Name", "PriceDelta", "IsActive")
SELECT g."Id", v.name, v.price_delta, v.is_active
FROM (VALUES
    ('Spice Level',    'Mild',              0.00, true),
    ('Spice Level',    'Medium',            0.00, true),
    ('Spice Level',    'Hot',               0.00, true),
    ('Sauce',          'No sauce',          0.00, true),
    ('Sauce',          'Curry sauce',       1.20, true),
    ('Sauce',          'Sweet chilli',      1.00, true),
    ('Sauce',          'Satay (seasonal)',  1.40, false),
    ('Extra Toppings', 'Extra chicken',     1.50, true),
    ('Extra Toppings', 'Extra vegetables',  1.00, true),
    ('Extra Toppings', 'Extra noodles',     1.20, true),
    ('Extra Toppings', 'Mushrooms',         0.80, true),
    ('Extra Toppings', 'No onions',         0.00, true),
    ('Extra Toppings', 'Less rice',        -0.50, true)
) AS v(group_name, name, price_delta, is_active)
JOIN "ModifierGroups" g ON g."Name" = v.group_name
WHERE NOT EXISTS (
    SELECT 1 FROM "ModifierOptions" o
    WHERE o."Name" = v.name AND o."ModifierGroupId" = g."Id"
);

-- Links ----------------------------------------------------------------------------------
-- Chicken Chow Mein gets all three groups (the full picker). Others get fewer, and Prawn
-- Crackers gets none, so the no-modifiers path is covered too.
INSERT INTO "MenuItemModifierGroups" ("MenuItemId", "ModifierGroupId")
SELECT m."Id", g."Id"
FROM (VALUES
    ('Chicken Chow Mein',        'Spice Level'),
    ('Chicken Chow Mein',        'Sauce'),
    ('Chicken Chow Mein',        'Extra Toppings'),
    ('Kung Pao Chicken',         'Spice Level'),
    ('Kung Pao Chicken',         'Extra Toppings'),
    ('Sweet and Sour Chicken',   'Extra Toppings'),
    ('Beef in Black Bean Sauce', 'Spice Level'),
    ('Egg Fried Rice',           'Extra Toppings'),
    ('Salt and Pepper Chips',    'Sauce')
) AS v(item_name, group_name)
JOIN "MenuItems" m ON m."Name" = v.item_name
JOIN "ModifierGroups" g ON g."Name" = v.group_name
WHERE NOT EXISTS (
    SELECT 1 FROM "MenuItemModifierGroups" link
    WHERE link."MenuItemId" = m."Id" AND link."ModifierGroupId" = g."Id"
);
