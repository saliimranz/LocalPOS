/* ============================================================
   Migration: add customer default discount percentage.

   Value is percent (0-100). App logic enforces range.
   ============================================================ */

IF COL_LENGTH('dbo.TBL_DEALERS', 'Default_Discount_Percentage') IS NULL
BEGIN
    ALTER TABLE dbo.TBL_DEALERS
    ADD Default_Discount_Percentage DECIMAL(18,4) NULL;
END
ELSE
BEGIN
    PRINT 'Default_Discount_Percentage already exists on dbo.TBL_DEALERS; skipping.';
END;

