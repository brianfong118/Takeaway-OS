-- Demo orders: a plausible evening's trading, spread across every status.
--
-- Why this file exists at all: the owner dashboard and the driver dashboard are the two screens
-- that separate this project from a CRUD tutorial, and both render nothing without orders in them.
-- A freshly seeded demo shows an empty board under every tab, which is the least representative
-- possible view of the thing being demonstrated.
--
-- The spread is chosen for what it puts on screen, not for realism alone:
--   Pending 1, Paid 2, Preparing 3, Ready 2, OutForDelivery 2, Completed 4, Cancelled 1
-- Every owner tab has content, the driver dashboard has live deliveries, and the account history
-- of the demo customer has past orders in it.
--
-- PREREQUISITES
--   * seed-demo-menu.sql, for item names and prices that match the live menu.
--   * seed-demo-settings.sql, for the 2.50 delivery fee this file snapshots.
--   * Optionally a Customer registered as demo.customer@example.com, and at least one Driver.
--     Both are looked up at the end and both degrade gracefully: no customer means those orders
--     stay guest orders, no driver means the deliveries are unassigned. Neither is an error, and
--     nothing here fails if you have not created them yet.
--
-- Run: psql -h localhost -p 5432 -U postgres -d "Takeaway-OS" -f Data/seed-demo-orders.sql
-- DESTRUCTIVE: clears ALL orders first, so it is the reset script for a demo that has filled up
-- with whatever visitors typed into the checkout. That deletion is what the privacy policy's
-- "the demo database is cleared periodically" line is promising.

-- Child-to-parent again: modifiers hang off items, items hang off orders.
DELETE FROM "OrderItemModifiers";
DELETE FROM "OrderItems";
DELETE FROM "Orders";

-- Orders ---------------------------------------------------------------------------------------
-- PublicToken is normally a random v4 GUID generated on the entity, and it is a CAPABILITY: anyone
-- holding it can read that order. The literal tokens below are deliberately predictable, which is
-- acceptable here and ONLY here, because every row is invented - there is no real name, phone or
-- address to leak. It buys two things: the items further down can be keyed to their order by
-- token, and the demo's order-tracking links stay stable across a reset, so a link in a README or
-- a bookmark keeps working. Orders placed through the app still get gen_random_uuid().
--
-- Phone numbers are Ofcom's reserved "drama" range for the 028 area (028 9018 0xxx). They are
-- allocated specifically so film and television can show a number that cannot ring a real person.
-- The same care as taking the real phone number out of the footer: fake-looking is not enough,
-- because plausible-looking numbers belong to somebody.
--
-- CreatedAt is relative to now(), so the dashboard's "waiting N minutes" reads sensibly whenever
-- this is run, and the recent orders genuinely look recent. Note the known limitation: with no
-- PaidAt column, that figure is measured from creation, so it reads high on anything older.
--
-- StripePaymentIntentId is left NULL on every row, including the paid ones. These orders never
-- went through Stripe, and inventing a pi_... id would put a reference in the database that
-- matches nothing in any Stripe account. NULL is the honest value and nothing reads it except the
-- webhook, which will never be handed one of these.
INSERT INTO "Orders"
    ("PublicToken", "CustomerName", "CustomerPhone", "DeliveryAddress", "DeliveryPostcode",
     "OrderType", "DeliveryFee", "Status", "Notes", "CreatedAt")
VALUES
    -- Completed, days old. This is the demo customer's history.
    -- NOTE THE FEE: 2.00, not the current 2.50. Orders snapshot the delivery fee at creation for
    -- exactly the same reason OrderItems snapshot the unit price, and a row that predates a price
    -- rise is the only way to SHOW that rather than assert it.
    ('00000000-0000-4000-8000-000000000001', 'Sam Carter', '028 9018 0101',
     '14 Comber Road, Dundonald, Belfast, BT16 2AA', 'BT16 2AA',
     'Delivery', 2.00, 'Completed', '', now() - interval '5 days'),

    ('00000000-0000-4000-8000-000000000002', 'Sam Carter', '028 9018 0101',
     '', '',
     'Collection', 0.00, 'Completed', '', now() - interval '2 days'),

    ('00000000-0000-4000-8000-000000000004', 'Laura Bennett', '028 9018 0104',
     '3 Church Road, Dundonald, Belfast, BT16 2LN', 'BT16 2LN',
     'Delivery', 2.50, 'Completed', 'Please ring the doorbell rather than knock.', now() - interval '3 hours'),

    ('00000000-0000-4000-8000-000000000005', 'Michael Doyle', '028 9018 0105',
     '', '',
     'Collection', 0.00, 'Completed', '', now() - interval '2 hours'),

    -- Cancelled. Only reachable from Pending or Paid, so this one was cancelled before the kitchen
    -- started; anything past Preparing would need a staff override that does not exist yet.
    ('00000000-0000-4000-8000-000000000015', 'Niamh Doherty', '028 9018 0115',
     '9 Ballyregan Road, Dundonald, Belfast, BT16 1HX', 'BT16 1HX',
     'Delivery', 2.50, 'Cancelled', '', now() - interval '40 minutes'),

    -- Out for delivery: the driver dashboard's live jobs.
    ('00000000-0000-4000-8000-000000000003', 'Sam Carter', '028 9018 0101',
     '14 Comber Road, Dundonald, Belfast, BT16 2AA', 'BT16 2AA',
     'Delivery', 2.50, 'OutForDelivery', '', now() - interval '25 minutes'),

    ('00000000-0000-4000-8000-000000000006', 'Priya Nair', '028 9018 0106',
     '27 Old Dundonald Road, Belfast, BT16 1XT', 'BT16 1XT',
     'Delivery', 2.50, 'OutForDelivery', 'Flat 2, buzzer is second from the top.', now() - interval '18 minutes'),

    -- Ready: cooked, waiting. One delivery awaiting a driver, one collection awaiting a customer.
    ('00000000-0000-4000-8000-000000000007', 'Tom Hughes', '028 9018 0107',
     '41 Millreagh Avenue, Dundonald, Belfast, BT16 2GR', 'BT16 2GR',
     'Delivery', 2.50, 'Ready', '', now() - interval '12 minutes'),

    ('00000000-0000-4000-8000-000000000008', 'Grace Millar', '028 9018 0108',
     '', '',
     'Collection', 0.00, 'Ready', 'Collecting at 7.30 if that is alright.', now() - interval '10 minutes'),

    -- Preparing: the kitchen is working on these. The busiest tab, as it should be at service.
    ('00000000-0000-4000-8000-000000000009', 'Daniel Okafor', '028 9018 0109',
     '6 Enler Park, Dundonald, Belfast, BT16 2HN', 'BT16 2HN',
     'Delivery', 2.50, 'Preparing', '', now() - interval '8 minutes'),

    ('00000000-0000-4000-8000-000000000010', 'Aoife Brennan', '028 9018 0110',
     '', '',
     'Collection', 0.00, 'Preparing', 'No cutlery needed, thanks.', now() - interval '6 minutes'),

    ('00000000-0000-4000-8000-000000000011', 'Ruth Alexander', '028 9018 0111',
     '22 Kings Road, Belfast, BT16 1TR', 'BT16 1TR',
     'Delivery', 2.50, 'Preparing', '', now() - interval '5 minutes'),

    -- Paid: Stripe has confirmed, the kitchen has not picked them up yet.
    ('00000000-0000-4000-8000-000000000012', 'Chris Wong', '028 9018 0112',
     '5 Grahams Bridge Road, Dundonald, Belfast, BT16 2DE', 'BT16 2DE',
     'Delivery', 2.50, 'Paid', '', now() - interval '3 minutes'),

    ('00000000-0000-4000-8000-000000000013', 'Hannah Reid', '028 9018 0113',
     '', '',
     'Collection', 0.00, 'Paid', '', now() - interval '2 minutes'),

    -- Pending: placed, never paid for. An abandoned checkout looks exactly like this, and it is a
    -- normal resting state rather than a fault - worth having one so the board is not all success.
    ('00000000-0000-4000-8000-000000000014', 'Owen Bell', '028 9018 0114',
     '18 Church View, Dundonald, Belfast, BT16 2LT', 'BT16 2LT',
     'Delivery', 2.50, 'Pending', '', now() - interval '1 minute');

-- Order items ----------------------------------------------------------------------------------
-- Names and prices are COPIES, not references: OrderItems has no foreign key to MenuItems, which
-- is what lets the menu be re-seeded without touching order history. They are typed to match the
-- current menu so the demo is internally consistent, but nothing enforces that - which is the
-- point. The 5-day-old order below could name a dish that no longer exists and still be correct.
--
-- Joined to the order on PublicToken rather than an id, since ids move on every re-run.
INSERT INTO "OrderItems" ("OrderId", "ItemName", "UnitPrice", "Quantity", "Notes")
SELECT o."Id", v.item_name, v.unit_price, v.quantity, v.notes
FROM (VALUES
    -- 1: Sam Carter, completed, 5 days ago
    ('00000000-0000-4000-8000-000000000001'::uuid, 'Chicken Curry',                9.20, 1, ''),
    ('00000000-0000-4000-8000-000000000001'::uuid, 'Egg Fried Rice',               3.90, 1, ''),
    ('00000000-0000-4000-8000-000000000001'::uuid, 'Prawn Crackers',               2.20, 1, ''),

    -- 2: Sam Carter, completed collection, 2 days ago
    ('00000000-0000-4000-8000-000000000002'::uuid, 'Salt and Chilli Chicken',      9.90, 1, ''),
    ('00000000-0000-4000-8000-000000000002'::uuid, 'Chips',                        3.40, 1, ''),

    -- 4: Laura Bennett, completed
    ('00000000-0000-4000-8000-000000000004'::uuid, 'Sweet and Sour Chicken',       9.80, 2, ''),
    ('00000000-0000-4000-8000-000000000004'::uuid, 'Boiled Rice',                  3.20, 2, ''),
    ('00000000-0000-4000-8000-000000000004'::uuid, 'Vegetable Spring Rolls',       3.80, 1, ''),

    -- 5: Michael Doyle, completed collection
    ('00000000-0000-4000-8000-000000000005'::uuid, 'Barbecue Spare Ribs',         10.20, 1, ''),
    ('00000000-0000-4000-8000-000000000005'::uuid, 'Curry Chips',                  4.60, 1, ''),

    -- 15: Niamh Doherty, cancelled
    ('00000000-0000-4000-8000-000000000015'::uuid, 'Kung Po Chicken',              9.90, 1, ''),
    ('00000000-0000-4000-8000-000000000015'::uuid, 'Boiled Rice',                  3.20, 1, ''),

    -- 3: Sam Carter, out for delivery
    ('00000000-0000-4000-8000-000000000003'::uuid, 'Beef in Oyster Sauce',        10.40, 1, ''),
    ('00000000-0000-4000-8000-000000000003'::uuid, 'Chow Mein Noodles',            4.60, 1, ''),

    -- 6: Priya Nair, out for delivery
    ('00000000-0000-4000-8000-000000000006'::uuid, 'King Prawn Curry',            11.40, 1, ''),
    ('00000000-0000-4000-8000-000000000006'::uuid, 'Egg Fried Rice',              3.90, 1, ''),
    ('00000000-0000-4000-8000-000000000006'::uuid, 'Sesame Prawn Toast',           5.90, 1, ''),

    -- 7: Tom Hughes, ready
    ('00000000-0000-4000-8000-000000000007'::uuid, 'Crispy Shredded Chilli Beef', 10.80, 1, ''),
    ('00000000-0000-4000-8000-000000000007'::uuid, 'Chips',                        3.40, 2, ''),

    -- 8: Grace Millar, ready collection
    ('00000000-0000-4000-8000-000000000008'::uuid, 'Chicken in Black Bean Sauce',  9.60, 1, ''),
    ('00000000-0000-4000-8000-000000000008'::uuid, 'Boiled Rice',                  3.20, 1, ''),

    -- 9: Daniel Okafor, preparing
    ('00000000-0000-4000-8000-000000000009'::uuid, 'Salt and Chilli King Prawns', 11.80, 1, ''),
    ('00000000-0000-4000-8000-000000000009'::uuid, 'Egg Fried Rice',               3.90, 1, ''),
    ('00000000-0000-4000-8000-000000000009'::uuid, 'Chicken Satay Skewers',        6.40, 1, ''),

    -- 10: Aoife Brennan, preparing collection
    ('00000000-0000-4000-8000-000000000010'::uuid, 'Sweet and Sour Pork Balls',    9.80, 1, ''),
    ('00000000-0000-4000-8000-000000000010'::uuid, 'Chips',                        3.40, 1, ''),
    ('00000000-0000-4000-8000-000000000010'::uuid, 'Coca-Cola',                    1.60, 2, ''),

    -- 11: Ruth Alexander, preparing. Quantity 2 with a modifier on the line, which is the case
    -- ComputeTotal and the frontend's lineTotal must agree on: the delta is added ONCE, not twice.
    ('00000000-0000-4000-8000-000000000011'::uuid, 'Chicken Curry',                9.20, 2, ''),
    ('00000000-0000-4000-8000-000000000011'::uuid, 'Chow Mein Noodles',            4.60, 1, ''),

    -- 12: Chris Wong, paid
    ('00000000-0000-4000-8000-000000000012'::uuid, 'King Prawn in Black Bean Sauce', 11.60, 1, ''),
    ('00000000-0000-4000-8000-000000000012'::uuid, 'Egg Fried Rice',               3.90, 1, ''),

    -- 13: Hannah Reid, paid collection
    ('00000000-0000-4000-8000-000000000013'::uuid, 'Salt and Chilli Chicken Wings', 6.50, 1, ''),
    ('00000000-0000-4000-8000-000000000013'::uuid, 'Chips',                         3.40, 1, ''),

    -- 14: Owen Bell, pending
    ('00000000-0000-4000-8000-000000000014'::uuid, 'Chicken Curry',                9.20, 1, ''),
    ('00000000-0000-4000-8000-000000000014'::uuid, 'Boiled Rice',                  3.20, 1, '')
) AS v(token, item_name, unit_price, quantity, notes)
JOIN "Orders" o ON o."PublicToken" = v.token;

-- Order item modifiers ---------------------------------------------------------------------------
-- Snapshots again: Name and PriceDelta are copied, never referenced, so re-seeding the menu cannot
-- change what a past order was charged.
--
-- Joined on (order token, item name), which is why no order above lists the same dish on two
-- separate lines - a duplicate name would attach these modifiers to both.
--
-- Zero-delta rows are here on purpose. A spice level or a "no onions" is a real instruction to the
-- kitchen that costs nothing, and it should still appear on the ticket the kitchen reads.
INSERT INTO "OrderItemModifiers" ("OrderItemId", "Name", "PriceDelta")
SELECT oi."Id", v.name, v.price_delta
FROM (VALUES
    ('00000000-0000-4000-8000-000000000001'::uuid, 'Chicken Curry',                'Medium',            0.00),
    ('00000000-0000-4000-8000-000000000001'::uuid, 'Chicken Curry',                'Regular',           0.00),

    ('00000000-0000-4000-8000-000000000002'::uuid, 'Salt and Chilli Chicken',      'Hot',               0.00),
    ('00000000-0000-4000-8000-000000000002'::uuid, 'Salt and Chilli Chicken',      'Large',             2.50),

    ('00000000-0000-4000-8000-000000000004'::uuid, 'Sweet and Sour Chicken',       'Regular',           0.00),
    ('00000000-0000-4000-8000-000000000004'::uuid, 'Vegetable Spring Rolls',       'Sweet and Sour Dip', 0.60),

    ('00000000-0000-4000-8000-000000000003'::uuid, 'Beef in Oyster Sauce',         'Large',             2.50),
    ('00000000-0000-4000-8000-000000000003'::uuid, 'Beef in Oyster Sauce',         'No Mushrooms',      0.00),

    ('00000000-0000-4000-8000-000000000006'::uuid, 'King Prawn Curry',             'Extra Hot',         0.00),
    ('00000000-0000-4000-8000-000000000006'::uuid, 'King Prawn Curry',             'Regular',           0.00),
    ('00000000-0000-4000-8000-000000000006'::uuid, 'Sesame Prawn Toast',           'Curry Dip',         0.60),

    ('00000000-0000-4000-8000-000000000007'::uuid, 'Crispy Shredded Chilli Beef',  'Medium',            0.00),
    ('00000000-0000-4000-8000-000000000007'::uuid, 'Crispy Shredded Chilli Beef',  'Large',             2.50),

    ('00000000-0000-4000-8000-000000000009'::uuid, 'Salt and Chilli King Prawns',  'Hot',               0.00),
    ('00000000-0000-4000-8000-000000000009'::uuid, 'Salt and Chilli King Prawns',  'Regular',           0.00),
    ('00000000-0000-4000-8000-000000000009'::uuid, 'Salt and Chilli King Prawns',  'Extra Vegetables',  1.00),

    -- The quantity-2 line. One row, one 1.50, however many portions - OrderItemModifier has no
    -- quantity column, and that is the recorded commercial decision, not an omission.
    ('00000000-0000-4000-8000-000000000011'::uuid, 'Chicken Curry',                'Mild',              0.00),
    ('00000000-0000-4000-8000-000000000011'::uuid, 'Chicken Curry',                'Extra Chicken',     1.50),

    ('00000000-0000-4000-8000-000000000012'::uuid, 'King Prawn in Black Bean Sauce', 'Regular',         0.00),
    ('00000000-0000-4000-8000-000000000012'::uuid, 'King Prawn in Black Bean Sauce', 'No Peppers',      0.00),

    ('00000000-0000-4000-8000-000000000013'::uuid, 'Salt and Chilli Chicken Wings', 'Medium',           0.00),
    ('00000000-0000-4000-8000-000000000013'::uuid, 'Salt and Chilli Chicken Wings', 'Garlic Mayo Dip',  0.60)
) AS v(token, item_name, name, price_delta)
JOIN "Orders" o ON o."PublicToken" = v.token
JOIN "OrderItems" oi ON oi."OrderId" = o."Id" AND oi."ItemName" = v.item_name;

-- Link the demo customer -------------------------------------------------------------------------
-- Identity stores an upper-cased NormalizedEmail, which is what it looks accounts up by, so match
-- on that rather than "Email". If no such account exists these three simply stay guest orders:
-- CustomerId is nullable precisely because guest checkout is the default path, so an unmatched
-- lookup produces a valid order rather than a failure.
--
-- CustomerName/CustomerPhone stay populated on the row regardless. Delivery needs a name and a
-- number whether or not somebody was logged in.
UPDATE "Orders" o
SET "CustomerId" = c."Id"
FROM "Customers" c
JOIN "AspNetUsers" u ON u."Id" = c."ApplicationUserId"
WHERE u."NormalizedEmail" = 'DEMO.CUSTOMER@EXAMPLE.COM'
  AND o."PublicToken" IN (
      '00000000-0000-4000-8000-000000000001',
      '00000000-0000-4000-8000-000000000002',
      '00000000-0000-4000-8000-000000000003'
  );

-- Assign drivers ---------------------------------------------------------------------------------
-- By MAX id rather than by name, so this works against any environment's drivers without knowing
-- what they are called. With no drivers at all it resolves to NULL and the deliveries are simply
-- unassigned - a real state the owner board has to render anyway (an order ready to go out with
-- nobody on it yet), so an environment with no drivers still seeds successfully.
--
-- All three go to ONE driver rather than being split across two. Two reasons, and the second is
-- the one that decided it:
--   * A small takeaway runs one driver at a time, doing a round of several drops.
--   * Split across two, each driver's dashboard shows a single card, and a screenshot of it says
--     nothing about how the screen behaves with a real workload on it. The demo has to show the
--     busy case, because that is the case the layout was designed for.
--
-- Note the mix this produces: two OutForDelivery and one Ready, all assigned to the same person.
-- That is deliberate - it is what the board looks like mid-round, with one drop still waiting on
-- the kitchen.
UPDATE "Orders"
SET "DriverId" = (SELECT MAX("Id") FROM "Drivers")
WHERE "PublicToken" IN (
    '00000000-0000-4000-8000-000000000003',
    '00000000-0000-4000-8000-000000000006',
    '00000000-0000-4000-8000-000000000007'
);

-- Check what landed: the board as the owner will see it.
SELECT "Status", COUNT(*) AS orders, SUM(CASE WHEN "DriverId" IS NOT NULL THEN 1 ELSE 0 END) AS with_driver
FROM "Orders"
GROUP BY "Status"
ORDER BY "Status";
