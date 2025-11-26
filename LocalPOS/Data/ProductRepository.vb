Imports System.Data.SqlClient
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
        sku.StockQuantity
FROM dbo.SPARE_PARTS sp
INNER JOIN dbo.TBL_SP_PSO_SKU sku ON sku.SparePartsId = sp.ID
WHERE sku.IsActive = 1
  AND (@Category IS NULL OR sp.Category = @Category)
  AND (
        @SearchTerm IS NULL OR
        sp.Description LIKE '%' + @SearchTerm + '%' OR
        sp.SP_CODE LIKE '%' + @SearchTerm + '%' OR
        sku.SKU_CODE LIKE '%' + @SearchTerm + '%'
      )
ORDER BY sku.DisplayName"
                    command.Parameters.AddWithValue("@Category", If(String.IsNullOrWhiteSpace(category), CType(DBNull.Value, Object), category))
                    command.Parameters.AddWithValue("@SearchTerm", If(String.IsNullOrWhiteSpace(searchTerm), CType(DBNull.Value, Object), searchTerm))

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
        sku.StockQuantity
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
                .StockQuantity = reader.GetInt32(reader.GetOrdinal("StockQuantity"))
            }
            Return product
        End Function
    End Class
End Namespace
