Imports System.Collections.Generic
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Linq
Imports System.Text
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Data
    Public Class OrderRepository
        Inherits SqlRepositoryBase

        Private Const DefaultCurrencyCode As String = "AED"

        Public Function CreateOrder(request As CheckoutRequest) As CheckoutResult
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If request.CartItems Is Nothing OrElse request.CartItems.Count = 0 Then
                Throw New InvalidOperationException("Cart is empty.")
            End If

            Dim cartItems = request.CartItems.ToList()
            Dim pricing = CalculateItemWisePricing(cartItems, request.DiscountPercent, request.TaxPercent, request.TotalDue)
            request.Subtotal = pricing.Subtotal
            request.TaxAmount = pricing.TotalVat
            request.TotalDue = pricing.TotalDue

            Dim orderNumber = GenerateOrderNumber()
            Dim inquiryNo = $"INQ-{DateTime.UtcNow:yyyyMMddHHmmss}"
            Dim paidAmount = ResolvePaidAmount(request)
            paidAmount = Decimal.Round(paidAmount, 2, MidpointRounding.AwayFromZero)
            Dim outstanding = Decimal.Round(Math.Max(0D, request.TotalDue - paidAmount), 2, MidpointRounding.AwayFromZero)
            Dim paymentReference = $"PMT-{DateTime.UtcNow:yyyyMMddHHmmssfff}"

            Using connection = CreateConnection()
                Using transaction = connection.BeginTransaction()
                    Try
                        Dim orderId = InsertOrder(connection, transaction, request, orderNumber, inquiryNo, outstanding)
                        InsertOrderItems(connection, transaction, orderId, cartItems, pricing.Items, request.TaxPercent)
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

        Private Shared Sub InsertOrderItems(connection As SqlConnection, transaction As SqlTransaction, orderId As Integer, cartItems As IList(Of CartItem), itemPricing As IList(Of ItemPricingLine), vatPercent As Decimal)
            If cartItems Is Nothing OrElse cartItems.Count = 0 Then
                Return
            End If

            Dim safeVatPercent = ClampPercent(vatPercent)

            For i = 0 To cartItems.Count - 1
                Dim cartItem = cartItems(i)
                Dim pricing As ItemPricingLine = Nothing
                If itemPricing IsNot Nothing AndAlso i >= 0 AndAlso i < itemPricing.Count Then
                    pricing = itemPricing(i)
                End If

                Dim lineDiscount = If(pricing IsNot Nothing, pricing.DiscountAmount, 0D)
                Dim lineVat = If(pricing IsNot Nothing, pricing.VatAmount, 0D)

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
    Advance_Tax,
    Total_Discount,
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
    @AdvanceTax,
    @TotalDiscount,
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
                    command.Parameters.AddWithValue("@TaxRate", safeVatPercent)
                    command.Parameters.AddWithValue("@AdvanceTax", Decimal.Round(Math.Max(0D, lineVat), 2, MidpointRounding.AwayFromZero))
                    command.Parameters.AddWithValue("@TotalDiscount", Decimal.Round(Math.Max(0D, lineDiscount), 2, MidpointRounding.AwayFromZero))
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
    CURRENCY_CODE,
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
    @CurrencyCode,
    CASE WHEN @Outstanding > 0 THEN 1 ELSE 0 END,
    @CreatedBy,
    @Notes
)"
                command.Parameters.AddWithValue("@OrderId", orderId)
                command.Parameters.AddWithValue("@Reference", paymentReference)
                command.Parameters.AddWithValue("@Method", method)
                command.Parameters.AddWithValue("@PaidAmount", paidAmount)
                command.Parameters.AddWithValue("@Outstanding", outstanding)
                command.Parameters.AddWithValue("@CurrencyCode", DefaultCurrencyCode)
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

        Public Function GetCustomerLedger(dealerId As Integer) As CustomerLedgerReport
            Dim report As New CustomerLedgerReport() With {
                .DealerId = dealerId,
                .OpeningBalance = 0D
            }

            Dim orders As IList(Of LedgerOrderSnapshot)
            Dim paymentsLookup As Dictionary(Of Integer, List(Of LedgerPaymentSnapshot))

            Using connection = CreateConnection()
                orders = LoadLedgerOrders(connection, dealerId)
                paymentsLookup = LoadLedgerPayments(connection, dealerId)
            End Using

            If orders Is Nothing OrElse orders.Count = 0 Then
                report.PendingAmount = 0D
                Return report
            End If

            Dim runningBalance = report.OpeningBalance
            Dim lastPaymentDate As Nullable(Of DateTime) = Nothing

            For Each order In orders
                Dim roundedTotal = Decimal.Round(order.TotalAmount, 2, MidpointRounding.AwayFromZero)
                runningBalance -= roundedTotal

                report.Entries.Add(New CustomerLedgerEntry() With {
                    .OrderId = order.OrderId,
                    .EntryDate = order.OrderDate,
                    .Description = order.OrderNumber,
                    .AmountDue = roundedTotal,
                    .PaymentReceived = 0D,
                    .BalanceAfterEntry = runningBalance,
                    .EntryType = CustomerLedgerEntryType.Order
                })

                Dim orderPayments As List(Of LedgerPaymentSnapshot) = Nothing
                If paymentsLookup IsNot Nothing AndAlso paymentsLookup.TryGetValue(order.OrderId, orderPayments) Then
                    For Each payment In orderPayments
                        Dim paidAmount = Decimal.Round(payment.PaidAmount, 2, MidpointRounding.AwayFromZero)
                        If paidAmount = 0D Then
                            Continue For
                        End If

                        runningBalance += paidAmount
                        Dim description = payment.PaymentReference
                        If String.IsNullOrWhiteSpace(description) Then
                            description = $"Payment #{payment.PaymentId}"
                        End If

                        report.Entries.Add(New CustomerLedgerEntry() With {
                            .OrderId = order.OrderId,
                            .PaymentId = payment.PaymentId,
                            .EntryDate = payment.CreatedOn,
                            .Description = description,
                            .AmountDue = 0D,
                            .PaymentReceived = paidAmount,
                            .BalanceAfterEntry = runningBalance,
                            .EntryType = CustomerLedgerEntryType.Payment
                        })

                        If Not lastPaymentDate.HasValue OrElse payment.CreatedOn > lastPaymentDate.Value Then
                            lastPaymentDate = payment.CreatedOn
                        End If
                    Next
                End If
            Next

            report.LastPaymentDate = lastPaymentDate
            Dim pending = If(runningBalance < 0D, -runningBalance, 0D)
            report.PendingAmount = Decimal.Round(pending, 2, MidpointRounding.AwayFromZero)
            Return report
        End Function

        Private Function LoadLedgerOrders(connection As SqlConnection, dealerId As Integer) As IList(Of LedgerOrderSnapshot)
            Dim orders As New List(Of LedgerOrderSnapshot)()
            Using command = connection.CreateCommand()
                command.CommandText =
"SELECT 
    o.ID,
    ISNULL(o.SPPSOID, '') AS ORDER_NUMBER,
    ISNULL(o.CDATED, GETDATE()) AS ORDER_DATE,
    ISNULL(o.TP, 0) AS TOTAL_AMOUNT
FROM dbo.TBL_SP_PSO_ORDER o
WHERE ((@DealerId = 0 AND ISNULL(o.DID, 0) = 0) OR o.DID = @DealerId)
ORDER BY o.CDATED ASC, o.ID ASC"
                command.Parameters.AddWithValue("@DealerId", dealerId)
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        orders.Add(New LedgerOrderSnapshot() With {
                            .OrderId = reader.GetInt32(reader.GetOrdinal("ID")),
                            .OrderNumber = reader.GetString(reader.GetOrdinal("ORDER_NUMBER")),
                            .OrderDate = reader.GetDateTime(reader.GetOrdinal("ORDER_DATE")),
                            .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_AMOUNT"))
                        })
                    End While
                End Using
            End Using
            Return orders
        End Function

        Private Function LoadLedgerPayments(connection As SqlConnection, dealerId As Integer) As Dictionary(Of Integer, List(Of LedgerPaymentSnapshot))
            Dim lookup As New Dictionary(Of Integer, List(Of LedgerPaymentSnapshot))()
            Using command = connection.CreateCommand()
                command.CommandText =
"SELECT 
    p.ID,
    p.ORDER_ID,
    ISNULL(p.PAYMENT_REFERENCE, '') AS PAYMENT_REFERENCE,
    ISNULL(p.PAID_AMOUNT, 0) AS PAID_AMOUNT,
    ISNULL(p.CREATED_ON, GETDATE()) AS CREATED_ON
FROM dbo.TBL_SP_PSO_PAYMENT_2 p
INNER JOIN dbo.TBL_SP_PSO_ORDER o ON o.ID = p.ORDER_ID
WHERE ((@DealerId = 0 AND ISNULL(o.DID, 0) = 0) OR o.DID = @DealerId)
ORDER BY o.CDATED ASC, o.ID ASC, p.CREATED_ON ASC, p.ID ASC"
                command.Parameters.AddWithValue("@DealerId", dealerId)
                Using reader = command.ExecuteReader()
                    While reader.Read()
                        Dim snapshot As New LedgerPaymentSnapshot() With {
                            .PaymentId = reader.GetInt32(reader.GetOrdinal("ID")),
                            .OrderId = reader.GetInt32(reader.GetOrdinal("ORDER_ID")),
                            .PaymentReference = reader.GetString(reader.GetOrdinal("PAYMENT_REFERENCE")),
                            .PaidAmount = reader.GetDecimal(reader.GetOrdinal("PAID_AMOUNT")),
                            .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CREATED_ON"))
                        }

                        Dim bucket As List(Of LedgerPaymentSnapshot) = Nothing
                        If Not lookup.TryGetValue(snapshot.OrderId, bucket) Then
                            bucket = New List(Of LedgerPaymentSnapshot)()
                            lookup(snapshot.OrderId) = bucket
                        End If
                        bucket.Add(snapshot)
                    End While
                End Using
            End Using

            Return lookup
        End Function

        Public Function GetReceivables() As IList(Of ReceivableReportRow)
            Dim receivables As New List(Of ReceivableReportRow)()
            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT 
    o.ID,
    o.SPPSOID,
    ISNULL(o.CDATED, GETDATE()) AS CDATED,
    ISNULL(o.TP, 0) AS TOTAL_AMOUNT,
    ISNULL(o.DID, 0) AS DEALER_ID,
    ISNULL(o.CONTACT, '') AS CONTACT,
    ISNULL(totals.TotalPaid, 0) AS TOTAL_PAID,
    ISNULL(lastPayment.OUTSTANDING, ISNULL(o.TP, 0) - ISNULL(totals.TotalPaid, 0)) AS OUTSTANDING
FROM dbo.TBL_SP_PSO_ORDER o
OUTER APPLY (
    SELECT SUM(p.PAID_AMOUNT) AS TotalPaid
    FROM dbo.TBL_SP_PSO_PAYMENT_2 p
    WHERE p.ORDER_ID = o.ID
) totals
OUTER APPLY (
    SELECT TOP 1 
        p.OUTSTANDING
    FROM dbo.TBL_SP_PSO_PAYMENT_2 p
    WHERE p.ORDER_ID = o.ID
    ORDER BY p.CREATED_ON DESC, p.ID DESC
) lastPayment
WHERE o.ORD_STATUS = 'Pending Payment'
ORDER BY o.CDATED ASC, o.ID ASC"
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            Dim dealerId = reader.GetInt32(reader.GetOrdinal("DEALER_ID"))
                            Dim contact = reader.GetString(reader.GetOrdinal("CONTACT"))
                            Dim outstanding = reader.GetDecimal(reader.GetOrdinal("OUTSTANDING"))
                            If outstanding < 0D Then
                                outstanding = 0D
                            End If

                            receivables.Add(New ReceivableReportRow() With {
                                .OrderId = reader.GetInt32(reader.GetOrdinal("ID")),
                                .InvoiceNumber = reader.GetString(reader.GetOrdinal("SPPSOID")),
                                .InvoiceDate = reader.GetDateTime(reader.GetOrdinal("CDATED")),
                                .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_AMOUNT")),
                                .PaidAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_PAID")),
                                .OutstandingAmount = outstanding,
                                .CustomerName = NormalizeCustomerName(dealerId, contact)
                            })
                        End While
                    End Using
                End Using
            End Using
            Return receivables
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

        Public Function GetSalesHistory(filter As SalesHistoryFilter) As IList(Of SalesHistoryOrder)
            Dim orders As New List(Of SalesHistoryOrder)()
            Dim notesLookup As New Dictionary(Of Integer, String)()

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    Dim sql = New StringBuilder()
                    sql.AppendLine("SELECT TOP 200")
                    sql.AppendLine("    o.ID,")
                    sql.AppendLine("    o.SPPSOID,")
                    sql.AppendLine("    o.CDATED,")
                    sql.AppendLine("    ISNULL(o.CONTACT, '') AS CONTACT,")
                    sql.AppendLine("    ISNULL(o.DID, 0) AS DEALER_ID,")
                    sql.AppendLine("    ISNULL(o.userid, '') AS CASHIER_NAME,")
                    sql.AppendLine("    ISNULL(o.TP, 0) AS TOTAL_DUE,")
                    sql.AppendLine("    ISNULL(totals.TotalPaid, 0) AS TOTAL_PAID,")
                    sql.AppendLine("    ISNULL(lastPayment.OUTSTANDING, 0) AS OUTSTANDING,")
                    sql.AppendLine("    ISNULL(primaryPayment.PAYMENT_METHOD, 'Cash') AS PAYMENT_METHOD,")
                    sql.AppendLine("    ISNULL(primaryPayment.PAYMENT_REFERENCE, o.SPPSOID) AS RECEIPT_NUMBER,")
                    sql.AppendLine("    ISNULL(primaryPayment.NOTES, '') AS PRIMARY_NOTES")
                    sql.AppendLine("FROM dbo.TBL_SP_PSO_ORDER o")
                    sql.AppendLine("OUTER APPLY (")
                    sql.AppendLine("    SELECT SUM(p.PAID_AMOUNT) AS TotalPaid")
                    sql.AppendLine("    FROM dbo.TBL_SP_PSO_PAYMENT_2 p")
                    sql.AppendLine("    WHERE p.ORDER_ID = o.ID")
                    sql.AppendLine(") totals")
                    sql.AppendLine("OUTER APPLY (")
                    sql.AppendLine("    SELECT TOP 1")
                    sql.AppendLine("        p.PAYMENT_METHOD,")
                    sql.AppendLine("        p.PAYMENT_REFERENCE,")
                    sql.AppendLine("        ISNULL(p.NOTES, '') AS NOTES")
                    sql.AppendLine("    FROM dbo.TBL_SP_PSO_PAYMENT_2 p")
                    sql.AppendLine("    WHERE p.ORDER_ID = o.ID")
                    sql.AppendLine("    ORDER BY p.CREATED_ON ASC, p.ID ASC")
                    sql.AppendLine(") primaryPayment")
                    sql.AppendLine("OUTER APPLY (")
                    sql.AppendLine("    SELECT TOP 1")
                    sql.AppendLine("        p.OUTSTANDING")
                    sql.AppendLine("    FROM dbo.TBL_SP_PSO_PAYMENT_2 p")
                    sql.AppendLine("    WHERE p.ORDER_ID = o.ID")
                    sql.AppendLine("    ORDER BY p.CREATED_ON DESC, p.ID DESC")
                    sql.AppendLine(") lastPayment")
                    sql.AppendLine("WHERE 1 = 1")

                    If filter IsNot Nothing Then
                        If filter.FromDate.HasValue Then
                            sql.AppendLine("    AND o.CDATED >= @FromDate")
                            command.Parameters.AddWithValue("@FromDate", filter.FromDate.Value.Date)
                        End If
                        If filter.ToDate.HasValue Then
                            sql.AppendLine("    AND o.CDATED < DATEADD(day, 1, @ToDate)")
                            command.Parameters.AddWithValue("@ToDate", filter.ToDate.Value.Date)
                        End If
                        If Not String.IsNullOrWhiteSpace(filter.OrderNumber) Then
                            sql.AppendLine("    AND o.SPPSOID LIKE @OrderNumber")
                            command.Parameters.AddWithValue("@OrderNumber", $"%{filter.OrderNumber.Trim()}%")
                        End If
                    End If

                    sql.AppendLine("ORDER BY o.CDATED DESC, o.ID DESC")
                    command.CommandText = sql.ToString()

                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            Dim orderId = reader.GetInt32(reader.GetOrdinal("ID"))
                            Dim entry As New SalesHistoryOrder() With {
                                .OrderId = orderId,
                                .OrderNumber = reader.GetString(reader.GetOrdinal("SPPSOID")),
                                .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CDATED")),
                                .CustomerName = NormalizeCustomerName(reader.GetInt32(reader.GetOrdinal("DEALER_ID")), reader.GetString(reader.GetOrdinal("CONTACT"))),
                                .PaymentMethod = reader.GetString(reader.GetOrdinal("PAYMENT_METHOD")),
                                .ReceiptNumber = reader.GetString(reader.GetOrdinal("RECEIPT_NUMBER")),
                                .CashierName = reader.GetString(reader.GetOrdinal("CASHIER_NAME")),
                                .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_DUE")),
                                .TotalPaid = reader.GetDecimal(reader.GetOrdinal("TOTAL_PAID")),
                                .OutstandingAmount = reader.GetDecimal(reader.GetOrdinal("OUTSTANDING")),
                                .LineItems = New List(Of OrderLineItem)()
                            }
                            orders.Add(entry)
                            notesLookup(orderId) = reader.GetString(reader.GetOrdinal("PRIMARY_NOTES"))
                        End While
                    End Using
                End Using

                PopulateLineItems(connection, orders)
            End Using

            For Each order In orders
                Dim note As String = Nothing
                notesLookup.TryGetValue(order.OrderId, note)
                Dim parsedNotes = ParsePaymentNotes(note)
                Dim financials = CalculateFinancials(order.LineItems, parsedNotes, order.TotalAmount)
                order.Subtotal = financials.Subtotal
                order.DiscountAmount = financials.DiscountAmount
                order.DiscountPercent = financials.DiscountPercent
                order.TaxPercent = financials.TaxPercent
                order.TaxAmount = financials.TaxAmount
                order.TotalAmount = financials.TotalAmount
            Next

            Return orders
        End Function

        Public Function GetOrderReceipt(orderId As Integer) As OrderReceiptData
            If orderId <= 0 Then
                Return Nothing
            End If

            Using connection = CreateConnection()
                Dim header = GetOrderHeader(connection, orderId)
                If header Is Nothing Then
                    Return Nothing
                End If

                Dim primaryPayment = GetPrimaryPayment(connection, orderId)
                Dim latestPayment = GetLatestPaymentRecord(connection, orderId)
                Dim itemsLookup = LoadLineItems(connection, New List(Of Integer) From {orderId})
                Dim items As IList(Of OrderLineItem) = Nothing
                If Not itemsLookup.TryGetValue(orderId, items) Then
                    items = New List(Of OrderLineItem)()
                End If

                Dim primaryNotes = If(primaryPayment IsNot Nothing, primaryPayment.Notes, Nothing)
                Dim parsedNotes = ParsePaymentNotes(primaryNotes)
                Dim financials = CalculateFinancials(items, parsedNotes, header.TotalAmount)

                Dim paymentMethod = "Cash"
                If primaryPayment IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(primaryPayment.PaymentMethod) Then
                    paymentMethod = primaryPayment.PaymentMethod
                End If

                Dim receiptNumber = header.OrderNumber
                If primaryPayment IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(primaryPayment.PaymentReference) Then
                    receiptNumber = primaryPayment.PaymentReference
                End If

                Dim cashierName = header.CashierName
                If primaryPayment IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(primaryPayment.CreatedBy) Then
                    cashierName = primaryPayment.CreatedBy
                End If

                Dim paidAmount = financials.TotalAmount
                If primaryPayment IsNot Nothing Then
                    paidAmount = primaryPayment.PaidAmount
                End If

                Dim outstanding = 0D
                If latestPayment IsNot Nothing Then
                    outstanding = latestPayment.OutstandingAfterPayment
                End If

                Dim receipt As New OrderReceiptData() With {
                    .OrderId = header.OrderId,
                    .OrderNumber = header.OrderNumber,
                    .DealerId = header.DealerId,
                    .OrderDate = header.CreatedOn,
                    .CustomerName = header.CustomerName,
                    .PaymentMethod = paymentMethod,
                    .ReceiptNumber = receiptNumber,
                    .CashierName = cashierName,
                    .LineItems = items,
                    .Subtotal = financials.Subtotal,
                    .DiscountAmount = financials.DiscountAmount,
                    .DiscountPercent = financials.DiscountPercent,
                    .TaxPercent = financials.TaxPercent,
                    .TaxAmount = financials.TaxAmount,
                    .TotalAmount = financials.TotalAmount,
                    .PaidAmount = paidAmount,
                    .OutstandingAmount = outstanding
                }

                Return receipt
            End Using
        End Function

        Public Function GetSettlementReceipt(orderId As Integer, receiptNumber As String) As PendingPaymentResult
            If orderId <= 0 OrElse String.IsNullOrWhiteSpace(receiptNumber) Then
                Return Nothing
            End If

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText =
"SELECT TOP 1
    p.ID,
    p.ORDER_ID,
    p.PAYMENT_REFERENCE,
    p.PAYMENT_METHOD,
    p.PAID_AMOUNT,
    p.OUTSTANDING,
    ISNULL(p.NOTES, '') AS NOTES,
    ISNULL(p.CREATED_BY, '') AS CREATED_BY,
    p.CREATED_ON,
    o.SPPSOID,
    ISNULL(o.CONTACT, '') AS CONTACT,
    ISNULL(o.TP, 0) AS TOTAL_DUE,
    ISNULL(o.DID, 0) AS DEALER_ID
FROM dbo.TBL_SP_PSO_PAYMENT_2 p
INNER JOIN dbo.TBL_SP_PSO_ORDER o ON o.ID = p.ORDER_ID
WHERE p.ORDER_ID = @OrderId AND p.PAYMENT_REFERENCE = @Receipt
ORDER BY p.ID DESC"
                    command.Parameters.AddWithValue("@OrderId", orderId)
                    command.Parameters.AddWithValue("@Receipt", receiptNumber)

                    Using reader = command.ExecuteReader()
                        If Not reader.Read() Then
                            Return Nothing
                        End If

                        Dim notes = reader.GetString(reader.GetOrdinal("NOTES"))
                        Dim parsedNotes = ParsePaymentNotes(notes)
                        Dim totalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_DUE"))
                        Dim settledAmount = reader.GetDecimal(reader.GetOrdinal("PAID_AMOUNT"))

                        Dim result As New PendingPaymentResult() With {
                            .OrderId = reader.GetInt32(reader.GetOrdinal("ORDER_ID")),
                            .OrderNumber = reader.GetString(reader.GetOrdinal("SPPSOID")),
                            .ReceiptNumber = reader.GetString(reader.GetOrdinal("PAYMENT_REFERENCE")),
                            .DealerName = NormalizeCustomerName(reader.GetInt32(reader.GetOrdinal("DEALER_ID")), reader.GetString(reader.GetOrdinal("CONTACT"))),
                            .TotalOrderAmount = totalAmount,
                            .SettledAmount = settledAmount,
                            .PaymentMethod = reader.GetString(reader.GetOrdinal("PAYMENT_METHOD")),
                            .CashierName = reader.GetString(reader.GetOrdinal("CREATED_BY")),
                            .CompletedOn = reader.GetDateTime(reader.GetOrdinal("CREATED_ON")),
                            .PreviouslyPaid = GetNoteDecimal(parsedNotes, "PreviouslyPaid"),
                            .OutstandingBeforePayment = GetNoteDecimal(parsedNotes, "SettlementOf"),
                            .VatPercent = GetNoteDecimal(parsedNotes, "VAT")
                        }

                        If result.PreviouslyPaid <= 0D Then
                            result.PreviouslyPaid = Math.Max(0D, totalAmount - settledAmount)
                        End If
                        If result.OutstandingBeforePayment <= 0D Then
                            result.OutstandingBeforePayment = result.SettledAmount
                        End If

                        If result.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase) Then
                            result.CashReceived = GetNoteDecimal(parsedNotes, "CashReceived")
                            result.CashChange = GetNoteDecimal(parsedNotes, "Change")
                        ElseIf result.PaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase) Then
                            result.CardRrn = GetNoteString(parsedNotes, "RRN")
                            result.CardAuthCode = GetNoteString(parsedNotes, "Auth")
                        End If

                        Return result
                    End Using
                End Using
            End Using
        End Function

        Private Shared Sub PopulateLineItems(connection As SqlConnection, orders As IList(Of SalesHistoryOrder))
            If orders Is Nothing OrElse orders.Count = 0 Then
                Return
            End If

            Dim orderIds = orders.Select(Function(o) o.OrderId).Distinct().ToList()
            Dim lookup = LoadLineItems(connection, orderIds)
            For Each order In orders
                Dim items As IList(Of OrderLineItem) = Nothing
                If Not lookup.TryGetValue(order.OrderId, items) Then
                    items = New List(Of OrderLineItem)()
                End If
                order.LineItems = items
            Next
        End Sub

        Private Shared Function LoadLineItems(connection As SqlConnection, orderIds As IList(Of Integer)) As Dictionary(Of Integer, List(Of OrderLineItem))
            Dim result As New Dictionary(Of Integer, List(Of OrderLineItem))()
            If orderIds Is Nothing OrElse orderIds.Count = 0 Then
                Return result
            End If

            Dim parameterNames = New List(Of String)()
            For i = 0 To orderIds.Count - 1
                parameterNames.Add($"@OrderId{i}")
            Next

            Dim sql =
$"SELECT SPPSOID,
        ITEMID,
        ITEMNAME,
        QTY,
        TotalAmount,
        RP,
        ST,
        ISNULL(Advance_Tax, 0) AS Advance_Tax,
        ISNULL(Total_Discount, 0) AS Total_Discount
FROM dbo.TBL_SP_PSO_ITEM
WHERE SPPSOID IN ({String.Join(",", parameterNames)})
ORDER BY SPPSOID, ITEMNAME"

            Using command = connection.CreateCommand()
                command.CommandText = sql
                For i = 0 To orderIds.Count - 1
                    command.Parameters.AddWithValue(parameterNames(i), orderIds(i))
                Next

                Using reader = command.ExecuteReader()
                    While reader.Read()
                        Dim orderId = GetDbInt32(reader, "SPPSOID")
                        Dim lineItem As New OrderLineItem() With {
                            .ProductId = reader.GetInt32(reader.GetOrdinal("ITEMID")),
                            .Name = reader.GetString(reader.GetOrdinal("ITEMNAME")),
                            .Quantity = reader.GetInt32(reader.GetOrdinal("QTY")),
                            .UnitPrice = reader.GetDecimal(reader.GetOrdinal("RP")),
                            .LineTotal = reader.GetDecimal(reader.GetOrdinal("TotalAmount")),
                            .TaxRate = reader.GetDecimal(reader.GetOrdinal("ST")),
                            .TaxAmount = GetDbDecimal(reader, "Advance_Tax"),
                            .DiscountAmount = GetDbDecimal(reader, "Total_Discount")
                        }

                        Dim items As List(Of OrderLineItem) = Nothing
                        If Not result.TryGetValue(orderId, items) Then
                            items = New List(Of OrderLineItem)()
                            result(orderId) = items
                        End If
                        items.Add(lineItem)
                    End While
                End Using
            End Using

            Return result
        End Function

        Private Shared Function GetDbInt32(reader As SqlDataReader, columnName As String) As Integer
            Dim ordinal = reader.GetOrdinal(columnName)
            If reader.IsDBNull(ordinal) Then
                Return 0
            End If

            Dim raw = reader.GetValue(ordinal)
            If TypeOf raw Is Integer Then
                Return CInt(raw)
            End If
            If TypeOf raw Is Long Then
                Dim value = CLng(raw)
                If value > Integer.MaxValue Then
                    Return Integer.MaxValue
                End If
                If value < Integer.MinValue Then
                    Return Integer.MinValue
                End If
                Return CInt(value)
            End If
            If TypeOf raw Is Decimal Then
                Return Convert.ToInt32(raw, CultureInfo.InvariantCulture)
            End If
            If TypeOf raw Is String Then
                Dim parsed As Integer
                If Integer.TryParse(CStr(raw), NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then
                    Return parsed
                End If
            End If

            Return Convert.ToInt32(raw, CultureInfo.InvariantCulture)
        End Function

        Private Shared Function GetDbDecimal(reader As SqlDataReader, columnName As String) As Decimal
            Dim ordinal = reader.GetOrdinal(columnName)
            If reader.IsDBNull(ordinal) Then
                Return 0D
            End If

            Dim raw = reader.GetValue(ordinal)
            If TypeOf raw Is Decimal Then
                Return CDec(raw)
            End If
            If TypeOf raw Is Double Then
                Return Convert.ToDecimal(CDbl(raw), CultureInfo.InvariantCulture)
            End If
            If TypeOf raw Is Single Then
                Return Convert.ToDecimal(CSng(raw), CultureInfo.InvariantCulture)
            End If
            If TypeOf raw Is Integer Then
                Return Convert.ToDecimal(CInt(raw), CultureInfo.InvariantCulture)
            End If
            If TypeOf raw Is Long Then
                Return Convert.ToDecimal(CLng(raw), CultureInfo.InvariantCulture)
            End If
            If TypeOf raw Is String Then
                Dim parsed As Decimal
                If Decimal.TryParse(CStr(raw), NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                    Return parsed
                End If
            End If

            Return Convert.ToDecimal(raw, CultureInfo.InvariantCulture)
        End Function

        Private Shared Function GetOrderHeader(connection As SqlConnection, orderId As Integer) As OrderHeaderInfo
            Using command = connection.CreateCommand()
                command.CommandText =
"SELECT TOP 1
    o.ID,
    o.SPPSOID,
    ISNULL(o.CONTACT, '') AS CONTACT,
    ISNULL(o.userid, '') AS CASHIER,
    o.CDATED,
    ISNULL(o.TP, 0) AS TOTAL_DUE,
    ISNULL(o.DID, 0) AS DEALER_ID
FROM dbo.TBL_SP_PSO_ORDER o
WHERE o.ID = @OrderId"
                command.Parameters.AddWithValue("@OrderId", orderId)

                Using reader = command.ExecuteReader()
                    If Not reader.Read() Then
                        Return Nothing
                    End If

                    Return New OrderHeaderInfo() With {
                        .OrderId = reader.GetInt32(reader.GetOrdinal("ID")),
                        .OrderNumber = reader.GetString(reader.GetOrdinal("SPPSOID")),
                        .DealerId = reader.GetInt32(reader.GetOrdinal("DEALER_ID")),
                        .CustomerName = NormalizeCustomerName(reader.GetInt32(reader.GetOrdinal("DEALER_ID")), reader.GetString(reader.GetOrdinal("CONTACT"))),
                        .CashierName = reader.GetString(reader.GetOrdinal("CASHIER")),
                        .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CDATED")),
                        .TotalAmount = reader.GetDecimal(reader.GetOrdinal("TOTAL_DUE"))
                    }
                End Using
            End Using
        End Function

        Private Shared Function GetPrimaryPayment(connection As SqlConnection, orderId As Integer) As OrderPaymentSnapshot
            Using command = connection.CreateCommand()
                command.CommandText =
"SELECT TOP 1
    PAYMENT_REFERENCE,
    PAYMENT_METHOD,
    PAID_AMOUNT,
    ISNULL(NOTES, '') AS NOTES,
    ISNULL(CREATED_BY, '') AS CREATED_BY
FROM dbo.TBL_SP_PSO_PAYMENT_2
WHERE ORDER_ID = @OrderId
ORDER BY CREATED_ON ASC, ID ASC"
                command.Parameters.AddWithValue("@OrderId", orderId)

                Using reader = command.ExecuteReader()
                    If Not reader.Read() Then
                        Return Nothing
                    End If

                    Return New OrderPaymentSnapshot() With {
                        .PaymentReference = reader.GetString(reader.GetOrdinal("PAYMENT_REFERENCE")),
                        .PaymentMethod = reader.GetString(reader.GetOrdinal("PAYMENT_METHOD")),
                        .PaidAmount = reader.GetDecimal(reader.GetOrdinal("PAID_AMOUNT")),
                        .Notes = reader.GetString(reader.GetOrdinal("NOTES")),
                        .CreatedBy = reader.GetString(reader.GetOrdinal("CREATED_BY"))
                    }
                End Using
            End Using
        End Function

        Private Shared Function GetLatestPaymentRecord(connection As SqlConnection, orderId As Integer) As OrderPaymentRecord
            Using command = connection.CreateCommand()
                command.CommandText =
"SELECT TOP 1
    ORDER_ID,
    PAYMENT_REFERENCE,
    PAYMENT_METHOD,
    PAID_AMOUNT,
    OUTSTANDING,
    ISNULL(NOTES, '') AS NOTES,
    IS_PARTIAL,
    CREATED_ON
FROM dbo.TBL_SP_PSO_PAYMENT_2
WHERE ORDER_ID = @OrderId
ORDER BY CREATED_ON DESC, ID DESC"
                command.Parameters.AddWithValue("@OrderId", orderId)

                Using reader = command.ExecuteReader()
                    If Not reader.Read() Then
                        Return Nothing
                    End If

                    Return New OrderPaymentRecord() With {
                        .OrderId = reader.GetInt32(reader.GetOrdinal("ORDER_ID")),
                        .PaymentReference = reader.GetString(reader.GetOrdinal("PAYMENT_REFERENCE")),
                        .PaymentMethod = reader.GetString(reader.GetOrdinal("PAYMENT_METHOD")),
                        .PaidAmount = reader.GetDecimal(reader.GetOrdinal("PAID_AMOUNT")),
                        .OutstandingAfterPayment = reader.GetDecimal(reader.GetOrdinal("OUTSTANDING")),
                        .Notes = reader.GetString(reader.GetOrdinal("NOTES")),
                        .IsPartial = reader.GetBoolean(reader.GetOrdinal("IS_PARTIAL")),
                        .CreatedOn = reader.GetDateTime(reader.GetOrdinal("CREATED_ON"))
                    }
                End Using
            End Using
        End Function

        Private Shared Function NormalizeCustomerName(dealerId As Integer, contact As String) As String
            If dealerId <= 0 Then
                Return "Walk-in Customer"
            End If
            If String.IsNullOrWhiteSpace(contact) Then
                Return $"Customer #{dealerId}"
            End If
            Return contact
        End Function

        Private Shared Function ParsePaymentNotes(notes As String) As IDictionary(Of String, String)
            Dim result As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
            If String.IsNullOrWhiteSpace(notes) Then
                Return result
            End If

            Dim segments = notes.Split(New Char() {"|"c}, StringSplitOptions.RemoveEmptyEntries)
            For Each segment In segments
                Dim parts = segment.Split(New Char() {":"c}, 2)
                If parts.Length = 2 Then
                    Dim key = parts(0).Trim()
                    Dim value = parts(1).Trim()
                    If Not String.IsNullOrWhiteSpace(key) Then
                        result(key) = value
                    End If
                End If
            Next
            Return result
        End Function

        Private Shared Function GetNoteDecimal(notes As IDictionary(Of String, String), key As String) As Decimal
            If notes Is Nothing OrElse Not notes.ContainsKey(key) Then
                Return 0D
            End If

            Dim raw = notes(key)
            If String.IsNullOrWhiteSpace(raw) Then
                Return 0D
            End If

            raw = raw.Trim().TrimEnd("%"c)
            Dim parsed As Decimal
            If Decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                Return parsed
            End If
            Return 0D
        End Function

        Private Shared Function GetNoteString(notes As IDictionary(Of String, String), key As String) As String
            If notes IsNot Nothing AndAlso notes.ContainsKey(key) Then
                Return notes(key)
            End If
            Return String.Empty
        End Function

        Private Shared Function CalculateFinancials(lineItems As IList(Of OrderLineItem), notes As IDictionary(Of String, String), fallbackTotal As Decimal) As OrderFinancials
            Dim subtotal = If(lineItems IsNot Nothing AndAlso lineItems.Count > 0, lineItems.Sum(Function(i) i.LineTotal), 0D)
            Dim totalFromNotes = GetNoteDecimal(notes, "Total")
            Dim vatPercent = GetNoteDecimal(notes, "VAT")

            Dim totalDue = If(totalFromNotes > 0D, totalFromNotes, fallbackTotal)
            If totalDue <= 0D Then
                totalDue = subtotal
            End If

            Dim itemDiscountTotal = If(lineItems IsNot Nothing AndAlso lineItems.Count > 0, lineItems.Sum(Function(i) Math.Max(0D, i.DiscountAmount)), 0D)
            Dim itemVatTotal = If(lineItems IsNot Nothing AndAlso lineItems.Count > 0, lineItems.Sum(Function(i) Math.Max(0D, i.TaxAmount)), 0D)
            Dim itemCandidateTotal = Decimal.Round(Math.Max(0D, subtotal - itemDiscountTotal + itemVatTotal), 2, MidpointRounding.AwayFromZero)
            Dim expectedTotal = Decimal.Round(Math.Max(0D, totalDue), 2, MidpointRounding.AwayFromZero)
            Dim shouldUseItemFinancials = (lineItems IsNot Nothing AndAlso lineItems.Count > 0 AndAlso Math.Abs(itemCandidateTotal - expectedTotal) <= 0.02D)

            If shouldUseItemFinancials Then
                Dim taxableBase = Math.Max(0D, subtotal - itemDiscountTotal)
                Dim discountPercent = If(subtotal > 0D, Decimal.Round((itemDiscountTotal / subtotal) * 100D, 2, MidpointRounding.AwayFromZero), 0D)
                Dim computedVatPercent = 0D
                If taxableBase > 0D Then
                    computedVatPercent = Decimal.Round((itemVatTotal / taxableBase) * 100D, 2, MidpointRounding.AwayFromZero)
                ElseIf vatPercent > 0D Then
                    computedVatPercent = vatPercent
                End If

                Return New OrderFinancials() With {
                    .Subtotal = Decimal.Round(Math.Max(0D, subtotal), 2, MidpointRounding.AwayFromZero),
                    .DiscountAmount = Decimal.Round(Math.Max(0D, itemDiscountTotal), 2, MidpointRounding.AwayFromZero),
                    .DiscountPercent = discountPercent,
                    .TaxPercent = computedVatPercent,
                    .TaxAmount = Decimal.Round(Math.Max(0D, itemVatTotal), 2, MidpointRounding.AwayFromZero),
                    .TotalAmount = expectedTotal
                }
            End If

            Dim taxableBase As Decimal
            If vatPercent > 0D Then
                Dim divisor = 1D + (vatPercent / 100D)
                taxableBase = If(divisor = 0D, totalDue, Decimal.Round(totalDue / divisor, 2, MidpointRounding.AwayFromZero))
            Else
                taxableBase = totalDue
            End If

            Dim discountAmount = Math.Max(0D, subtotal - taxableBase)
            Dim discountPercent = If(subtotal > 0D, Decimal.Round((discountAmount / subtotal) * 100D, 2, MidpointRounding.AwayFromZero), 0D)
            Dim taxAmount = Decimal.Round(totalDue - taxableBase, 2, MidpointRounding.AwayFromZero)

            Return New OrderFinancials() With {
                .Subtotal = If(subtotal > 0D, subtotal, taxableBase),
                .DiscountAmount = discountAmount,
                .DiscountPercent = discountPercent,
                .TaxPercent = vatPercent,
                .TaxAmount = taxAmount,
                .TotalAmount = totalDue
            }
        End Function

        Private Shared Function ClampPercent(value As Decimal) As Decimal
            If value < 0D Then
                Return 0D
            End If
            If value > 100D Then
                Return 100D
            End If
            Return value
        End Function

        Private Shared Function ToCents(amount As Decimal) As Integer
            Return Convert.ToInt32(Decimal.Round(amount * 100D, 0, MidpointRounding.AwayFromZero))
        End Function

        Private Shared Function FromCents(cents As Integer) As Decimal
            Return cents / 100D
        End Function

        Private Shared Sub ReconcileRoundedAmounts(targetAmount As Decimal, values As IList(Of Decimal), maxima As IList(Of Decimal))
            If values Is Nothing OrElse values.Count = 0 Then
                Return
            End If

            Dim targetCents = ToCents(targetAmount)
            Dim currentCents = values.Sum(Function(v) ToCents(v))
            Dim delta = targetCents - currentCents
            If delta = 0 Then
                Return
            End If

            ' Work from the end so the "last line" absorbs rounding by default.
            For i = values.Count - 1 To 0 Step -1
                If delta = 0 Then
                    Exit For
                End If

                Dim current = ToCents(values(i))
                Dim minC = 0
                Dim maxC = If(maxima Is Nothing OrElse i >= maxima.Count, Integer.MaxValue, ToCents(maxima(i)))

                If delta > 0 Then
                    Dim room = maxC - current
                    If room <= 0 Then
                        Continue For
                    End If
                    Dim add = Math.Min(delta, room)
                    current += add
                    delta -= add
                Else
                    Dim room = current - minC
                    If room <= 0 Then
                        Continue For
                    End If
                    Dim take = Math.Min(-delta, room)
                    current -= take
                    delta += take
                End If

                values(i) = FromCents(current)
            Next
        End Sub

        Private Shared Function CalculateItemWisePricing(cartItems As IList(Of CartItem), discountPercent As Decimal, vatPercent As Decimal, totalDueTarget As Decimal) As ItemWisePricingResult
            Dim result As New ItemWisePricingResult()
            If cartItems Is Nothing OrElse cartItems.Count = 0 Then
                Return result
            End If

            Dim safeVatPercent = ClampPercent(vatPercent)
            Dim safeDiscountPercent = ClampPercent(discountPercent)

            Dim lineTotals = cartItems.Select(Function(i) Math.Max(0D, i.LineTotal)).ToList()
            Dim subtotal = lineTotals.Sum()

            Dim totalDue = totalDueTarget
            If totalDue <= 0D Then
                ' Fallback if client didn't provide a total.
                Dim approxDiscount = Decimal.Round(subtotal * (safeDiscountPercent / 100D), 2, MidpointRounding.AwayFromZero)
                Dim approxTaxable = Math.Max(0D, subtotal - approxDiscount)
                Dim approxVat = Decimal.Round(approxTaxable * (safeVatPercent / 100D), 2, MidpointRounding.AwayFromZero)
                totalDue = Decimal.Round(approxTaxable + approxVat, 2, MidpointRounding.AwayFromZero)
            Else
                totalDue = Decimal.Round(totalDue, 2, MidpointRounding.AwayFromZero)
            End If

            Dim taxableBaseTarget As Decimal
            If safeVatPercent > 0D Then
                taxableBaseTarget = totalDue / (1D + (safeVatPercent / 100D))
            Else
                taxableBaseTarget = totalDue
            End If

            Dim totalVatTarget = Decimal.Round(Math.Max(0D, totalDue - taxableBaseTarget), 2, MidpointRounding.AwayFromZero)
            Dim totalDiscountTarget = Decimal.Round(Math.Max(0D, subtotal - taxableBaseTarget), 2, MidpointRounding.AwayFromZero)
            If totalDiscountTarget > subtotal Then
                totalDiscountTarget = Decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero)
            End If

            Dim discounts As New List(Of Decimal)(cartItems.Count)
            Dim discountMaxima As New List(Of Decimal)(cartItems.Count)
            For i = 0 To cartItems.Count - 1
                discounts.Add(0D)
                discountMaxima.Add(Decimal.Round(Math.Max(0D, lineTotals(i)), 2, MidpointRounding.AwayFromZero))
            Next

            If subtotal > 0D AndAlso totalDiscountTarget > 0D Then
                For i = 0 To cartItems.Count - 1
                    If i = cartItems.Count - 1 Then
                        Continue For
                    End If
                    Dim weight = lineTotals(i) / subtotal
                    discounts(i) = Decimal.Round(totalDiscountTarget * weight, 2, MidpointRounding.AwayFromZero)
                Next
                ReconcileRoundedAmounts(totalDiscountTarget, discounts, discountMaxima)
            End If

            Dim vats As New List(Of Decimal)(cartItems.Count)
            For i = 0 To cartItems.Count - 1
                Dim taxableLine = Math.Max(0D, lineTotals(i) - discounts(i))
                vats.Add(Decimal.Round(taxableLine * (safeVatPercent / 100D), 2, MidpointRounding.AwayFromZero))
            Next
            ReconcileRoundedAmounts(totalVatTarget, vats, Nothing)

            For i = 0 To cartItems.Count - 1
                result.Items.Add(New ItemPricingLine() With {
                    .LineTotal = Decimal.Round(lineTotals(i), 2, MidpointRounding.AwayFromZero),
                    .DiscountAmount = Decimal.Round(Math.Max(0D, discounts(i)), 2, MidpointRounding.AwayFromZero),
                    .VatAmount = Decimal.Round(Math.Max(0D, vats(i)), 2, MidpointRounding.AwayFromZero)
                })
            Next

            result.Subtotal = Decimal.Round(Math.Max(0D, subtotal), 2, MidpointRounding.AwayFromZero)
            result.TotalDiscount = Decimal.Round(Math.Max(0D, totalDiscountTarget), 2, MidpointRounding.AwayFromZero)
            result.TotalVat = Decimal.Round(Math.Max(0D, totalVatTarget), 2, MidpointRounding.AwayFromZero)
            result.TotalDue = Decimal.Round(Math.Max(0D, result.Subtotal - result.TotalDiscount + result.TotalVat), 2, MidpointRounding.AwayFromZero)
            Return result
        End Function

        Private Class OrderHeaderInfo
            Public Property OrderId As Integer
            Public Property OrderNumber As String
            Public Property DealerId As Integer
            Public Property CustomerName As String
            Public Property CashierName As String
            Public Property CreatedOn As DateTime
            Public Property TotalAmount As Decimal
        End Class

        Private Class OrderPaymentSnapshot
            Public Property PaymentReference As String
            Public Property PaymentMethod As String
            Public Property PaidAmount As Decimal
            Public Property Notes As String
            Public Property CreatedBy As String
        End Class

        Private Class OrderFinancials
            Public Property Subtotal As Decimal
            Public Property DiscountAmount As Decimal
            Public Property DiscountPercent As Decimal
            Public Property TaxPercent As Decimal
            Public Property TaxAmount As Decimal
            Public Property TotalAmount As Decimal
        End Class

        Private Class ItemPricingLine
            Public Property LineTotal As Decimal
            Public Property DiscountAmount As Decimal
            Public Property VatAmount As Decimal
        End Class

        Private Class ItemWisePricingResult
            Public Sub New()
                Items = New List(Of ItemPricingLine)()
            End Sub

            Public Property Items As List(Of ItemPricingLine)
            Public Property Subtotal As Decimal
            Public Property TotalDiscount As Decimal
            Public Property TotalVat As Decimal
            Public Property TotalDue As Decimal
        End Class

        Private Class LedgerOrderSnapshot
            Public Property OrderId As Integer
            Public Property OrderNumber As String
            Public Property OrderDate As DateTime
            Public Property TotalAmount As Decimal
        End Class

        Private Class LedgerPaymentSnapshot
            Public Property PaymentId As Integer
            Public Property OrderId As Integer
            Public Property PaymentReference As String
            Public Property PaidAmount As Decimal
            Public Property CreatedOn As DateTime
        End Class
    End Class
End Namespace
