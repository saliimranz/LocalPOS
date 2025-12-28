/* ============================================================
   Customer Default Discount - schema update

   Adds an optional percent field to dealers/customers so POS can
   apply a base price adjustment before other discounts.
   ============================================================ */

IF COL_LENGTH('dbo.TBL_DEALERS', 'Default_Discount_Percentage') IS NULL
BEGIN
    ALTER TABLE dbo.TBL_DEALERS
        ADD Default_Discount_Percentage DECIMAL(18,4) NULL;
END
ELSE
BEGIN
    PRINT 'Column Default_Discount_Percentage already exists on dbo.TBL_DEALERS; skipping alter.';
END;

