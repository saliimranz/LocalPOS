Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Globalization
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Data
    Public Class OrderRepository
        Inherits SqlRepositoryBase

        Public Function CreateOrder(request As CheckoutRequest) As CheckoutResult
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If request.CartItems Is Nothing OrElse request.CartItems.Count = 0 Then
                Throw New InvalidOperationException("Cart is empty.")
            End If

            Dim orderNumber = GenerateOrderNumber()
            Dim inquiryNo = $"INQ-{DateTime.UtcNow:yyyyMMddHHmmss}"
            Dim paidAmount = ResolvePaidAmount(request)
            Dim outstanding = Math.Max(0D, request.TotalDue - paidAmount)
            Dim paymentReference = $"PMT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"

            Using connection = CreateConnection()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim orderId = InsertOrder(connection, transaction, request, orderNumber, inquiryNo, outstanding)
                        InsertOrderItems(connection, transaction, orderId, request)
                        Dim paymentNotes = BuildPaymentNotes(request, outstanding)
                        InsertPayment(connection, transaction, orderId, paymentReference, request.PaymentMethod, paidAmount, outstanding, request.CreatedBy, paymentNotes)
                        transaction.Commit()

                        Return New CheckoutResult() With {
                            .OrderId = orderId,
                            .OrderNumber = orderNumber,
                            .ReceiptNumber = paymentReference,
                            .RemainingBalance = outstanding
                        }
                    Catch ex As Exception
                        transaction.Rollback()
                        Throw
                    End Try
                End Using
            End Using
        End Function

        Private Shared Function InsertOrder(connection As SqlConnection, transaction As SqlTransaction, request As CheckoutRequest, orderNumber As String, inquiryNo As String, outstanding As Decimal) As Integer
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                Dim orderStatus = If(outstanding > 0D, "Pending Payment", "Completed")
                command.CommandText =
"INSERT INTO dbo.TBL_SP_PSO_ORDER
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
OUTPUT INSERTED.ID
VALUES
(
    @Inquiry,
    @OrderNumber,
    @DealerId,
    'POS',
    @Contact,
    1,
    @OrderStatus,
    GETDATE(),
    'Retail',
    @TotalDue,
    CASE WHEN @Outstanding > 0 THEN 0 ELSE 1 END,
    @UserId,
    GETDATE(),
    GETDATE()
)"
                command.Parameters.AddWithValue("@Inquiry", inquiryNo)
                command.Parameters.AddWithValue("@OrderNumber", orderNumber)
                command.Parameters.AddWithValue("@DealerId", If(request.DealerId = 0, CType(DBNull.Value, Object), request.DealerId))
                command.Parameters.AddWithValue("@Contact", request.DealerName)
                command.Parameters.AddWithValue("@OrderStatus", orderStatus)
                command.Parameters.AddWithValue("@TotalDue", request.TotalDue)
                command.Parameters.AddWithValue("@Outstanding", outstanding)
                command.Parameters.AddWithValue("@UserId", request.CreatedBy)

                Dim newId = Convert.ToInt32(command.ExecuteScalar())
                Return newId
            End Using
        End Function

        Private Shared Sub InsertOrderItems(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer, request As CheckoutRequest)
            For Each cartItem In request.CartItems
                Using command = connection.CreateCommand()
                    command.Transaction = transaction
                    command.CommandText =
"INSERT INTO dbo.TBL_SP_PSO_ITEM
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
VALUES
(
    @OrderId,
    @ItemId,
    @ItemName,
    @Qty,
    @TotalAmount,
    @UnitPrice,
    @TaxRate,
    1,
    'Completed',
    GETDATE()
)"
                    command.Parameters.AddWithValue("@OrderId", orderId)
                    command.Parameters.AddWithValue("@ItemId", cartItem.ProductId)
                    command.Parameters.AddWithValue("@ItemName", cartItem.Name)
                    command.Parameters.AddWithValue("@Qty", cartItem.Quantity)
                    command.Parameters.AddWithValue("@TotalAmount", cartItem.LineTotal)
                    command.Parameters.AddWithValue("@UnitPrice", cartItem.UnitPrice)
                    command.Parameters.AddWithValue("@TaxRate", cartItem.TaxRate)
                    command.ExecuteNonQuery()
                End Using
                UpdateSkuQuantity(connection, transaction, cartItem.ProductId, cartItem.Quantity)
            Next
        End Sub

        Private Shared Sub InsertPayment(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer, paymentReference As String, method As String, paidAmount As Decimal, outstanding As Decimal, createdBy As String, notes As String)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"INSERT INTO dbo.TBL_SP_PSO_PAYMENT_2
(
    ORDER_ID,
    PAYMENT_REFERENCE,
    PAYMENT_METHOD,
    PAID_AMOUNT,
    OUTSTANDING,
    IS_PARTIAL,
    CREATED_BY,
    NOTES
)
VALUES
(
    @OrderId,
    @Reference,
    @Method,
    @PaidAmount,
    @Outstanding,
    CASE WHEN @Outstanding > 0 THEN 1 ELSE 0 END,
    @CreatedBy,
    @Notes
)"
                command.Parameters.AddWithValue("@OrderId", orderId)
                command.Parameters.AddWithValue("@Reference", paymentReference)
                command.Parameters.AddWithValue("@Method", method)
                command.Parameters.AddWithValue("@PaidAmount", paidAmount)
                command.Parameters.AddWithValue("@Outstanding", outstanding)
                command.Parameters.AddWithValue("@CreatedBy", createdBy)
                If String.IsNullOrWhiteSpace(notes) Then
                    command.Parameters.AddWithValue("@Notes", CType(DBNull.Value, Object))
                Else
                    command.Parameters.AddWithValue("@Notes", notes)
                End If
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function GenerateOrderNumber() As String
            Return $"POS-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
        End Function

        Private Shared Sub UpdateSkuQuantity(connection As SqlConnection, transaction As SqlTransaction, skuId As Integer, quantitySold As Integer)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"UPDATE dbo.TBL_SP_PSO_SKU
SET StockQuantity = CASE WHEN StockQuantity >= @Qty THEN StockQuantity - @Qty ELSE 0 END,
    LastUpdated = GETDATE()
WHERE ID = @SkuId"
                command.Parameters.AddWithValue("@Qty", quantitySold)
                command.Parameters.AddWithValue("@SkuId", skuId)
                Dim affected = command.ExecuteNonQuery()
                If affected = 0 Then
                    Throw New InvalidOperationException($"Unable to update inventory for SKU ID {skuId}.")
                End If
            End Using
        End Sub

        Private Shared Function BuildPaymentNotes(request As CheckoutRequest, outstanding As Decimal) As String
            Dim notes As New List(Of String) From {
                $"Method:{request.PaymentMethod}"
            }

            If Not String.IsNullOrWhiteSpace(request.CorporatePaymentType) Then
                notes.Add($"Type:{request.CorporatePaymentType}")
            End If

            notes.Add($"VAT:{request.TaxPercent.ToString("F2", CultureInfo.InvariantCulture)}%")
            notes.Add($"Total:{request.TotalDue.ToString("F2", CultureInfo.InvariantCulture)}")
            notes.Add($"Paid:{request.PaymentAmount.ToString("F2", CultureInfo.InvariantCulture)}")

            If outstanding > 0D Then
                notes.Add($"Outstanding:{outstanding.ToString("F2", CultureInfo.InvariantCulture)}")
            End If

            If request.PaymentMethod = "Cash" Then
                If request.CashReceived.HasValue Then
                    notes.Add($"CashReceived:{request.CashReceived.Value.ToString("F2", CultureInfo.InvariantCulture)}")
                End If
                If request.CashChange.HasValue Then
                    notes.Add($"Change:{request.CashChange.Value.ToString("F2", CultureInfo.InvariantCulture)}")
                End If
            ElseIf request.PaymentMethod = "Card" Then
                If Not String.IsNullOrWhiteSpace(request.CardRrn) Then
                    notes.Add($"RRN:{request.CardRrn}")
                End If
                If Not String.IsNullOrWhiteSpace(request.CardAuthCode) Then
                    notes.Add($"Auth:{request.CardAuthCode}")
                End If
                If Not String.IsNullOrWhiteSpace(request.CardStatus) Then
                    notes.Add($"Status:{request.CardStatus}")
                End If
            End If

            Dim formatted = String.Join(" | ", notes)
            If formatted.Length > 480 Then
                formatted = formatted.Substring(0, 480)
            End If
            Return formatted
        End Function

        Private Shared Function ResolvePaidAmount(request As CheckoutRequest) As Decimal
            Dim amount = request.PaymentAmount
            If amount <= 0D Then
                Throw New InvalidOperationException("Payment amount must be greater than zero.")
            End If
            If amount > request.TotalDue Then
                Throw New InvalidOperationException("Payment amount cannot exceed total due.")
            End If
            Return amount
        End Function
    End Class
End Namespace
