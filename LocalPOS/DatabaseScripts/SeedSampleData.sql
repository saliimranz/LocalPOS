/*
    Populates all LocalPOS transactional tables with predictable seed data
    so the web UI can be exercised end-to-end without manually creating
    orders. The script touches the following tables:
        1. dbo.SPARE_PARTS
        2. dbo.TBL_SP_PSO_SKU
        3. dbo.TBL_DEALERS
        4. dbo.TBL_SP_PSO_ORDER
        5. dbo.TBL_SP_PSO_ITEM
        6. dbo.TBL_SP_PSO_PAYMENT

    Adjust column lists if your schema diverges from the assumptions below.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @SparePartCodes TABLE (Code VARCHAR(50));
    INSERT INTO @SparePartCodes (Code)
    VALUES ('TEST-SP-ENGINE'), ('TEST-SP-BATTERY'), ('TEST-SP-BELT');

    DECLARE @SkuCodes TABLE (Code VARCHAR(50));
    INSERT INTO @SkuCodes (Code)
    VALUES ('TEST-SKU-1001'), ('TEST-SKU-1002'), ('TEST-SKU-1003');

    DECLARE @DealerCodes TABLE (Code VARCHAR(50));
    INSERT INTO @DealerCodes (Code)
    VALUES ('TEST-DEALER-001'), ('TEST-DEALER-002');

    DECLARE @OrderNumbers TABLE (OrderNo VARCHAR(30));
    INSERT INTO @OrderNumbers (OrderNo)
    VALUES ('POS-TEST-0001'), ('POS-TEST-0002');

    DECLARE @PaymentRefs TABLE (PaymentRef VARCHAR(30));
    INSERT INTO @PaymentRefs (PaymentRef)
    VALUES ('PMT-TEST-9001'), ('PMT-TEST-9002');

    -- Clean up any previous test data so the script is idempotent.
    DELETE p
    FROM dbo.TBL_SP_PSO_PAYMENT AS p
    WHERE p.PAYMENT_REFERENCE IN (SELECT PaymentRef FROM @PaymentRefs);

    DELETE i
    FROM dbo.TBL_SP_PSO_ITEM AS i
    WHERE i.SPPSOID IN (SELECT o.ID FROM dbo.TBL_SP_PSO_ORDER AS o WHERE o.SPPSOID IN (SELECT OrderNo FROM @OrderNumbers));

    DELETE o
    FROM dbo.TBL_SP_PSO_ORDER AS o
    WHERE o.SPPSOID IN (SELECT OrderNo FROM @OrderNumbers);

    DELETE sku
    FROM dbo.TBL_SP_PSO_SKU AS sku
    WHERE sku.SKU_CODE IN (SELECT Code FROM @SkuCodes);

    DELETE sp
    FROM dbo.SPARE_PARTS AS sp
    WHERE sp.SP_CODE IN (SELECT Code FROM @SparePartCodes);

    DELETE d
    FROM dbo.TBL_DEALERS AS d
    WHERE d.DealerID IN (SELECT Code FROM @DealerCodes);

    -- Insert spare parts that serve as the base product catalog.
    DECLARE @InsertedSpareParts TABLE (Code VARCHAR(50) PRIMARY KEY, SparePartId INT);

    INSERT INTO dbo.SPARE_PARTS (SP_CODE, Description, Category, Brand)
    OUTPUT inserted.SP_CODE, inserted.ID
    INTO @InsertedSpareParts (Code, SparePartId)
    VALUES
        ('TEST-SP-ENGINE',  '14mm Impact Drill Motor', 'Power Tools',   'VoltCraft'),
        ('TEST-SP-BATTERY', '20V Lithium Battery Pack', 'Accessories',   'EonCharge'),
        ('TEST-SP-BELT',    'Kevlar Reinforced Drive Belt', 'Hardware', 'FlexiParts');

    -- Insert SKUs bound to the spare parts above.
    DECLARE @InsertedSkus TABLE (SkuCode VARCHAR(50) PRIMARY KEY, SkuId INT);

    INSERT INTO dbo.TBL_SP_PSO_SKU
    (
        SparePartsId,
        SKU_CODE,
        DisplayName,
        DisplayDescription,
        DefaultImageUrl,
        RetailPrice,
        WholesalePrice,
        TaxRate,
        StockQuantity,
        MinStockThreshold,
        IsActive
    )
    OUTPUT inserted.SKU_CODE, inserted.ID
    INTO @InsertedSkus (SkuCode, SkuId)
    SELECT
        sp.SparePartId,
        data.SkuCode,
        data.DisplayName,
        data.DisplayDescription,
        data.ImageUrl,
        data.RetailPrice,
        data.WholesalePrice,
        data.TaxRate,
        data.StockQuantity,
        data.MinStockThreshold,
        1
    FROM @InsertedSpareParts AS sp
    INNER JOIN (
        VALUES
            ('TEST-SP-ENGINE',  'TEST-SKU-1001', 'VoltCraft Impact Drill Kit',    'High torque drill body bundled with spare motor.', 'https://placehold.co/200x200?text=Drill',   18500.00, 14900.00, 17.00, 24, 5),
            ('TEST-SP-BATTERY', 'TEST-SKU-1002', 'EonCharge 20V Battery Pack',    'Fast charging Li-Ion battery compatible with VoltCraft tools.', 'https://placehold.co/200x200?text=Battery', 9500.00,  7900.00, 12.50, 40, 8),
            ('TEST-SP-BELT',    'TEST-SKU-1003', 'FlexiParts Kevlar Drive Belt',  'Heat resistant belt for belt sanders up to 52mm width.',       'https://placehold.co/200x200?text=Belt',   4500.00,  3600.00, 8.00,  60, 10)
    ) AS data(PartCode, SkuCode, DisplayName, DisplayDescription, ImageUrl, RetailPrice, WholesalePrice, TaxRate, StockQuantity, MinStockThreshold)
        ON sp.Code = data.PartCode;

    -- Insert a couple of dealers/customers.
    DECLARE @InsertedDealers TABLE (DealerCode VARCHAR(50) PRIMARY KEY, DealerId INT, DealerName NVARCHAR(150));

    INSERT INTO dbo.TBL_DEALERS (DealerName, DealerID, ContactPerson, CellNumber, City, Status)
    OUTPUT inserted.DealerID, inserted.ID, inserted.DealerName
    INTO @InsertedDealers (DealerCode, DealerId, DealerName)
    VALUES
        ('Inno Mechanics Supplies', 'TEST-DEALER-001', 'Arham Masood', '+92-300-1111111', 'Karachi', 1),
        ('Metro Build Traders',     'TEST-DEALER-002', 'Hiba Rauf',    '+92-321-2222222', 'Lahore',  1);

    -- Insert two POS orders that consume the SKUs above.
    DECLARE @InsertedOrders TABLE (OrderNumber VARCHAR(30) PRIMARY KEY, OrderId INT, DealerCode VARCHAR(50), TotalDue DECIMAL(18,2), Outstanding DECIMAL(18,2));

    INSERT INTO dbo.TBL_SP_PSO_ORDER
    (
        INQUIRY_NO,
        SPPSOID,
        DID,
        ORDERTYPE,
        CONTACT,
        STATUS,
        ORD_STATUS,
        CDATED,
        ORDER_PROPERTY,
        TP,
        DP_STATUS,
        userid,
        ORDER_SO_DATE,
        ORDER_C_DATE
    )
    OUTPUT inserted.SPPSOID, inserted.ID, data.DealerCode, data.TotalDue, data.Outstanding
    INTO @InsertedOrders (OrderNumber, OrderId, DealerCode, TotalDue, Outstanding)
    SELECT
        data.InquiryNo,
        data.OrderNumber,
        dealers.DealerId,
        'POS',
        dealers.DealerName,
        1,
        'Completed',
        GETDATE(),
        'Retail',
        data.TotalDue,
        CASE WHEN data.Outstanding > 0 THEN 0 ELSE 1 END,
        data.UserId,
        GETDATE(),
        GETDATE()
    FROM (
        VALUES
            ('INQ-TEST-0001', 'POS-TEST-0001', 'TEST-DEALER-001', 37500.00, 0.00,  'demo.user@localpos'),
            ('INQ-TEST-0002', 'POS-TEST-0002', 'TEST-DEALER-002', 22500.00, 12500.00, 'ops.user@localpos')
    ) AS data(InquiryNo, OrderNumber, DealerCode, TotalDue, Outstanding, UserId)
    INNER JOIN @InsertedDealers AS dealers ON dealers.DealerCode = data.DealerCode;

    -- Insert order line items.
    INSERT INTO dbo.TBL_SP_PSO_ITEM
    (
        SPPSOID,
        ITEMID,
        ITEMNAME,
        QTY,
        TotalAmount,
        RP,
        ST,
        status,
        ORDER_STATUS,
        CDATED
    )
    SELECT
        orders.OrderId,
        skus.SkuId,
        line.ItemName,
        line.Qty,
        line.TotalAmount,
        line.UnitPrice,
        line.TaxRate,
        1,
        'Completed',
        GETDATE()
    FROM (
        VALUES
            ('POS-TEST-0001', 'TEST-SKU-1001', 'VoltCraft Impact Drill Kit',   1, 18500.00, 18500.00, 17.00),
            ('POS-TEST-0001', 'TEST-SKU-1002', 'EonCharge 20V Battery Pack',   2, 19000.00, 9500.00, 12.50),
            ('POS-TEST-0002', 'TEST-SKU-1003', 'FlexiParts Kevlar Drive Belt', 5, 22500.00, 4500.00, 8.00)
    ) AS line(OrderNumber, SkuCode, ItemName, Qty, TotalAmount, UnitPrice, TaxRate)
    INNER JOIN @InsertedOrders AS orders ON orders.OrderNumber = line.OrderNumber
    INNER JOIN @InsertedSkus AS skus ON skus.SkuCode = line.SkuCode;

    -- Insert payment records for the orders (one full payment, one partial).
    INSERT INTO dbo.TBL_SP_PSO_PAYMENT
    (
        ORDER_ID,
        PAYMENT_REFERENCE,
        PAYMENT_METHOD,
        PAID_AMOUNT,
        OUTSTANDING,
        CURRENCY_CODE,
        IS_PARTIAL,
        CREATED_BY,
        NOTES
    )
    SELECT
        orders.OrderId,
        pay.PaymentReference,
        pay.Method,
        pay.PaidAmount,
        pay.Outstanding,
        'PKR',
        CASE WHEN pay.Outstanding > 0 THEN 1 ELSE 0 END,
        pay.CreatedBy,
        pay.Notes
    FROM (
        VALUES
            ('POS-TEST-0001', 'PMT-TEST-9001', 'Cash',    37500.00,   0.00,  'demo.user@localpos', 'Full cash settlement for POS-TEST-0001'),
            ('POS-TEST-0002', 'PMT-TEST-9002', 'Partial', 10000.00, 12500.00, 'ops.user@localpos', 'Partial payment leaves an outstanding balance')
    ) AS pay(OrderNumber, PaymentReference, Method, PaidAmount, Outstanding, CreatedBy, Notes)
    INNER JOIN @InsertedOrders AS orders ON orders.OrderNumber = pay.OrderNumber;

    COMMIT TRANSACTION;
    PRINT 'Seed data inserted successfully.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    DECLARE @ErrorMessage NVARCHAR(4000) = ERROR_MESSAGE();
    DECLARE @ErrorSeverity INT = ERROR_SEVERITY();
    DECLARE @ErrorState INT = ERROR_STATE();
    RAISERROR(@ErrorMessage, @ErrorSeverity, @ErrorState);
END CATCH;
