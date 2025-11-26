Imports System.Globalization
Imports System.IO
Imports iTextSharp.text
Imports iTextSharp.text.pdf
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Services
    Public Class ReceiptGenerator
        Private ReadOnly _rootPath As String
        Private Const ReceiptsFolderName As String = "recipts"

        Public Sub New(rootPath As String)
            If String.IsNullOrWhiteSpace(rootPath) Then Throw New ArgumentNullException(NameOf(rootPath))
            _rootPath = rootPath
        End Sub

        Public Function Generate(request As CheckoutRequest, result As CheckoutResult) As String
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            If result Is Nothing Then Throw New ArgumentNullException(NameOf(result))

            Dim receiptsDirectory = Path.Combine(_rootPath, ReceiptsFolderName)
            Directory.CreateDirectory(receiptsDirectory)

            Dim fileName = $"{result.ReceiptNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"
            Dim filePath = Path.Combine(receiptsDirectory, fileName)

            Using stream = New FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
                Dim document = New Document(PageSize.A4, 36.0F, 36.0F, 36.0F, 36.0F)
                PdfWriter.GetInstance(document, stream)
                document.Open()

                AddHeader(document, result)
                AddCustomerSection(document, request)
                AddLineItems(document, request)
                AddTotals(document, request, result)

                document.Close()
            End Using

            Return $"~/recipts/{fileName}"
        End Function

        Public Function GenerateSettlementReceipt(result As PendingPaymentResult) As String
            If result Is Nothing Then Throw New ArgumentNullException(NameOf(result))

            Dim receiptsDirectory = Path.Combine(_rootPath, ReceiptsFolderName)
            Directory.CreateDirectory(receiptsDirectory)

            Dim fileName = $"{result.ReceiptNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}_settlement.pdf"
            Dim filePath = Path.Combine(receiptsDirectory, fileName)

            Using stream = New FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None)
                Dim document = New Document(PageSize.A4, 36.0F, 36.0F, 36.0F, 36.0F)
                PdfWriter.GetInstance(document, stream)
                document.Open()

                AddSettlementHeader(document, result)
                AddSettlementDetails(document, result)

                document.Close()
            End Using

            Return $"~/recipts/{fileName}"
        End Function

        Private Shared Sub AddHeader(document As Document, result As CheckoutResult)
            Dim titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 18.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Sales Receipt", titleFont))
            document.Add(New Paragraph($"Order: {result.OrderNumber}"))
            document.Add(New Paragraph($"Receipt: {result.ReceiptNumber}"))
            document.Add(New Paragraph($"Issued: {DateTime.Now.ToString("f", CultureInfo.CurrentCulture)}"))
            AddSpacer(document)
        End Sub

        Private Shared Sub AddCustomerSection(document As Document, request As CheckoutRequest)
            Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Customer", sectionFont))
            document.Add(New Paragraph($"Name: {request.DealerName}"))
            document.Add(New Paragraph($"Payment method: {request.PaymentMethod}"))
            If request.PartialAmount.HasValue Then
                document.Add(New Paragraph($"Partial payment: {FormatCurrency(request.PartialAmount.Value)}"))
            End If
            document.Add(New Paragraph($"Cashier: {request.CreatedBy}"))
            AddSpacer(document)
        End Sub

        Private Shared Sub AddLineItems(document As Document, request As CheckoutRequest)
            Dim items = request.CartItems
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

        Private Shared Sub AddTotals(document As Document, request As CheckoutRequest, result As CheckoutResult)
            Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 12.0F, BaseColor.BLACK)
            document.Add(New Paragraph("Totals", sectionFont))
            document.Add(New Paragraph($"Subtotal: {FormatCurrency(request.Subtotal)}"))
            document.Add(New Paragraph($"Discount ({request.DiscountPercent.ToString("F2", CultureInfo.InvariantCulture)}%): {FormatCurrency(request.Subtotal * (request.DiscountPercent / 100D))}"))
            document.Add(New Paragraph($"Tax ({request.TaxPercent.ToString("F2", CultureInfo.InvariantCulture)}%): {FormatCurrency(request.TaxAmount)}"))
            document.Add(New Paragraph($"Total due: {FormatCurrency(request.TotalDue)}"))
            document.Add(New Paragraph($"Paid amount: {FormatCurrency(request.PaymentAmount)}"))

            If result.RemainingBalance > 0D Then
                document.Add(New Paragraph($"Outstanding: {FormatCurrency(result.RemainingBalance)}"))
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
    End Class
End Namespace
