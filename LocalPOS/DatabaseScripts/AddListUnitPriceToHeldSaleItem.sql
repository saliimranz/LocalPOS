/* ============================================================
   Customer Default Discount - held sale baseline pricing

   Stores LIST_UNIT_PRICE on held sale items so:
   - restored held sales keep saved effective prices
   - switching customer can re-price from list baseline (no compounding)
   ============================================================ */

IF COL_LENGTH('dbo.TBL_POS_HELD_SALE_ITEM', 'LIST_UNIT_PRICE') IS NULL
BEGIN
    ALTER TABLE dbo.TBL_POS_HELD_SALE_ITEM
        ADD LIST_UNIT_PRICE DECIMAL(18,4) NULL;
END
ELSE
BEGIN
    PRINT 'Column LIST_UNIT_PRICE already exists on dbo.TBL_POS_HELD_SALE_ITEM; skipping alter.';
END;

