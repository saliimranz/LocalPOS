IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'TBL_SP_PSO_SKU' AND schema_id = SCHEMA_ID('dbo'))
BEGIN
    CREATE TABLE dbo.TBL_SP_PSO_SKU
    (
        ID                  INT            IDENTITY(1,1) PRIMARY KEY,
        SparePartsId        INT            NOT NULL,
        SKU_CODE            VARCHAR(100)   NOT NULL,
        DisplayName         NVARCHAR(200)  NOT NULL,
        DisplayDescription  NVARCHAR(500)  NULL,
        DefaultImageUrl     NVARCHAR(500)  NULL,
        RetailPrice         DECIMAL(18,2)  NOT NULL,
        WholesalePrice      DECIMAL(18,2)  NULL,
        TaxRate             DECIMAL(5,2)   NOT NULL CONSTRAINT DF_SKU_TAXRATE DEFAULT (0),
        StockQuantity       INT            NOT NULL CONSTRAINT DF_SKU_STOCK DEFAULT (0),
        MinStockThreshold   INT            NOT NULL CONSTRAINT DF_SKU_MINTH DEFAULT (0),
        IsActive            BIT            NOT NULL CONSTRAINT DF_SKU_ISACTIVE DEFAULT (1),
        LastUpdated         DATETIME       NOT NULL CONSTRAINT DF_SKU_UPDATED DEFAULT (GETDATE()),
        CONSTRAINT FK_SKU_SPAREPART FOREIGN KEY (SparePartsId) REFERENCES dbo.SPARE_PARTS(ID),
        CONSTRAINT UQ_SKU_CODE UNIQUE (SKU_CODE)
    );

    CREATE INDEX IX_TBL_SP_PSO_SKU_SparePart ON dbo.TBL_SP_PSO_SKU (SparePartsId);
END
ELSE
BEGIN
    PRINT 'TBL_SP_PSO_SKU already exists; skipping create.';
END;
