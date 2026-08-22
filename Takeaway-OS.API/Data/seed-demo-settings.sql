-- Demo settings: opening hours, delivery area and delivery fee for the deployed portfolio demo.
--
-- These three tables share a property that makes them the most dangerous empty state in the app:
-- each one FAILS CLOSED, correctly, and each failure looks exactly like a bug.
--
--   No OpeningHours rows   -> every order rejected with a 409, shop permanently "closed"
--   No DeliveryAreas rows  -> every delivery order rejected with a 400
--   DeliveryFee left at 0  -> delivery is silently free
--
-- A fresh database has all three. This file is what turns a deployed-but-inert API into one that
-- can actually take an order, and it must be run before anyone is shown the URL.
--
-- Run: psql -h localhost -p 5432 -U postgres -d "Takeaway-OS" -f Data/seed-demo-settings.sql
-- Destructive and re-runnable, same as seed-demo-menu.sql.

-- Opening hours ------------------------------------------------------------------------------
-- OPEN 24/7, which is a demo decision rather than a plausible trading pattern. Someone opening
-- the link at 2am - a recruiter in another timezone, or you at the end of a long evening - has to
-- be able to complete an order. Realistic hours would mean the demo silently refuses most visitors
-- outside a five-hour window, and the thing working correctly would be invisible.
--
-- Getting to genuine 24/7 takes 14 rows rather than 7, because the schema has no "all day" form:
-- OpenTime = CloseTime is rejected outright as ambiguous between zero minutes and a full day.
-- So each day is split in half:
--
--   'Monday', 00:00 -> 12:00   a normal same-day window, since CloseTime > OpenTime
--   'Monday', 12:00 -> 00:00   CloseTime <= OpenTime, so this is a PAST-MIDNIGHT window and runs
--                              from Monday noon to exactly Tuesday 00:00
--
-- Tuesday's first row then starts at exactly 00:00, where Monday's second row stopped. That leaves
-- no gap and raises no overlap error because the interval maths in BusinessHoursService is
-- half-open at both ends: Overlaps() tests `offset < a.Length`, so a window starting exactly where
-- another finishes does not collide, and IsWithin() tests `now < CloseTime`, so the same instant is
-- never inside two windows at once. The Saturday-to-Sunday join wraps the week and behaves the same.
--
-- Consequence worth knowing: this does NOT mean "ignore opening hours". The check still runs on
-- every order, the banner still reads from the same GetStatusAsync, and setting the temporary
-- closure flag still shuts the whole thing. It only means no hour of the week is outside a window.
DELETE FROM "OpeningHours";

-- "DayOfWeek" is stored as text ('Tuesday'), not an int - see AppDbContext's HasConversion<string>().
INSERT INTO "OpeningHours" ("DayOfWeek", "OpenTime", "CloseTime") VALUES
    ('Sunday',    '00:00', '12:00'),
    ('Sunday',    '12:00', '00:00'),
    ('Monday',    '00:00', '12:00'),
    ('Monday',    '12:00', '00:00'),
    ('Tuesday',   '00:00', '12:00'),
    ('Tuesday',   '12:00', '00:00'),
    ('Wednesday', '00:00', '12:00'),
    ('Wednesday', '12:00', '00:00'),
    ('Thursday',  '00:00', '12:00'),
    ('Thursday',  '12:00', '00:00'),
    ('Friday',    '00:00', '12:00'),
    ('Friday',    '12:00', '00:00'),
    ('Saturday',  '00:00', '12:00'),
    ('Saturday',  '12:00', '00:00');

-- Delivery area ------------------------------------------------------------------------------
-- Outward code only, uppercase, no whitespace: that is exactly what UkPostcode.Normalise produces
-- (whitespace stripped, ToUpperInvariant), and the stored value is compared for EXACT equality
-- against the normalised outward code of an order's postcode. Seeding 'bt16' or 'BT16 1AA' would
-- store something no real postcode can ever match, and every delivery order would be refused with
-- no obvious cause.
--
-- Exact equality, never StartsWith, is the whole reason districts are stored rather than prefixes.
-- Under prefix matching 'BT1' would also match 'BT16 1AA' and quietly commit the shop to a
-- different part of Belfast.
DELETE FROM "DeliveryAreas";
INSERT INTO "DeliveryAreas" ("OutwardCode") VALUES ('BT16');

-- Restaurant settings ------------------------------------------------------------------------
-- The singleton row (Id = 1) is created structurally by the AddBusinessHours migration, so this is
-- an UPDATE and not an INSERT. ON CONFLICT covers the one case where it is missing - a database
-- whose migration history was tampered with - rather than letting the seed appear to succeed while
-- changing nothing.
--
-- DeliveryFee 2.50 is snapshotted onto each order at creation, so changing it later never rewrites
-- what an existing order cost. Collection orders snapshot 0.00 regardless.
--
-- IsTemporarilyClosed is forced false and the reason cleared: it has no expiry, so a flag left on
-- from testing would leave the demo permanently refusing orders with a stale message.
INSERT INTO "RestaurantSettings" ("Id", "IsTemporarilyClosed", "ClosureReason", "DeliveryFee")
VALUES (1, false, '', 2.50)
ON CONFLICT ("Id") DO UPDATE SET
    "IsTemporarilyClosed" = false,
    "ClosureReason"       = '',
    "DeliveryFee"         = 2.50;

-- Check what landed.
SELECT "DayOfWeek", "OpenTime", "CloseTime" FROM "OpeningHours" ORDER BY "Id";
SELECT "OutwardCode" FROM "DeliveryAreas";
SELECT "Id", "IsTemporarilyClosed", "ClosureReason", "DeliveryFee" FROM "RestaurantSettings";
