-- Dev seed for the weekly schedule. NOT a migration, on purpose:
-- opening hours are the owner's business data, not database schema. Baking real trading hours
-- into a migration would mean a code change + redeploy every time the shop's hours change.
-- (The RestaurantSettings row IS seeded by the migration — that's a structural singleton, and
-- the app needs exactly one of it to exist to function at all.)
--
-- Run in pgAdmin against the Takeaway-OS database. Safe to re-run: it clears first.
-- Edit the times below to the real shop hours whenever you know them.

DELETE FROM "OpeningHours";

-- "DayOfWeek" is stored as text ('Tuesday'), not an int — see AppDbContext's HasConversion<string>().
-- "Id" is omitted: it's an identity column and Postgres assigns it.
INSERT INTO "OpeningHours" ("DayOfWeek", "OpenTime", "CloseTime") VALUES
    -- Monday: no rows at all = closed. Absence of a window is what "closed" means;
    -- there's no IsClosed flag on the table.
    ('Tuesday',   '17:00', '23:00'),
    ('Wednesday', '17:00', '23:00'),
    ('Thursday',  '17:00', '23:00'),

    -- Friday and Saturday run past midnight. CloseTime (00:30) being EARLIER than OpenTime
    -- (17:00) is exactly how BusinessHoursService detects a window that crosses into the next
    -- day — so at 00:15 on Saturday the shop is still open on FRIDAY's row.
    ('Friday',    '17:00', '00:30'),
    ('Saturday',  '17:00', '00:30'),

    ('Sunday',    '17:00', '22:30'),

    -- A split shift is just two rows for the same day — here a Sunday lunch service.
    -- Included so the multi-row-per-day path actually gets exercised in dev.
    ('Sunday',    '12:00', '14:30');

SELECT "DayOfWeek", "OpenTime", "CloseTime" FROM "OpeningHours" ORDER BY "Id";
