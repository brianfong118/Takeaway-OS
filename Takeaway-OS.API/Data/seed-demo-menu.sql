-- Demo menu: categories, items, modifier groups and options for the deployed portfolio demo.
--
-- Distinct from seed-dev-menu.sql, which stays as it is. That one exists to exercise edge cases
-- (an empty description, a deliberately over-long one, every sign of price delta) and looks odd to
-- a visitor. This one is written to be READ: realistic dishes, realistic prices, and allergen text
-- on every line, because the menu is the first page anyone lands on and the one most likely to end
-- up in a screenshot.
--
-- Run: psql -h localhost -p 5432 -U postgres -d "Takeaway-OS" -f Data/seed-demo-menu.sql
--
-- DESTRUCTIVE and re-runnable, unlike the dev seed's NOT EXISTS guards. It clears the five menu
-- tables and rebuilds them, which is what makes it usable as the "put the demo back" script after
-- a visitor has edited something through the owner UI.
--
-- Safe to delete menu rows even when orders exist: OrderItems snapshots ItemName and UnitPrice and
-- holds NO foreign key to MenuItems (the whole point of the price-snapshotting decision), so past
-- orders keep their names and prices with nothing to cascade.
--
-- Delete order is child-to-parent, since the FKs point upward:
--   MenuItemModifierGroups -> MenuItems + ModifierGroups
--   ModifierOptions        -> ModifierGroups
--   MenuItems              -> Categories
DELETE FROM "MenuItemModifierGroups";
DELETE FROM "ModifierOptions";
DELETE FROM "ModifierGroups";
DELETE FROM "MenuItems";
DELETE FROM "Categories";

-- Categories -------------------------------------------------------------------------------
-- DisplayOrder drives the order sections appear in on the menu page; it is not the Id, so the
-- owner can reorder without the rows moving.
INSERT INTO "Categories" ("Name", "DisplayOrder") VALUES
    ('Starters',                1),
    ('Chicken',                 2),
    ('Beef and Pork',           3),
    ('Seafood',                 4),
    ('Rice, Noodles and Chips', 5),
    ('Drinks',                  6);

-- Menu items -------------------------------------------------------------------------------
-- CategoryId comes from a subquery on the name rather than a hardcoded number, because the
-- DELETE above does not reset the identity sequence: a re-run gets higher ids every time, so any
-- literal id here would be wrong the second time this file is used.
--
-- On the allergen text, which is the part that actually matters:
--   * It names the specific allergens present as INGREDIENTS. The kitchen cross-contamination
--     notice is a separate, general statement rendered once at the top of the menu page.
--   * Peanuts and tree nuts are written as separate words, and the specific nut is named. They
--     are two distinct entries in the regulated 14, and treating them as one is the most common
--     mistake in hand-written allergen text.
--   * "Contains no allergenic ingredients" is deliberately never written as "allergen free" -
--     the shared-kitchen notice on the menu page is what qualifies it, and no dish out of this
--     kitchen could honestly claim the stronger phrase.
INSERT INTO "MenuItems" ("CategoryId", "Name", "Description", "Price", "IsAvailable") VALUES
    -- Starters
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Starters'),
     'Prawn Crackers',
     'Light, crisp crackers served warm. Contains crustaceans (prawn) and fish.',
     2.20, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Starters'),
     'Vegetable Spring Rolls',
     'Two rolls of shredded cabbage, carrot and beansprouts in crisp pastry. Contains gluten (wheat), soya and celery.',
     3.80, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Starters'),
     'Sesame Prawn Toast',
     'Four pieces of minced prawn on toast, coated in sesame seeds and fried. Contains crustaceans (prawn), gluten (wheat), sesame, eggs and soya.',
     5.90, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Starters'),
     'Chicken Satay Skewers',
     'Three grilled skewers with a peanut satay sauce. Contains peanuts, soya and gluten (wheat).',
     6.40, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Starters'),
     'Salt and Chilli Chicken Wings',
     'Wings tossed with chilli, garlic and spring onion. Contains gluten (wheat) and soya.',
     6.50, true),

    -- Chicken
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Chicken'),
     'Chicken Curry',
     'Chicken in a mild curry sauce with onion and peppers. Contains gluten (wheat), soya and celery.',
     9.20, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Chicken'),
     'Sweet and Sour Chicken',
     'Battered chicken in a Hong Kong style sweet and sour sauce. Contains gluten (wheat), eggs and soya.',
     9.80, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Chicken'),
     'Chicken in Black Bean Sauce',
     'Chicken with onion and peppers in a fermented black bean sauce. Contains gluten (wheat), soya and sesame.',
     9.60, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Chicken'),
     'Kung Po Chicken',
     'Chicken with peppers, water chestnuts and cashew nuts in a sweet chilli sauce. Contains tree nuts (cashew), gluten (wheat), soya and sesame.',
     9.90, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Chicken'),
     'Salt and Chilli Chicken',
     'Crisp chicken with chilli, garlic, onion and peppers. Contains gluten (wheat) and soya.',
     9.90, true),

    -- Beef and Pork
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Beef and Pork'),
     'Beef in Oyster Sauce',
     'Sliced beef with onion and mushroom in oyster sauce. Contains molluscs (oyster), gluten (wheat) and soya.',
     10.40, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Beef and Pork'),
     'Crispy Shredded Chilli Beef',
     'Shredded beef fried crisp and tossed in a sweet chilli glaze. Contains gluten (wheat), soya, eggs and sesame.',
     10.80, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Beef and Pork'),
     'Sweet and Sour Pork Balls',
     'Battered pork with a sweet and sour dipping sauce. Contains gluten (wheat), eggs and soya.',
     9.80, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Beef and Pork'),
     'Barbecue Spare Ribs',
     'Pork ribs in a sticky barbecue sauce. Contains gluten (wheat), soya and sesame.',
     10.20, true),

    -- Seafood
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Seafood'),
     'King Prawn Curry',
     'King prawns in a mild curry sauce with onion and peppers. Contains crustaceans (prawn), gluten (wheat), soya and celery.',
     11.40, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Seafood'),
     'Salt and Chilli King Prawns',
     'King prawns with chilli, garlic, onion and peppers. Contains crustaceans (prawn), gluten (wheat) and soya.',
     11.80, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Seafood'),
     'King Prawn in Black Bean Sauce',
     'King prawns with peppers in a fermented black bean sauce. Contains crustaceans (prawn), gluten (wheat), soya and sesame.',
     11.60, true),

    -- The one unavailable item. Soft delete, not deletion: IsAvailable = false hides it from the
    -- public menu while the owner admin view still lists it, so it can be switched back on later
    -- without losing its description or price. Worth having exactly one in the demo, because that
    -- difference between the two views is invisible if every item is available.
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Seafood'),
     'Salt and Chilli Squid',
     'Squid rings with chilli, garlic and spring onion. Contains molluscs (squid), gluten (wheat) and soya.',
     11.20, false),

    -- Rice, Noodles and Chips
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Rice, Noodles and Chips'),
     'Boiled Rice',
     'Plain steamed long grain rice. Contains no allergenic ingredients.',
     3.20, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Rice, Noodles and Chips'),
     'Egg Fried Rice',
     'Rice fried with egg and spring onion. Contains eggs and soya.',
     3.90, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Rice, Noodles and Chips'),
     'Chow Mein Noodles',
     'Soft egg noodles with beansprouts and spring onion. Contains gluten (wheat), eggs, soya and sesame.',
     4.60, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Rice, Noodles and Chips'),
     'Chips',
     'Thick cut chips. Contains no allergenic ingredients.',
     3.40, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Rice, Noodles and Chips'),
     'Curry Chips',
     'Chips with curry sauce. Contains gluten (wheat), soya and celery.',
     4.60, true),

    -- Drinks
    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Drinks'),
     'Coca-Cola',
     '330ml can. Contains no allergenic ingredients.',
     1.60, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Drinks'),
     'Diet Coke',
     '330ml can. Contains no allergenic ingredients.',
     1.60, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Drinks'),
     'Irn-Bru',
     '330ml can. Contains no allergenic ingredients.',
     1.60, true),

    ((SELECT "Id" FROM "Categories" WHERE "Name" = 'Drinks'),
     'Still Water',
     '500ml bottle. Contains no allergenic ingredients.',
     1.40, true);

-- Modifier groups --------------------------------------------------------------------------
-- Groups are reusable and NOT owned by a menu item, which is why "Spice Level" can sit on a
-- curry, a wings starter and a plate of curry chips without being defined three times.
--
-- MinSelect/MaxSelect/IsRequired between them describe the four shapes the UI has to handle:
--   1/1 required  -> a choice that must be made (radio buttons, no default)
--   0/1 optional  -> pick one or none
--   0/n optional  -> a capped multi-select
INSERT INTO "ModifierGroups" ("Name", "MinSelect", "MaxSelect", "IsRequired") VALUES
    ('Spice Level', 1, 1, true),
    ('Portion',     1, 1, true),
    ('Add Extras',  0, 3, false),
    ('Leave Out',   0, 4, false),
    ('Dips',        0, 1, false);

-- Modifier options -------------------------------------------------------------------------
-- PriceDelta is decimal and may be zero: "Leave Out" exists entirely to send instructions to the
-- kitchen, and charging for removing an onion would be strange. Zero-delta options are the reason
-- the schema stores a delta per option rather than assuming every option costs something.
--
-- Note what deltas do NOT do: OrderItemModifier has no quantity column, so a selected option is
-- one row however many portions the line covers. Two portions with Extra Chicken adds 1.50 once,
-- not twice. That is a deliberate commercial call recorded in CLAUDE.md, not an oversight here.
INSERT INTO "ModifierOptions" ("ModifierGroupId", "Name", "PriceDelta", "IsActive") VALUES
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Spice Level'), 'Mild',        0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Spice Level'), 'Medium',      0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Spice Level'), 'Hot',         0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Spice Level'), 'Extra Hot',   0.00, true),

    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Portion'), 'Regular',         0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Portion'), 'Large',           2.50, true),

    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Add Extras'), 'Extra Chicken',     1.50, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Add Extras'), 'Extra King Prawns', 2.50, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Add Extras'), 'Extra Vegetables',  1.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Add Extras'), 'Extra Sauce',       0.80, true),

    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Leave Out'), 'No Onions',          0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Leave Out'), 'No Peppers',         0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Leave Out'), 'No Mushrooms',       0.00, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Leave Out'), 'No Water Chestnuts', 0.00, true),

    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Dips'), 'Sweet and Sour Dip', 0.60, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Dips'), 'Curry Dip',          0.60, true),
    ((SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Dips'), 'Garlic Mayo Dip',    0.60, true);

-- Which groups apply to which items ---------------------------------------------------------
-- The join table is what makes groups reusable. Built with SELECT ... WHERE IN rather than one
-- row per pair, so adding a dish to a group is editing a list of names rather than writing
-- another INSERT and looking up two ids by hand.

-- Dips: the starters that come with something to dip in.
INSERT INTO "MenuItemModifierGroups" ("MenuItemId", "ModifierGroupId")
SELECT mi."Id", (SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Dips')
FROM "MenuItems" mi
WHERE mi."Name" IN (
    'Vegetable Spring Rolls',
    'Sesame Prawn Toast',
    'Chicken Satay Skewers',
    'Salt and Chilli Chicken Wings'
);

-- Spice Level: everything with chilli or curry in it, across three different categories - which
-- is the case a reusable group exists for.
INSERT INTO "MenuItemModifierGroups" ("MenuItemId", "ModifierGroupId")
SELECT mi."Id", (SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Spice Level')
FROM "MenuItems" mi
WHERE mi."Name" IN (
    'Salt and Chilli Chicken Wings',
    'Chicken Curry',
    'Kung Po Chicken',
    'Salt and Chilli Chicken',
    'Crispy Shredded Chilli Beef',
    'King Prawn Curry',
    'Salt and Chilli King Prawns',
    'Salt and Chilli Squid',
    'Curry Chips'
);

-- Portion, Add Extras and Leave Out: the main dishes, i.e. everything in the three main-course
-- categories. Selected by category so a new dish added later inherits nothing silently - it is a
-- one-off list, not a rule the app enforces.
INSERT INTO "MenuItemModifierGroups" ("MenuItemId", "ModifierGroupId")
SELECT mi."Id", mg."Id"
FROM "MenuItems" mi
CROSS JOIN "ModifierGroups" mg
WHERE mi."CategoryId" IN (
        SELECT "Id" FROM "Categories" WHERE "Name" IN ('Chicken', 'Beef and Pork', 'Seafood')
      )
  AND mg."Name" IN ('Portion', 'Add Extras', 'Leave Out');

-- Leave Out on its own for the noodles, which have onion and beansprouts in them.
INSERT INTO "MenuItemModifierGroups" ("MenuItemId", "ModifierGroupId")
SELECT mi."Id", (SELECT "Id" FROM "ModifierGroups" WHERE "Name" = 'Leave Out')
FROM "MenuItems" mi
WHERE mi."Name" = 'Chow Mein Noodles';

-- Check what landed: one row per category with its item count, and the modifier groups per item.
SELECT c."DisplayOrder", c."Name" AS category, COUNT(mi."Id") AS items
FROM "Categories" c
LEFT JOIN "MenuItems" mi ON mi."CategoryId" = c."Id"
GROUP BY c."DisplayOrder", c."Name"
ORDER BY c."DisplayOrder";
