Imports System.Data.SqlClient
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
            Dim paidAmount = CalculatePaidAmount(request)
            Dim outstanding = request.TotalDue - paidAmount
            Dim paymentReference = $"PMT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"

            Using connection = CreateConnection()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim orderId = InsertOrder(connection, transaction, request, orderNumber, inquiryNo, outstanding)
                        InsertOrderItems(connection, transaction, orderId, request)
                        InsertPayment(connection, transaction, orderId, paymentReference, request.PaymentMethod, paidAmount, outstanding, request.CreatedBy)
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
    'Completed',
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
            Next
        End Sub

        Private Shared Sub InsertPayment(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer, paymentReference As String, method As String, paidAmount As Decimal, outstanding As Decimal, createdBy As String)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"INSERT INTO dbo.TBL_SP_PSO_PAYMENT
(
    ORDER_ID,
    PAYMENT_REFERENCE,
    PAYMENT_METHOD,
    PAID_AMOUNT,
    OUTSTANDING,
    IS_PARTIAL,
    CREATED_BY
)
VALUES
(
    @OrderId,
    @Reference,
    @Method,
    @PaidAmount,
    @Outstanding,
    CASE WHEN @Outstanding > 0 THEN 1 ELSE 0 END,
    @CreatedBy
)"
                command.Parameters.AddWithValue("@OrderId", orderId)
                command.Parameters.AddWithValue("@Reference", paymentReference)
                command.Parameters.AddWithValue("@Method", method)
                command.Parameters.AddWithValue("@PaidAmount", paidAmount)
                command.Parameters.AddWithValue("@Outstanding", outstanding)
                command.Parameters.AddWithValue("@CreatedBy", createdBy)
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function GenerateOrderNumber() As String
            Return $"POS-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
        End Function

        Private Shared Function CalculatePaidAmount(request As CheckoutRequest) As Decimal
            Select Case request.PaymentMethod
                Case "Cash", "Card"
                    Return request.TotalDue
                Case "Credit"
                    Return 0D
                Case "Partial"
                    If Not request.PartialAmount.HasValue OrElse request.PartialAmount.Value <= 0D Then
                        Throw New InvalidOperationException("Partial payment amount is required.")
                    End If
                    If request.PartialAmount.Value > request.TotalDue Then
                        Throw New InvalidOperationException("Partial amount cannot exceed total due.")
                    End If
                    Return request.PartialAmount.Value
                Case Else
                    Throw New InvalidOperationException("Unsupported payment method.")
            End Select
        End Function
    End Class
End Namespace
