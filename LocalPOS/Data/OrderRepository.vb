Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Linq
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
                command.ExecuteNonQuery()
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

        Public Function GetCustomerOrders(dealerId As Integer) As IList(Of CustomerOrderSummary)
            Dim orders As New List(Of CustomerOrderSummary)()
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT 
    o.ID,
    o.SPPSOID,
    ISNULL(o.INQUIRY_NO, '') AS INQUIRY_NO,
    ISNULL(o.ORD_STATUS, 'Completed') AS ORD_STATUS,
    o.CDATED,
    ISNULL(o.TP, 0) AS TP,
    ISNULL(o.DID, 0) AS DEALER_ID,
    ISNULL(o.CONTACT, '') AS CONTACT,
    ISNULL(totals.TotalPaid, 0) AS TOTAL_PAID,
    ISNULL(lastPayment.OUTSTANDING, 0) AS OUTSTANDING_BALANCE,
    lastPayment.LAST_PAYMENT_ON
FROM dbo.TBL_SP_PSO_ORDER o
OUTER APPLY (
    SELECT SUM(p.PAID_AMOUNT) AS TotalPaid
    FROM dbo.TBL_SP_PSO_PAYMENT_2 p
    WHERE p.ORDER_ID = o.ID
) totals
OUTER APPLY (
    SELECT TOP 1 
        p.OUTSTANDING,
        p.CREATED_ON AS LAST_PAYMENT_ON
    FROM dbo.TBL_SP_PSO_PAYMENT_2 p
    WHERE p.ORDER_ID = o.ID
    ORDER BY p.CREATED_ON DESC, p.ID DESC
) lastPayment
WHERE ((@DealerId = 0 AND ISNULL(o.DID, 0) = 0) OR o.DID = @DealerId)
ORDER BY o.CDATED DESC"
                    command.Parameters.AddWithValue("@DealerId", dealerId)
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            Dim summary As New CustomerOrderSummary() With {
                                .OrderId = reader.GetInt32(reader.GetOrdinal("ID")),
                                .OrderNumber = reader.GetString(reader.GetOrdinal("SPPSOID")),
                                .InquiryNumber = reader.GetString(reader.GetOrdinal("INQUIRY_NO")),
                                .Status = reader.GetString(reader.GetOrdinal("ORD_STATUS")),
                                .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CDATED")),
                                .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TP")),
                                .DealerId = reader.GetInt32(reader.GetOrdinal("DEALER_ID")),
                                .DealerName = reader.GetString(reader.GetOrdinal("CONTACT")),
                                .TotalPaid = reader.GetDecimal(reader.GetOrdinal("TOTAL_PAID")),
                                .Outstanding = reader.GetDecimal(reader.GetOrdinal("OUTSTANDING_BALANCE"))
                            }
                            Dim lastPaymentOrdinal = reader.GetOrdinal("LAST_PAYMENT_ON")
                            If Not reader.IsDBNull(lastPaymentOrdinal) Then
                                summary.LastPaymentOn = reader.GetDateTime(lastPaymentOrdinal)
                            End If
                            orders.Add(summary)
                        End While
                    End Using
                End Using
            End Using
            Return orders
        End Function

        Public Function GetOrderById(orderId As Integer) As CustomerOrderSummary
            Using connection = CreateConnection()
                Return GetOrderById(connection, Nothing, orderId)
            End Using
        End Function

        Private Shared Function GetOrderById(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer) As CustomerOrderSummary
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"SELECT TOP 1
    o.ID,
    o.SPPSOID,
    ISNULL(o.INQUIRY_NO, '') AS INQUIRY_NO,
    ISNULL(o.ORD_STATUS, 'Completed') AS ORD_STATUS,
    o.CDATED,
    ISNULL(o.TP, 0) AS TP,
    ISNULL(o.DID, 0) AS DEALER_ID,
    ISNULL(o.CONTACT, '') AS CONTACT
FROM dbo.TBL_SP_PSO_ORDER o
WHERE o.ID = @OrderId"
                command.Parameters.AddWithValue("@OrderId", orderId)
                Using reader = command.ExecuteReader()
                    If reader.Read() Then
                        Return New CustomerOrderSummary() With {
                            .OrderId = reader.GetInt32(reader.GetOrdinal("ID")),
                            .OrderNumber = reader.GetString(reader.GetOrdinal("SPPSOID")),
                            .InquiryNumber = reader.GetString(reader.GetOrdinal("INQUIRY_NO")),
                            .Status = reader.GetString(reader.GetOrdinal("ORD_STATUS")),
                            .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CDATED")),
                            .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TP")),
                            .DealerId = reader.GetInt32(reader.GetOrdinal("DEALER_ID")),
                            .DealerName = reader.GetString(reader.GetOrdinal("CONTACT"))
                        }
                    End If
                End Using
            End Using
            Return Nothing
        End Function

        Public Function GetOrderPayments(orderId As Integer) As IList(Of OrderPaymentRecord)
            Dim payments As New List(Of OrderPaymentRecord)()
            Using connection = CreateConnection()
                FillPayments(connection, Nothing, orderId, payments)
            End Using
            Return payments
        End Function

        Private Shared Sub FillPayments(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer, buffer As IList(Of OrderPaymentRecord))
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"SELECT ID,
        ORDER_ID,
        ISNULL(PAYMENT_REFERENCE, '') AS PAYMENT_REFERENCE,
        PAYMENT_METHOD,
        PAID_AMOUNT,
        OUTSTANDING,
        IS_PARTIAL,
        CREATED_ON,
        ISNULL(NOTES, '') AS NOTES
FROM dbo.TBL_SP_PSO_PAYMENT_2
WHERE ORDER_ID = @OrderId
ORDER BY CREATED_ON ASC, ID ASC"
                command.Parameters.AddWithValue("@OrderId", orderId)
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        buffer.Add(New OrderPaymentRecord() With {
                            .PaymentId = reader.GetInt32(reader.GetOrdinal("ID")),
                            .OrderId = reader.GetInt32(reader.GetOrdinal("ORDER_ID")),
                            .PaymentReference = reader.GetString(reader.GetOrdinal("PAYMENT_REFERENCE")),
                            .PaymentMethod = reader.GetString(reader.GetOrdinal("PAYMENT_METHOD")),
                            .PaidAmount = reader.GetDecimal(reader.GetOrdinal("PAID_AMOUNT")),
                            .OutstandingAfterPayment = reader.GetDecimal(reader.GetOrdinal("OUTSTANDING")),
                            .IsPartial = reader.GetBoolean(reader.GetOrdinal("IS_PARTIAL")),
                            .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CREATED_ON")),
                            .Notes = reader.GetString(reader.GetOrdinal("NOTES"))
                        })
                    End While
                End Using
            End Using
        End Sub

        Public Function GetPendingPaymentContext(orderId As Integer) As PendingPaymentContext
            Using connection = CreateConnection()
                Return BuildPendingPaymentContext(connection, Nothing, orderId)
            End Using
        End Function

        Private Function BuildPendingPaymentContext(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer) As PendingPaymentContext
            Dim order = GetOrderById(connection, transaction, orderId)
            If order Is Nothing Then
                Return Nothing
            End If

            Dim payments As New List(Of OrderPaymentRecord)()
            FillPayments(connection, transaction, orderId, payments)
            If payments.Count = 0 Then
                Return Nothing
            End If

            Dim latest = payments.Last()
            Dim context As New PendingPaymentContext() With {
                .OrderId = order.OrderId,
                .OrderNumber = order.OrderNumber,
                .DealerId = order.DealerId,
                .DealerName = order.DealerName,
                .TotalAmount = order.TotalAmount,
                .OutstandingAmount = latest.OutstandingAfterPayment,
                .Payments = payments,
                .PreviouslyPaid = payments.Sum(Function(p) p.PaidAmount)
            }
            context.VatPercent = ParseVatPercent(latest.Notes)

            If context.OutstandingAmount <= 0D Then
                Return Nothing
            End If
            Return context
        End Function

        Public Function CompletePendingPayment(request As PendingPaymentRequest, context As PendingPaymentContext) As PendingPaymentResult
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If context Is Nothing Then Throw New ArgumentNullException(NameOf(context))
            If context.OutstandingAmount <= 0D Then
                Throw New InvalidOperationException("Order has no outstanding amount.")
            End If

            Using connection = CreateConnection()
                Using transaction = connection.BeginTransaction()
                    Dim lockedOutstanding = GetLockedOutstanding(connection, transaction, request.OrderId)
                    Dim expected = Decimal.Round(context.OutstandingAmount, 2, MidpointRounding.AwayFromZero)
                    Dim current = Decimal.Round(lockedOutstanding, 2, MidpointRounding.AwayFromZero)
                    If expected <> current Then
                        Throw New InvalidOperationException("Outstanding balance changed. Refresh the page and try again.")
                    End If

                    Dim paymentReference = $"PMT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"
                    request.PaymentAmount = context.OutstandingAmount

                    Dim notes = BuildSettlementNotes(request, context)
                    InsertPayment(connection, transaction, request.OrderId, paymentReference, request.PaymentMethod, request.PaymentAmount, 0D, request.CreatedBy, notes)
                    MarkOrderCompleted(connection, transaction, request.OrderId)

                    transaction.Commit()

                    Return New PendingPaymentResult() With {
                        .OrderId = request.OrderId,
                        .OrderNumber = context.OrderNumber,
                        .ReceiptNumber = paymentReference,
                        .DealerName = context.DealerName,
                        .TotalOrderAmount = context.TotalAmount,
                        .PreviouslyPaid = context.PreviouslyPaid,
                        .OutstandingBeforePayment = context.OutstandingAmount,
                        .SettledAmount = request.PaymentAmount,
                        .VatPercent = context.VatPercent,
                        .PaymentMethod = request.PaymentMethod,
                        .CardRrn = request.CardRrn,
                        .CardAuthCode = request.CardAuthCode,
                        .CashReceived = request.CashReceived,
                        .CashChange = request.CashChange,
                        .CashierName = request.CreatedBy,
                        .CompletedOn = DateTime.UtcNow
                    }
                End Using
            End Using
        End Function

        Private Shared Function GetLockedOutstanding(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer) As Decimal
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"SELECT TOP 1 OUTSTANDING
FROM dbo.TBL_SP_PSO_PAYMENT_2 WITH (UPDLOCK, HOLDLOCK)
WHERE ORDER_ID = @OrderId
ORDER BY CREATED_ON DESC, ID DESC"
                command.Parameters.AddWithValue("@OrderId", orderId)
                Dim result = command.ExecuteScalar()
                If result Is Nothing OrElse result Is DBNull.Value Then
                    Return 0D
                End If
                Return Convert.ToDecimal(result, CultureInfo.InvariantCulture)
            End Using
        End Function

        Private Shared Sub MarkOrderCompleted(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer)
            Using command = connection.CreateCommand()
                command.Transaction = transaction
                command.CommandText =
"UPDATE dbo.TBL_SP_PSO_ORDER
SET ORD_STATUS = 'Completed',
    DP_STATUS = 1,
    ORDER_C_DATE = GETDATE()
WHERE ID = @OrderId"
                command.Parameters.AddWithValue("@OrderId", orderId)
                command.ExecuteNonQuery()
            End Using
        End Sub

        Private Shared Function BuildSettlementNotes(request As PendingPaymentRequest, context As PendingPaymentContext) As String
            Dim fragments As New List(Of String) From {
                $"Method:{request.PaymentMethod}",
                $"VAT:{context.VatPercent.ToString("F2", CultureInfo.InvariantCulture)}%",
                $"Total:{context.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)}",
                $"Paid:{request.PaymentAmount.ToString("F2", CultureInfo.InvariantCulture)}",
                $"PreviouslyPaid:{context.PreviouslyPaid.ToString("F2", CultureInfo.InvariantCulture)}",
                $"Outstanding:0.00",
                $"SettlementOf:{context.OutstandingAmount.ToString("F2", CultureInfo.InvariantCulture)}"
            }

            If request.PaymentMethod = "Cash" Then
                If request.CashReceived.HasValue Then
                    fragments.Add($"CashReceived:{request.CashReceived.Value.ToString("F2", CultureInfo.InvariantCulture)}")
                End If
                If request.CashChange.HasValue Then
                    fragments.Add($"Change:{request.CashChange.Value.ToString("F2", CultureInfo.InvariantCulture)}")
                End If
            ElseIf request.PaymentMethod = "Card" Then
                If Not String.IsNullOrWhiteSpace(request.CardRrn) Then
                    fragments.Add($"RRN:{request.CardRrn}")
                End If
                If Not String.IsNullOrWhiteSpace(request.CardAuthCode) Then
                    fragments.Add($"Auth:{request.CardAuthCode}")
                End If
                If Not String.IsNullOrWhiteSpace(request.CardStatus) Then
                    fragments.Add($"Status:{request.CardStatus}")
                End If
            End If

            Dim formatted = String.Join(" | ", fragments)
            If formatted.Length > 480 Then
                formatted = formatted.Substring(0, 480)
            End If
            Return formatted
        End Function

        Private Shared Function ParseVatPercent(notes As String) As Decimal
            If String.IsNullOrWhiteSpace(notes) Then
                Return 0D
            End If

            Dim segments = notes.Split(New Char() {"|"c}, StringSplitOptions.RemoveEmptyEntries)
            For Each segment In segments
                Dim trimmed = segment.Trim()
                If trimmed.StartsWith("VAT:", StringComparison.OrdinalIgnoreCase) Then
                    Dim value = trimmed.Substring(4).Trim().TrimEnd("%"c)
                    Dim parsed As Decimal
                    If Decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                        Return parsed
                    End If
                End If
            Next

            Return 0D
        End Function
    End Class
End Namespace
