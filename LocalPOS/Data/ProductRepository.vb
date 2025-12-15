Imports System
Imports System.Data.SqlClient
Imports System.Linq
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Data
    Public Class ProductRepository
        Inherits SqlRepositoryBase

        Public Function GetCategories() As IList(Of String)
            Dim categories As New List(Of String)()
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT DISTINCT ISNULL(Category, 'Misc') AS Category FROM dbo.SPARE_PARTS WHERE ISNULL(Category, '') <> '' ORDER BY Category"
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            categories.Add(reader.GetString(0))
                        End While
                    End Using
                End Using
            End Using
            Return categories
        End Function

        Public Function SearchProducts(Optional searchTerm As String = Nothing, Optional category As String = Nothing) As IList(Of Product)
            Dim products As New List(Of Product)()
            Dim normalizedSearchTerm = If(searchTerm, String.Empty).Trim()
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT TOP (120)
        sku.ID AS ProductId,
        sku.SparePartsId,
        sku.SKU_CODE,
        COALESCE(sku.DisplayName, sp.Description, sp.SP_CODE) AS DisplayName,
        ISNULL(sp.Category, 'Misc') AS Category,
        ISNULL(sp.Brand, 'General') AS Brand,
        ISNULL(sku.DefaultImageUrl, '') AS ImageUrl,
        sku.RetailPrice,
        sku.TaxRate,
        COALESCE(sku.DisplayDescription, sp.Description, '') AS Description,
        sku.StockQuantity,
        sku.MinStockThreshold
FROM dbo.SPARE_PARTS sp
INNER JOIN dbo.TBL_SP_PSO_SKU sku ON sku.SparePartsId = sp.ID
WHERE sku.IsActive = 1
  AND (@Category IS NULL OR sp.Category = @Category)
  AND (
        @SearchTerm IS NULL OR
        sku.DisplayName LIKE '%' + @SearchTerm + '%' OR
        sku.DisplayDescription LIKE '%' + @SearchTerm + '%' OR
        COALESCE(sku.DisplayName, sp.Description, sp.SP_CODE) LIKE '%' + @SearchTerm + '%' OR
        sp.Description LIKE '%' + @SearchTerm + '%' OR
        sp.SP_CODE LIKE '%' + @SearchTerm + '%' OR
        sku.SKU_CODE LIKE '%' + @SearchTerm + '%'
      )
ORDER BY sku.DisplayName"
                    command.Parameters.AddWithValue("@Category", If(String.IsNullOrWhiteSpace(category), CType(DBNull.Value, Object), category))
                    command.Parameters.AddWithValue("@SearchTerm", If(String.IsNullOrWhiteSpace(normalizedSearchTerm), CType(DBNull.Value, Object), normalizedSearchTerm))

                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            products.Add(MapProduct(reader))
                        End While
                    End Using
                End Using
            End Using
            Return products
        End Function

        Public Function GetProduct(productId As Integer) As Product
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT TOP 1 sku.ID AS ProductId,
        sku.SparePartsId,
        sku.SKU_CODE,
        COALESCE(sku.DisplayName, sp.Description, sp.SP_CODE) AS DisplayName,
        ISNULL(sp.Category, 'Misc') AS Category,
        ISNULL(sp.Brand, 'General') AS Brand,
        ISNULL(sku.DefaultImageUrl, '') AS ImageUrl,
        sku.RetailPrice,
        sku.TaxRate,
        COALESCE(sku.DisplayDescription, sp.Description, '') AS Description,
        sku.StockQuantity,
        sku.MinStockThreshold
FROM dbo.SPARE_PARTS sp
INNER JOIN dbo.TBL_SP_PSO_SKU sku ON sku.SparePartsId = sp.ID
WHERE sku.IsActive = 1 AND sku.ID = @Id"
                    command.Parameters.AddWithValue("@Id", productId)

                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return MapProduct(reader)
                        End If
                    End Using
                End Using
            End Using
            Return Nothing
        End Function

        Public Function CreateProduct(request As ProductCreateRequest) As Integer
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If String.IsNullOrWhiteSpace(request.SparePartCode) Then
                Throw New ArgumentException("Spare part code is required.", NameOf(request))
            End If

            Dim skuItems = If(request.Skus, Enumerable.Empty(Of ProductSkuRequest)()) _
                .Where(Function(sku) sku IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(sku.SkuCode)) _
                .ToList()

            If skuItems.Count = 0 Then
                Throw New ArgumentException("At least one SKU is required to create a product.", NameOf(request))
            End If

            Using connection = CreateConnection()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim sparePartId As Integer
                        Using command = connection.CreateCommand()
                            command.Transaction = transaction
                            command.CommandText =
"INSERT INTO dbo.SPARE_PARTS (SP_CODE, Description, Category, Brand)
VALUES (@Code, @Description, @Category, @Brand);
SELECT CAST(SCOPE_IDENTITY() AS INT);"
                            command.Parameters.AddWithValue("@Code", request.SparePartCode.Trim())
                            command.Parameters.AddWithValue("@Description", Coalesce(request.DisplayName, request.Description, request.SparePartCode))
                            command.Parameters.AddWithValue("@Category", ToDbValue(request.Category))
                            command.Parameters.AddWithValue("@Brand", ToDbValue(request.Brand))

                            sparePartId = Convert.ToInt32(command.ExecuteScalar())
                        End Using

                        For Each sku In skuItems
                            InsertSku(connection, transaction, sparePartId, sku, request)
                        Next

                        transaction.Commit()
                        Return sparePartId
                    Catch
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Function

        Public Sub UpdateProduct(request As ProductUpdateRequest)
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If request.ProductId <= 0 Then
                Throw New ArgumentException("A valid product identifier is required.", NameOf(request))
            End If

            Dim displayName = Coalesce(request.DisplayName, request.Description)
            Dim description = Coalesce(request.Description, displayName)
            Dim stock = Math.Max(0, request.StockQuantity)
            Dim threshold = Math.Max(0, request.MinStockThreshold)
            Dim retail = Math.Max(0D, request.RetailPrice)
            Dim taxRate = Math.Max(0D, request.TaxRate)

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"UPDATE dbo.TBL_SP_PSO_SKU
SET DisplayName = @DisplayName,
    DisplayDescription = @Description,
    RetailPrice = @RetailPrice,
    TaxRate = @TaxRate,
    StockQuantity = @StockQuantity,
    MinStockThreshold = @MinStockThreshold,
    DefaultImageUrl = CASE WHEN @ImageUrl IS NULL OR @ImageUrl = '' THEN DefaultImageUrl ELSE @ImageUrl END,
    LastUpdated = GETDATE()
WHERE ID = @Id;"
                    command.Parameters.AddWithValue("@DisplayName", displayName)
                    command.Parameters.AddWithValue("@Description", description)
                    command.Parameters.AddWithValue("@RetailPrice", retail)
                    command.Parameters.AddWithValue("@TaxRate", taxRate)
                    command.Parameters.AddWithValue("@StockQuantity", stock)
                    command.Parameters.AddWithValue("@MinStockThreshold", threshold)
                    command.Parameters.AddWithValue("@ImageUrl", ToDbValue(request.ImageUrl))
                    command.Parameters.AddWithValue("@Id", request.ProductId)

                    Dim affected = command.ExecuteNonQuery()
                    If affected = 0 Then
                        Throw New InvalidOperationException("The requested product could not be updated because it no longer exists.")
                    End If
                End Using
            End Using
        End Sub

        Private Shared Function MapProduct(reader As SqlDataReader) As Product
            Dim product As New Product() With {
                .Id = reader.GetInt32(reader.GetOrdinal("ProductId")),
                .SparePartId = reader.GetInt32(reader.GetOrdinal("SparePartsId")),
                .SkuCode = reader.GetString(reader.GetOrdinal("SKU_CODE")),
                .DisplayName = reader.GetString(reader.GetOrdinal("DisplayName")),
                .Category = reader.GetString(reader.GetOrdinal("Category")),
                .Brand = reader.GetString(reader.GetOrdinal("Brand")),
                .ImageUrl = reader.GetString(reader.GetOrdinal("ImageUrl")),
                .UnitPrice = reader.GetDecimal(reader.GetOrdinal("RetailPrice")),
                .TaxRate = reader.GetDecimal(reader.GetOrdinal("TaxRate")),
                .Description = reader.GetString(reader.GetOrdinal("Description")),
                .StockQuantity = reader.GetInt32(reader.GetOrdinal("StockQuantity")),
                .MinStockThreshold = reader.GetInt32(reader.GetOrdinal("MinStockThreshold"))
            }
            Return product
        End Function

        Private Shared Sub InsertSku(connection As SqlConnection, transaction As SqlTransaction, sparePartId As Integer, sku As ProductSkuRequest, request As ProductCreateRequest)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"INSERT INTO dbo.TBL_SP_PSO_SKU
(
    SparePartsId,
    SKU_CODE,
    DisplayName,
    DisplayDescription,
    DefaultImageUrl,
    RetailPrice,
    TaxRate,
    StockQuantity,
    MinStockThreshold,
    IsActive
)
VALUES
(
    @SparePartId,
    @SkuCode,
    @DisplayName,
    @Description,
    @ImageUrl,
    @RetailPrice,
    @TaxRate,
    @StockQuantity,
    @MinStockThreshold,
    1
);"
                command.Parameters.AddWithValue("@SparePartId", sparePartId)
                command.Parameters.AddWithValue("@SkuCode", sku.SkuCode.Trim())
                command.Parameters.AddWithValue("@DisplayName", Coalesce(sku.DisplayName, request.DisplayName, request.Description, sku.SkuCode))
                command.Parameters.AddWithValue("@Description", Coalesce(sku.Description, request.Description, sku.DisplayName, request.DisplayName))
                command.Parameters.AddWithValue("@ImageUrl", ToDbValue(sku.ImageUrl))
                command.Parameters.AddWithValue("@RetailPrice", Math.Max(0D, sku.RetailPrice))
                command.Parameters.AddWithValue("@TaxRate", Math.Max(0D, sku.TaxRate))
                command.Parameters.AddWithValue("@StockQuantity", Math.Max(0, sku.StockQuantity))
                command.Parameters.AddWithValue("@MinStockThreshold", Math.Max(0, sku.MinStockThreshold))

                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function Coalesce(ParamArray values() As String) As String
            For Each value As String In values
                If Not String.IsNullOrWhiteSpace(value) Then
                    Return value.Trim()
                End If
            Next
            Return String.Empty
        End Function

        Private Shared Function ToDbValue(value As String) As Object
            If String.IsNullOrWhiteSpace(value) Then
                Return DBNull.Value
            End If
            Return value.Trim()
        End Function
    End Class
End Namespace
