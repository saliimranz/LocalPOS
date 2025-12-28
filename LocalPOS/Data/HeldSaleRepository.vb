Imports System
Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Linq
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Data
    Public Class HeldSaleRepository
        Inherits SqlRepositoryBase

        Public Function SaveHeldSale(request As HoldSaleRequest) As Integer
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If request.Items Is Nothing OrElse request.Items.Count = 0 Then
                Throw New InvalidOperationException("Cannot hold an empty cart.")
            End If

            Using connection = CreateConnection()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim heldSaleId As Integer
                        If request.HeldSaleId.HasValue AndAlso request.HeldSaleId.Value > 0 Then
                            heldSaleId = request.HeldSaleId.Value
                            UpdateHeldSaleHeader(connection, transaction, heldSaleId, request, False)
                            DeleteHeldSaleItems(connection, transaction, heldSaleId)
                            InsertHeldSaleItems(connection, transaction, heldSaleId, request.Items)
                            PersistHeldSaleDiscounts(connection, transaction, heldSaleId, request.Discounts, request.CreatedBy)
                        Else
                            heldSaleId = InsertHeldSale(connection, transaction, request)
                            InsertHeldSaleItems(connection, transaction, heldSaleId, request.Items)
                            PersistHeldSaleDiscounts(connection, transaction, heldSaleId, request.Discounts, request.CreatedBy)
                        End If

                        transaction.Commit()
                        Return heldSaleId
                    Catch
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Function

        Public Function GetHeldSales() As IList(Of HeldSaleSummary)
            Dim results As New List(Of HeldSaleSummary)()
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT hs.ID,
        hs.HOLD_REFERENCE,
        ISNULL(hs.DEALER_NAME, 'Walk-in Customer') AS DEALER_NAME,
        ISNULL(hs.DEALER_ID, 0) AS DEALER_ID,
        hs.ITEMS_COUNT,
        hs.SUBTOTAL,
        hs.TOTAL_AMOUNT,
        hs.DISCOUNT_PERCENT,
        hs.CREATED_ON,
        ISNULL(hs.CREATED_BY, '') AS CREATED_BY
FROM dbo.TBL_POS_HELD_SALE hs
ORDER BY hs.CREATED_ON DESC, hs.ID DESC"

                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            results.Add(New HeldSaleSummary() With {
                                .HeldSaleId = reader.GetInt32(reader.GetOrdinal("ID")),
                                .ReferenceCode = reader.GetString(reader.GetOrdinal("HOLD_REFERENCE")),
                                .DealerName = reader.GetString(reader.GetOrdinal("DEALER_NAME")),
                                .ItemsCount = reader.GetInt32(reader.GetOrdinal("ITEMS_COUNT")),
                                .Subtotal = reader.GetDecimal(reader.GetOrdinal("SUBTOTAL")),
                                .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_AMOUNT")),
                                .DiscountPercent = reader.GetDecimal(reader.GetOrdinal("DISCOUNT_PERCENT")),
                                .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CREATED_ON")),
                                .CreatedBy = reader.GetString(reader.GetOrdinal("CREATED_BY"))
                            })
                        End While
                    End Using
                End Using
            End Using
            Return results
        End Function

        Public Function GetHeldSale(heldSaleId As Integer) As HeldSaleDetail
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT TOP 1
        hs.ID,
        hs.HOLD_REFERENCE,
        ISNULL(hs.DEALER_ID, 0) AS DEALER_ID,
        ISNULL(hs.DEALER_NAME, 'Walk-in Customer') AS DEALER_NAME,
        hs.DISCOUNT_PERCENT,
        hs.DISCOUNT_AMOUNT,
        hs.TAX_PERCENT,
        hs.TAX_AMOUNT,
        hs.SUBTOTAL,
        hs.TOTAL_AMOUNT,
        hs.CREATED_ON,
        ISNULL(hs.CREATED_BY, '') AS CREATED_BY
FROM dbo.TBL_POS_HELD_SALE hs
WHERE hs.ID = @Id"
                    command.Parameters.AddWithValue("@Id", heldSaleId)

                    Using reader = command.ExecuteReader()
                        If Not reader.Read() Then
                            Return Nothing
                        End If

                        Dim detail As New HeldSaleDetail() With {
                            .heldSaleId = reader.GetInt32(reader.GetOrdinal("ID")),
                            .ReferenceCode = reader.GetString(reader.GetOrdinal("HOLD_REFERENCE")),
                            .DealerId = reader.GetInt32(reader.GetOrdinal("DEALER_ID")),
                            .DealerName = reader.GetString(reader.GetOrdinal("DEALER_NAME")),
                            .DiscountPercent = reader.GetDecimal(reader.GetOrdinal("DISCOUNT_PERCENT")),
                            .DiscountAmount = reader.GetDecimal(reader.GetOrdinal("DISCOUNT_AMOUNT")),
                            .TaxPercent = reader.GetDecimal(reader.GetOrdinal("TAX_PERCENT")),
                            .TaxAmount = reader.GetDecimal(reader.GetOrdinal("TAX_AMOUNT")),
                            .Subtotal = reader.GetDecimal(reader.GetOrdinal("SUBTOTAL")),
                            .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_AMOUNT")),
                            .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CREATED_ON")),
                            .CreatedBy = reader.GetString(reader.GetOrdinal("CREATED_BY")),
                            .Items = New List(Of HeldSaleItem)(),
                            .Discounts = New List(Of CheckoutDiscount)()
                        }

                        reader.Close()
                        FillHeldSaleItems(connection, heldSaleId, CType(detail.Items, List(Of HeldSaleItem)))
                        detail.Discounts = LoadHeldSaleDiscounts(connection, Nothing, heldSaleId, detail)
                        Return detail
                    End Using
                End Using
            End Using
        End Function

        Private Shared Sub PersistHeldSaleDiscounts(connection As SqlConnection,
                                                   transaction As SqlTransaction,
                                                   heldSaleId As Integer,
                                                   discounts As IList(Of CheckoutDiscount),
                                                   createdBy As String)
            If heldSaleId <= 0 OrElse connection Is Nothing Then
                Return
            End If

            If Not TableExists(connection, transaction, "dbo", "TBL_POS_HELD_SALE_DISCOUNT") Then
                Return
            End If

            ' Replace-all for simplicity (held sale is mutable draft state).
            Using deleteCmd = connection.CreateCommand()
                deleteCmd.Transaction = transaction
                deleteCmd.CommandText = "DELETE FROM dbo.TBL_POS_HELD_SALE_DISCOUNT WHERE HELD_SALE_ID = @Id"
                deleteCmd.Parameters.AddWithValue("@Id", heldSaleId)
                deleteCmd.ExecuteNonQuery()
            End Using

            If discounts Is Nothing OrElse discounts.Count = 0 Then
                Return
            End If

            For Each d In discounts
                If d Is Nothing OrElse String.IsNullOrWhiteSpace(d.Scope) OrElse String.IsNullOrWhiteSpace(d.ValueType) Then
                    Continue For
                End If

                Using insertCmd = connection.CreateCommand()
                    insertCmd.Transaction = transaction
                    insertCmd.CommandText =
"INSERT INTO dbo.TBL_POS_HELD_SALE_DISCOUNT
(
    HELD_SALE_ID,
    PRODUCT_ID,
    SCOPE,
    VALUE_TYPE,
    VALUE,
    SOURCE,
    REFERENCE,
    DESCRIPTION,
    PRIORITY,
    IS_STACKABLE,
    CREATED_BY
)
VALUES
(
    @HeldSaleId,
    @ProductId,
    @Scope,
    @ValueType,
    @Value,
    @Source,
    @Reference,
    @Description,
    @Priority,
    @IsStackable,
    @CreatedBy
)"
                    insertCmd.Parameters.AddWithValue("@HeldSaleId", heldSaleId)
                    If d.TargetProductId.HasValue AndAlso d.TargetProductId.Value > 0 Then
                        insertCmd.Parameters.AddWithValue("@ProductId", d.TargetProductId.Value)
                    Else
                        insertCmd.Parameters.AddWithValue("@ProductId", CType(DBNull.Value, Object))
                    End If
                    insertCmd.Parameters.AddWithValue("@Scope", d.Scope.Trim().ToUpperInvariant())
                    insertCmd.Parameters.AddWithValue("@ValueType", d.ValueType.Trim().ToUpperInvariant())
                    insertCmd.Parameters.AddWithValue("@Value", Decimal.Round(Math.Max(0D, d.Value), 4, MidpointRounding.AwayFromZero))
                    insertCmd.Parameters.AddWithValue("@Source", If(String.IsNullOrWhiteSpace(d.Source), "Unknown", d.Source.Trim()))
                    insertCmd.Parameters.AddWithValue("@Reference", ToDbValue(d.Reference))
                    insertCmd.Parameters.AddWithValue("@Description", ToDbValue(d.Description))
                    insertCmd.Parameters.AddWithValue("@Priority", d.Priority)
                    insertCmd.Parameters.AddWithValue("@IsStackable", d.IsStackable)
                    insertCmd.Parameters.AddWithValue("@CreatedBy", ToDbValue(createdBy))
                    insertCmd.ExecuteNonQuery()
                End Using
            Next
        End Sub

        Private Shared Function LoadHeldSaleDiscounts(connection As SqlConnection,
                                                    transaction As SqlTransaction,
                                                    heldSaleId As Integer,
                                                    fallback As HeldSaleDetail) As IList(Of CheckoutDiscount)
            Dim results As New List(Of CheckoutDiscount)()
            If heldSaleId <= 0 OrElse connection Is Nothing Then
                Return results
            End If

            If TableExists(connection, transaction, "dbo", "TBL_POS_HELD_SALE_DISCOUNT") Then
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText =
"SELECT
    ISNULL(SCOPE, '') AS SCOPE,
    ISNULL(VALUE_TYPE, '') AS VALUE_TYPE,
    ISNULL(VALUE, 0) AS VALUE,
    ISNULL(SOURCE, '') AS SOURCE,
    ISNULL(REFERENCE, '') AS REFERENCE,
    ISNULL(DESCRIPTION, '') AS DESCRIPTION,
    ISNULL(PRIORITY, 0) AS PRIORITY,
    ISNULL(IS_STACKABLE, 1) AS IS_STACKABLE,
    ISNULL(PRODUCT_ID, 0) AS PRODUCT_ID
FROM dbo.TBL_POS_HELD_SALE_DISCOUNT
WHERE HELD_SALE_ID = @Id
ORDER BY ID ASC"
                    command.Parameters.AddWithValue("@Id", heldSaleId)
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            Dim productId = reader.GetInt32(reader.GetOrdinal("PRODUCT_ID"))
                            Dim discount As New CheckoutDiscount() With {
                                .Scope = reader.GetString(reader.GetOrdinal("SCOPE")),
                                .ValueType = reader.GetString(reader.GetOrdinal("VALUE_TYPE")),
                                .Value = reader.GetDecimal(reader.GetOrdinal("VALUE")),
                                .Source = reader.GetString(reader.GetOrdinal("SOURCE")),
                                .Reference = reader.GetString(reader.GetOrdinal("REFERENCE")),
                                .Description = reader.GetString(reader.GetOrdinal("DESCRIPTION")),
                                .Priority = reader.GetInt32(reader.GetOrdinal("PRIORITY")),
                                .IsStackable = reader.GetBoolean(reader.GetOrdinal("IS_STACKABLE"))
                            }
                            If productId > 0 Then
                                discount.TargetProductId = productId
                            End If
                            results.Add(discount)
                        End While
                    End Using
                End Using
            End If

            If results.Count = 0 AndAlso fallback IsNot Nothing AndAlso fallback.DiscountPercent > 0D Then
                ' Backwards compatibility with legacy held sale header columns.
                results.Add(New CheckoutDiscount() With {
                    .Scope = "SUBTOTAL",
                    .ValueType = "PERCENT",
                    .Value = fallback.DiscountPercent,
                    .Source = "Legacy",
                    .Reference = String.Empty,
                    .Description = "Legacy cart discount",
                    .Priority = 0,
                    .IsStackable = True
                })
            End If

            Return results
        End Function

        Private Shared Function TableExists(connection As SqlConnection, transaction As SqlTransaction, schemaName As String, tableName As String) As Boolean
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "SELECT CASE WHEN OBJECT_ID(@FullName, 'U') IS NULL THEN 0 ELSE 1 END"
                command.Parameters.AddWithValue("@FullName", $"{schemaName}.{tableName}")
                Dim raw = command.ExecuteScalar()
                If raw Is Nothing OrElse raw Is DBNull.Value Then
                    Return False
                End If
                Return Convert.ToInt32(raw) = 1
            End Using
        End Function

        Public Sub DeleteHeldSale(heldSaleId As Integer)
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "DELETE FROM dbo.TBL_POS_HELD_SALE WHERE ID = @Id"
                    command.Parameters.AddWithValue("@Id", heldSaleId)
                    command.ExecuteNonQuery()
                End Using
            End Using
        End Sub

        Private Shared Function InsertHeldSale(connection As SqlConnection, transaction As SqlTransaction, request As HoldSaleRequest) As Integer
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"INSERT INTO dbo.TBL_POS_HELD_SALE
(
    HOLD_REFERENCE,
    DEALER_ID,
    DEALER_NAME,
    ITEMS_COUNT,
    SUBTOTAL,
    DISCOUNT_PERCENT,
    DISCOUNT_AMOUNT,
    TAX_PERCENT,
    TAX_AMOUNT,
    TOTAL_AMOUNT,
    CREATED_BY
)
OUTPUT INSERTED.ID
VALUES
(
    @Reference,
    @DealerId,
    @DealerName,
    @ItemsCount,
    @Subtotal,
    @DiscountPercent,
    @DiscountAmount,
    @TaxPercent,
    @TaxAmount,
    @Total,
    @CreatedBy
)"

                command.Parameters.AddWithValue("@Reference", GenerateReference())
                command.Parameters.AddWithValue("@DealerId", If(request.DealerId <= 0, CType(DBNull.Value, Object), request.DealerId))
                command.Parameters.AddWithValue("@DealerName", If(String.IsNullOrWhiteSpace(request.DealerName), "Walk-in Customer", request.DealerName.Trim()))
                command.Parameters.AddWithValue("@ItemsCount", request.Items.Sum(Function(i) i.Quantity))
                command.Parameters.AddWithValue("@Subtotal", request.Subtotal)
                command.Parameters.AddWithValue("@DiscountPercent", request.DiscountPercent)
                command.Parameters.AddWithValue("@DiscountAmount", request.DiscountAmount)
                command.Parameters.AddWithValue("@TaxPercent", request.TaxPercent)
                command.Parameters.AddWithValue("@TaxAmount", request.TaxAmount)
                command.Parameters.AddWithValue("@Total", request.TotalAmount)
                command.Parameters.AddWithValue("@CreatedBy", ToDbValue(request.CreatedBy))

                Return Convert.ToInt32(command.ExecuteScalar())
            End Using
        End Function

        Private Shared Sub UpdateHeldSaleHeader(connection As SqlConnection, transaction As SqlTransaction, heldSaleId As Integer, request As HoldSaleRequest, updateTimestamp As Boolean)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"UPDATE dbo.TBL_POS_HELD_SALE
SET DEALER_ID = @DealerId,
    DEALER_NAME = @DealerName,
    ITEMS_COUNT = @ItemsCount,
    SUBTOTAL = @Subtotal,
    DISCOUNT_PERCENT = @DiscountPercent,
    DISCOUNT_AMOUNT = @DiscountAmount,
    TAX_PERCENT = @TaxPercent,
    TAX_AMOUNT = @TaxAmount,
    TOTAL_AMOUNT = @Total" &
If(updateTimestamp, ",
    CREATED_ON = GETDATE()", "") &
"
WHERE ID = @Id"
                command.Parameters.AddWithValue("@DealerId", If(request.DealerId <= 0, CType(DBNull.Value, Object), request.DealerId))
                command.Parameters.AddWithValue("@DealerName", If(String.IsNullOrWhiteSpace(request.DealerName), "Walk-in Customer", request.DealerName.Trim()))
                command.Parameters.AddWithValue("@ItemsCount", request.Items.Sum(Function(i) i.Quantity))
                command.Parameters.AddWithValue("@Subtotal", request.Subtotal)
                command.Parameters.AddWithValue("@DiscountPercent", request.DiscountPercent)
                command.Parameters.AddWithValue("@DiscountAmount", request.DiscountAmount)
                command.Parameters.AddWithValue("@TaxPercent", request.TaxPercent)
                command.Parameters.AddWithValue("@TaxAmount", request.TaxAmount)
                command.Parameters.AddWithValue("@Total", request.TotalAmount)
                command.Parameters.AddWithValue("@Id", heldSaleId)
                Dim affected = command.ExecuteNonQuery()
                If affected = 0 Then
                    Throw New InvalidOperationException("The held sale no longer exists.")
                End If
            End Using
        End Sub

        Private Shared Sub DeleteHeldSaleItems(connection As SqlConnection, transaction As SqlTransaction, heldSaleId As Integer)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "DELETE FROM dbo.TBL_POS_HELD_SALE_ITEM WHERE HELD_SALE_ID = @Id"
                command.Parameters.AddWithValue("@Id", heldSaleId)
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Sub InsertHeldSaleItems(connection As SqlConnection, transaction As SqlTransaction, heldSaleId As Integer, items As IEnumerable(Of CartItem))
            Dim hasListUnitPriceColumn = ColumnExists(connection, transaction, "dbo", "TBL_POS_HELD_SALE_ITEM", "LIST_UNIT_PRICE")
            For Each item In items
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText =
"INSERT INTO dbo.TBL_POS_HELD_SALE_ITEM
(
    HELD_SALE_ID,
    PRODUCT_ID,
    SKU_CODE,
    ITEM_NAME,
    UNIT_PRICE,
    " & If(hasListUnitPriceColumn, "LIST_UNIT_PRICE," & vbCrLf & "    ", String.Empty) &
    QUANTITY,
    TAX_RATE,
    LINE_TOTAL,
    THUMBNAIL_URL
)
VALUES
(
    @HeldSaleId,
    @ProductId,
    @SkuCode,
    @Name,
    @UnitPrice,
    " & If(hasListUnitPriceColumn, "@ListUnitPrice," & vbCrLf & "    ", String.Empty) &
    @Quantity,
    @TaxRate,
    @LineTotal,
    @Thumbnail
)"
                    command.Parameters.AddWithValue("@HeldSaleId", heldSaleId)
                    command.Parameters.AddWithValue("@ProductId", item.ProductId)
                    command.Parameters.AddWithValue("@SkuCode", ToDbValue(item.SkuCode))
                    command.Parameters.AddWithValue("@Name", item.Name)
                    command.Parameters.AddWithValue("@UnitPrice", item.UnitPrice)
                    If hasListUnitPriceColumn Then
                        Dim listPrice = item.ListUnitPrice
                        If listPrice <= 0D Then
                            listPrice = item.UnitPrice
                        End If
                        command.Parameters.AddWithValue("@ListUnitPrice", Decimal.Round(listPrice, 4, MidpointRounding.AwayFromZero))
                    End If
                    command.Parameters.AddWithValue("@Quantity", item.Quantity)
                    command.Parameters.AddWithValue("@TaxRate", item.TaxRate)
                    command.Parameters.AddWithValue("@LineTotal", item.LineTotal)
                    command.Parameters.AddWithValue("@Thumbnail", ToDbValue(item.Thumbnail))
                    command.ExecuteNonQuery()
                End Using
            Next
        End Sub

        Private Shared Sub FillHeldSaleItems(connection As SqlConnection, heldSaleId As Integer, buffer As IList(Of HeldSaleItem))
            Using command = connection.CreateCommand()
                Dim hasListUnitPriceColumn = ColumnExists(connection, Nothing, "dbo", "TBL_POS_HELD_SALE_ITEM", "LIST_UNIT_PRICE")
                command.CommandText =
"SELECT
    HELD_SALE_ID,
    PRODUCT_ID,
    ISNULL(SKU_CODE, '') AS SKU_CODE,
    ITEM_NAME,
    UNIT_PRICE,
    " & If(hasListUnitPriceColumn, "LIST_UNIT_PRICE," & vbCrLf, String.Empty) &
    QUANTITY,
    TAX_RATE,
    LINE_TOTAL,
    ISNULL(THUMBNAIL_URL, '') AS THUMBNAIL_URL
FROM dbo.TBL_POS_HELD_SALE_ITEM
WHERE HELD_SALE_ID = @HeldSaleId
ORDER BY ID ASC"
                command.Parameters.AddWithValue("@HeldSaleId", heldSaleId)

                Using reader = command.ExecuteReader()
                    While reader.Read()
                        Dim listUnitPrice As Decimal? = Nothing
                        If hasListUnitPriceColumn Then
                            Dim ordinal = reader.GetOrdinal("LIST_UNIT_PRICE")
                            If Not reader.IsDBNull(ordinal) Then
                                listUnitPrice = Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture)
                            End If
                        End If
                        buffer.Add(New HeldSaleItem() With {
                            .heldSaleId = reader.GetInt32(reader.GetOrdinal("HELD_SALE_ID")),
                            .ProductId = reader.GetInt32(reader.GetOrdinal("PRODUCT_ID")),
                            .SkuCode = reader.GetString(reader.GetOrdinal("SKU_CODE")),
                            .Name = reader.GetString(reader.GetOrdinal("ITEM_NAME")),
                            .ListUnitPrice = listUnitPrice,
                            .UnitPrice = reader.GetDecimal(reader.GetOrdinal("UNIT_PRICE")),
                            .Quantity = reader.GetInt32(reader.GetOrdinal("QUANTITY")),
                            .TaxRate = reader.GetDecimal(reader.GetOrdinal("TAX_RATE")),
                            .LineTotal = reader.GetDecimal(reader.GetOrdinal("LINE_TOTAL")),
                            .ThumbnailUrl = reader.GetString(reader.GetOrdinal("THUMBNAIL_URL"))
                        })
                    End While
                End Using
            End Using
        End Sub

        Private Shared Function GenerateReference() As String
            Return $"HLD-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
        End Function

        Private Shared Function ToDbValue(value As String) As Object
            If String.IsNullOrWhiteSpace(value) Then
                Return DBNull.Value
            End If
            Return value.Trim()
        End Function

        Private Shared Function ColumnExists(connection As SqlConnection, transaction As SqlTransaction, schemaName As String, tableName As String, columnName As String) As Boolean
            If connection Is Nothing OrElse String.IsNullOrWhiteSpace(schemaName) OrElse String.IsNullOrWhiteSpace(tableName) OrElse String.IsNullOrWhiteSpace(columnName) Then
                Return False
            End If

            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText = "SELECT CASE WHEN COL_LENGTH(@FullName, @ColumnName) IS NULL THEN 0 ELSE 1 END"
                command.Parameters.AddWithValue("@FullName", $"{schemaName}.{tableName}")
                command.Parameters.AddWithValue("@ColumnName", columnName)
                Dim raw = command.ExecuteScalar()
                If raw Is Nothing OrElse raw Is DBNull.Value Then
                    Return False
                End If
                Return Convert.ToInt32(raw, CultureInfo.InvariantCulture) = 1
            End Using
        End Function
    End Class
End Namespace
