Imports System.Globalization
Imports System.IO
Imports System.Text.RegularExpressions
Imports iTextSharp.text
Imports iTextSharp.text.pdf
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Services
    Public Class ReceiptGenerator
        Private Const DefaultMargin As Single = 36.0F

        Public Function Generate(order As OrderReceiptData) As ReceiptDocument
            If order Is Nothing Then Throw New ArgumentNullException(NameOf(order))

            Dim documentBytes As Byte()
            Using stream = New MemoryStream()
                Using document = New Document(PageSize.A4, DefaultMargin, DefaultMargin, DefaultMargin, DefaultMargin)
                    PdfWriter.GetInstance(document, stream)
                    document.Open()

                    AddOrderHeader(document, order)
                    AddCustomerSection(document, order)
                    AddLineItems(document, order.LineItems)
                    AddTotals(document, order)
                End Using
                documentBytes = stream.ToArray()
            End Using

            Dim fileName = SanitizeFileName($"{order.ReceiptNumber}_{order.OrderNumber}.pdf")
            Dim payload = New ReceiptDocument() With {
                .FileName = fileName,
                .Content = documentBytes
            }
            payload.EnsureValid()
            Return payload
        End Function

        Public Function GenerateSettlementReceipt(result As PendingPaymentResult) As ReceiptDocument
            If result Is Nothing Then Throw New ArgumentNullException(NameOf(result))

            Dim documentBytes As Byte()
            Using stream = New MemoryStream()
                Using document = New Document(PageSize.A4, DefaultMargin, DefaultMargin, DefaultMargin, DefaultMargin)
                    PdfWriter.GetInstance(document, stream)
                    document.Open()

                    AddSettlementHeader(document, result)
                    AddSettlementDetails(document, result)
                End Using
                documentBytes = stream.ToArray()
            End Using

            Dim fileName = SanitizeFileName($"{result.ReceiptNumber}_{result.OrderNumber}_settlement.pdf")
            Dim payload = New ReceiptDocument() With {
                .FileName = fileName,
                .Content = documentBytes
            }
            payload.EnsureValid()
            Return payload
        End Function

        Private Shared Sub AddOrderHeader(document As Document, order As OrderReceiptData)
            Dim titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Sales Receipt", titleFont))
            document.Add(New Paragraph($"Order: {order.OrderNumber}"))
            document.Add(New Paragraph($"Receipt: {order.ReceiptNumber}"))
            document.Add(New Paragraph($"Issued: {order.OrderDate.ToString("f", CultureInfo.CurrentCulture)}"))
            AddSpacer(document)
        End Sub

        Private Shared Sub AddCustomerSection(document As Document, order As OrderReceiptData)
            Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Customer", sectionFont))
            document.Add(New Paragraph($"Name: {order.CustomerName}"))
            document.Add(New Paragraph($"Payment method: {order.PaymentMethod}"))
            document.Add(New Paragraph($"Cashier: {order.CashierName}"))
            document.Add(New Paragraph($"Paid amount: {FormatCurrency(order.PaidAmount)}"))
            document.Add(New Paragraph($"Outstanding: {FormatCurrency(order.OutstandingAmount)}"))
            AddSpacer(document)
        End Sub

        Private Shared Sub AddLineItems(document As Document, lineItems As IList(Of OrderLineItem))
            Dim items = lineItems
            Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Items", sectionFont))

            If items Is Nothing OrElse items.Count = 0 Then
                document.Add(New Paragraph("No line items available."))
                AddSpacer(document)
                Return
            End If

            Dim table = New PdfPTable(4)
            table.WidthPercentage = 100
            table.SetWidths(New Single() {45.0F, 15.0F, 20.0F, 20.0F})

            AddHeaderCell(table, "Item")
            AddHeaderCell(table, "Qty")
            AddHeaderCell(table, "Unit")
            AddHeaderCell(table, "Line Total")

            For Each cartItem In items
                AddBodyCell(table, cartItem.Name)
                AddBodyCell(table, cartItem.Quantity.ToString(CultureInfo.InvariantCulture))
                AddBodyCell(table, FormatCurrency(cartItem.UnitPrice))
                AddBodyCell(table, FormatCurrency(cartItem.LineTotal))
            Next

            document.Add(table)
            AddSpacer(document)
        End Sub

        Private Shared Sub AddTotals(document As Document, order As OrderReceiptData)
            Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Totals", sectionFont))
            document.Add(New Paragraph($"Subtotal: {FormatCurrency(order.Subtotal)}"))
            If order.ItemDiscountAmount > 0D Then
                document.Add(New Paragraph($"Item discounts: {FormatCurrency(order.ItemDiscountAmount)}"))
            End If
            If order.SubtotalDiscountAmount > 0D Then
                document.Add(New Paragraph($"Subtotal discounts: {FormatCurrency(order.SubtotalDiscountAmount)}"))
            End If
            document.Add(New Paragraph($"Total discount ({order.DiscountPercent.ToString("F2", CultureInfo.InvariantCulture)}%): {FormatCurrency(order.DiscountAmount)}"))
            document.Add(New Paragraph($"Tax ({order.TaxPercent.ToString("F2", CultureInfo.InvariantCulture)}%): {FormatCurrency(order.TaxAmount)}"))
            document.Add(New Paragraph($"Total due: {FormatCurrency(order.TotalAmount)}"))
            document.Add(New Paragraph($"Paid amount: {FormatCurrency(order.PaidAmount)}"))

            If order.OutstandingAmount > 0D Then
                document.Add(New Paragraph($"Outstanding: {FormatCurrency(order.OutstandingAmount)}"))
            End If

            AddSpacer(document)
            document.Add(New Paragraph("Thank you for your purchase!"))
        End Sub

        Private Shared Sub AddSettlementHeader(document As Document, result As PendingPaymentResult)
            Dim titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Pending Payment Settlement", titleFont))
            document.Add(New Paragraph($"Order: {result.OrderNumber}"))
            document.Add(New Paragraph($"Receipt: {result.ReceiptNumber}"))
            document.Add(New Paragraph($"Customer: {result.DealerName}"))
            document.Add(New Paragraph($"Cashier: {result.CashierName}"))
            document.Add(New Paragraph($"Completed: {result.CompletedOn.ToString("f", CultureInfo.CurrentCulture)}"))
            AddSpacer(document)
        End Sub

        Private Shared Sub AddSettlementDetails(document As Document, result As PendingPaymentResult)
            Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Payment breakdown", sectionFont))
            document.Add(New Paragraph($"Order total: {FormatCurrency(result.TotalOrderAmount)}"))
            document.Add(New Paragraph($"Paid previously: {FormatCurrency(result.PreviouslyPaid)}"))
            document.Add(New Paragraph($"Outstanding before payment: {FormatCurrency(result.OutstandingBeforePayment)}"))
            document.Add(New Paragraph($"Settled now: {FormatCurrency(result.SettledAmount)}"))
            document.Add(New Paragraph($"Total paid to date: {FormatCurrency(result.PreviouslyPaid + result.SettledAmount)}"))
            document.Add(New Paragraph($"VAT rate: {result.VatPercent.ToString("F2", CultureInfo.InvariantCulture)}%"))
            document.Add(New Paragraph($"Payment method: {result.PaymentMethod}"))

            If result.PaymentMethod.Equals("Cash", StringComparison.OrdinalIgnoreCase) Then
                If result.CashReceived.HasValue Then
                    document.Add(New Paragraph($"Cash received: {FormatCurrency(result.CashReceived.Value)}"))
                End If
                If result.CashChange.HasValue Then
                    document.Add(New Paragraph($"Change returned: {FormatCurrency(result.CashChange.Value)}"))
                End If
            ElseIf result.PaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase) Then
                If Not String.IsNullOrWhiteSpace(result.CardRrn) Then
                    document.Add(New Paragraph($"RRN: {result.CardRrn}"))
                End If
                If Not String.IsNullOrWhiteSpace(result.CardAuthCode) Then
                    document.Add(New Paragraph($"Auth code: {result.CardAuthCode}"))
                End If
            End If

            AddSpacer(document)
            document.Add(New Paragraph("Balance is now fully settled."))
        End Sub

        Private Shared Sub AddSpacer(document As Document)
            document.Add(New Paragraph(" "))
        End Sub

        Private Shared Sub AddHeaderCell(table As PdfPTable, text As String)
            Dim font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11.0F, BaseColor.WHITE)
            Dim cell = New PdfPCell(New Phrase(text, font)) With {
                .BackgroundColor = New BaseColor(33, 37, 41),
                .HorizontalAlignment = Element.ALIGN_LEFT,
                .Padding = 6.0F
            }
            table.AddCell(cell)
        End Sub

        Private Shared Sub AddBodyCell(table As PdfPTable, text As String)
            Dim font = FontFactory.GetFont(FontFactory.HELVETICA, 10.0F, BaseColor.BLACK)
            Dim cell = New PdfPCell(New Phrase(text, font)) With {
                .HorizontalAlignment = Element.ALIGN_LEFT,
                .Padding = 6.0F,
                .BorderWidthBottom = 0.5F
            }
            table.AddCell(cell)
        End Sub

        Private Shared Function FormatCurrency(amount As Decimal) As String
            Return amount.ToString("C", CultureInfo.CurrentCulture)
        End Function

        Private Shared Function SanitizeFileName(raw As String) As String
            If String.IsNullOrWhiteSpace(raw) Then
                Return $"receipt_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"
            End If
            Dim invalid = New String(Path.GetInvalidFileNameChars())
            Dim pattern = $"[{Regex.Escape(invalid)}]"
            Dim cleaned = Regex.Replace(raw, pattern, "_")
            If Not cleaned.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) Then
                cleaned &= ".pdf"
            End If
            Return cleaned
        End Function
    End Class
End Namespace
