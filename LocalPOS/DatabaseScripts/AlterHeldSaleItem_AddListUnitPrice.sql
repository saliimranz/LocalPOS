/* ============================================================
   Migration: add list/original unit price to held sale items.

   This supports customer default discount repricing after restore
   without compounding discounts.
   ============================================================ */

IF COL_LENGTH('dbo.TBL_POS_HELD_SALE_ITEM', 'LIST_UNIT_PRICE') IS NULL
BEGIN
    ALTER TABLE dbo.TBL_POS_HELD_SALE_ITEM
    ADD LIST_UNIT_PRICE DECIMAL(18,4) NULL;
END
ELSE
BEGIN
    PRINT 'LIST_UNIT_PRICE already exists on dbo.TBL_POS_HELD_SALE_ITEM; skipping.';
END;

