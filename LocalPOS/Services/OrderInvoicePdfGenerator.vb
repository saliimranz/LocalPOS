Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports iTextSharp.text
Imports iTextSharp.text.pdf
Imports LocalPOS.LocalPOS.Models

Public Class OrderInvoicePdfGenerator
    Private Const DefaultMargin As Single = 28.0F

    Public Function Generate(order As OrderReceiptData, billToBlock As String, remarks As String) As ReportDocument
        If order Is Nothing Then
            Throw New ArgumentNullException(NameOf(order))
        End If

        Dim bytes As Byte()
        Using stream = New MemoryStream()
            Using document = New Document(PageSize.A4.Rotate(), DefaultMargin, DefaultMargin, DefaultMargin, DefaultMargin)
                PdfWriter.GetInstance(document, stream)
                document.Open()

                AddHeader(document, order)
                AddBillTo(document, billToBlock, order)
                AddLineItems(document, order.LineItems)
                AddTotals(document, order)
                AddRemarks(document, remarks, order)
            End Using

            bytes = stream.ToArray()
        End Using

        Dim invoiceNo = If(String.IsNullOrWhiteSpace(order.ReceiptNumber), order.OrderNumber, order.ReceiptNumber)
        Dim fileName = SanitizeFileName($"Invoice_{invoiceNo}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf")

        Dim payload As New ReportDocument() With {
            .FileName = fileName,
            .ContentType = "application/pdf",
            .Content = bytes
        }
        payload.EnsureValid()
        Return payload
    End Function

    Private Shared Sub AddHeader(document As Document, order As OrderReceiptData)
        Dim titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16.0F, BaseColor.BLACK)
        Dim metaFont = FontFactory.GetFont(FontFactory.HELVETICA, 10.0F, BaseColor.BLACK)

        document.Add(New Paragraph("Sales Tax Invoice", titleFont))
        document.Add(New Paragraph($"Order: {If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber)}", metaFont))

        Dim invoiceNo = If(String.IsNullOrWhiteSpace(order.ReceiptNumber), order.OrderNumber, order.ReceiptNumber)
        document.Add(New Paragraph($"Invoice: {If(String.IsNullOrWhiteSpace(invoiceNo), "-", invoiceNo)}", metaFont))
        document.Add(New Paragraph($"Date: {order.OrderDate.ToString("dd-MMM-yyyy", CultureInfo.CurrentCulture)}", metaFont))
        AddSpacer(document, 10.0F)
    End Sub

    Private Shared Sub AddBillTo(document As Document, billToBlock As String, order As OrderReceiptData)
        Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11.0F, BaseColor.BLACK)
        Dim bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10.0F, BaseColor.BLACK)

        document.Add(New Paragraph("Bill To", sectionFont))

        Dim block = billToBlock
        If String.IsNullOrWhiteSpace(block) Then
            block = If(String.IsNullOrWhiteSpace(order.CustomerName), "Walk-in Customer", order.CustomerName)
        End If

        For Each line In block.Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries).
                            Select(Function(l) l.Trim()).
                            Where(Function(l) Not String.IsNullOrWhiteSpace(l))
            document.Add(New Paragraph(line, bodyFont))
        Next

        AddSpacer(document, 10.0F)
    End Sub

    Private Shared Sub AddLineItems(document As Document, lineItems As IList(Of OrderLineItem))
        Dim items = If(lineItems, New List(Of OrderLineItem)())

        Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11.0F, BaseColor.BLACK)
        document.Add(New Paragraph("Items", sectionFont))
        AddSpacer(document, 6.0F)

        Dim headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.5F, BaseColor.WHITE)
        Dim bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 8.0F, BaseColor.BLACK)

        Dim table = New PdfPTable(12) With {.WidthPercentage = 100}
        table.SetWidths(New Single() {4.5F, 7.0F, 26.0F, 5.0F, 7.0F, 8.0F, 6.0F, 8.0F, 9.0F, 6.0F, 8.0F, 8.5F})

        AddHeaderCell(table, "S.No", headerFont)
        AddHeaderCell(table, "Part No", headerFont)
        AddHeaderCell(table, "Description", headerFont)
        AddHeaderCell(table, "Qty", headerFont)
        AddHeaderCell(table, "Rate", headerFont)
        AddHeaderCell(table, "Amount", headerFont)
        AddHeaderCell(table, "Disc %", headerFont)
        AddHeaderCell(table, "Disc Val", headerFont)
        AddHeaderCell(table, "After Disc", headerFont)
        AddHeaderCell(table, "VAT %", headerFont)
        AddHeaderCell(table, "VAT Val", headerFont)
        AddHeaderCell(table, "Net Amt", headerFont)

        If items.Count = 0 Then
            Dim cell = New PdfPCell(New Phrase("No line items available.", bodyFont)) With {
                .Colspan = 12,
                .Padding = 6.0F
            }
            table.AddCell(cell)
            document.Add(table)
            AddSpacer(document, 10.0F)
            Return
        End If

        For i = 0 To items.Count - 1
            Dim item = items(i)
            Dim gross = RoundMoney(Math.Max(0D, item.LineTotal))
            Dim itemDisc = RoundMoney(Math.Max(0D, item.DiscountAmount))
            Dim itemDiscPercent = If(gross > 0D AndAlso itemDisc > 0D, Decimal.Round((itemDisc / gross) * 100D, 2, MidpointRounding.AwayFromZero), 0D)
            Dim allocDisc = RoundMoney(Math.Max(0D, item.SubtotalDiscountAllocationAmount))
            Dim afterDisc = RoundMoney(Math.Max(0D, gross - itemDisc - allocDisc))
            Dim vatPercent = Decimal.Round(Math.Max(0D, item.TaxRate), 2, MidpointRounding.AwayFromZero)
            Dim vatValue = RoundMoney(Math.Max(0D, item.TaxAmount))
            Dim net = RoundMoney(afterDisc + vatValue)

            AddBodyCell(table, (i + 1).ToString(CultureInfo.InvariantCulture), bodyFont)
            AddBodyCell(table, If(item.ProductId > 0, item.ProductId.ToString(CultureInfo.InvariantCulture), ""), bodyFont)
            AddBodyCell(table, If(String.IsNullOrWhiteSpace(item.Name), "Item", item.Name), bodyFont)
            AddBodyCell(table, Math.Max(0, item.Quantity).ToString(CultureInfo.InvariantCulture), bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, FormatMoney(item.UnitPrice), bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, FormatMoney(gross), bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, $"{FormatPercent(itemDiscPercent)}%", bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, FormatMoney(itemDisc), bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, FormatMoney(afterDisc), bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, $"{FormatPercent(vatPercent)}%", bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, FormatMoney(vatValue), bodyFont, Element.ALIGN_RIGHT)
            AddBodyCell(table, FormatMoney(net), bodyFont, Element.ALIGN_RIGHT)
        Next

        document.Add(table)
        AddSpacer(document, 10.0F)
    End Sub

    Private Shared Sub AddTotals(document As Document, order As OrderReceiptData)
        Dim sectionFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11.0F, BaseColor.BLACK)
        Dim labelFont = FontFactory.GetFont(FontFactory.HELVETICA, 10.0F, BaseColor.BLACK)
        Dim valueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.0F, BaseColor.BLACK)

        document.Add(New Paragraph("Summary", sectionFont))
        AddSpacer(document, 6.0F)

        Dim subtotalGross = RoundMoney(Math.Max(0D, order.Subtotal))
        Dim itemDiscount = RoundMoney(Math.Max(0D, order.ItemDiscountAmount))
        Dim subtotalDiscount = RoundMoney(Math.Max(0D, order.SubtotalDiscountAmount))
        Dim totalDiscount = RoundMoney(Math.Min(subtotalGross, Math.Max(0D, itemDiscount + subtotalDiscount)))
        Dim subtotalAfterItem = RoundMoney(Math.Max(0D, subtotalGross - itemDiscount))
        Dim totalBeforeVat = RoundMoney(Math.Max(0D, subtotalGross - totalDiscount))
        Dim vatPercent = Decimal.Round(Math.Max(0D, order.TaxPercent), 2, MidpointRounding.AwayFromZero)
        Dim vatAmount = RoundMoney(Math.Max(0D, order.TaxAmount))
        Dim totalIncVat = RoundMoney(Math.Max(0D, order.TotalAmount))
        Dim totalPaid = RoundMoney(Math.Max(0D, order.PaidAmount))
        Dim amountDue = RoundMoney(Math.Max(0D, order.OutstandingAmount))
        Dim discountPercent = DetermineEffectiveDiscountPercent(order, subtotalGross, totalDiscount)

        Dim table = New PdfPTable(2) With {.WidthPercentage = 45, .HorizontalAlignment = Element.ALIGN_RIGHT}
        table.SetWidths(New Single() {65.0F, 35.0F})

        AddSummaryRow(table, "Sub Total", FormatMoney(subtotalGross), labelFont, valueFont)
        AddSummaryRow(table, "Sub Total (After Item Discount)", FormatMoney(subtotalAfterItem), labelFont, valueFont)
        AddSummaryRow(table, $"Discount ({FormatPercent(discountPercent)}%)", FormatMoney(totalDiscount), labelFont, valueFont)
        AddSummaryRow(table, "Total Before VAT", FormatMoney(totalBeforeVat), labelFont, valueFont)
        AddSummaryRow(table, $"VAT ({FormatPercent(vatPercent)}%)", FormatMoney(vatAmount), labelFont, valueFont)
        AddSummaryRow(table, "Total Inc VAT", FormatMoney(totalIncVat), labelFont, valueFont)
        AddSummaryRow(table, "Total Paid", FormatMoney(totalPaid), labelFont, valueFont)
        AddSummaryRow(table, "Amount Due", FormatMoney(amountDue), labelFont, valueFont)

        document.Add(table)
        AddSpacer(document, 10.0F)
    End Sub

    Private Shared Sub AddRemarks(document As Document, remarks As String, order As OrderReceiptData)
        Dim labelFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.0F, BaseColor.BLACK)
        Dim bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 10.0F, BaseColor.BLACK)

        Dim due = RoundMoney(Math.Max(0D, order.OutstandingAmount))
        Dim text = remarks
        If String.IsNullOrWhiteSpace(text) Then
            text = If(due > 0D, "Partial payment", "Paid in full")
        End If

        document.Add(New Paragraph("Remarks", labelFont))
        document.Add(New Paragraph(text, bodyFont))
    End Sub

    Private Shared Sub AddSummaryRow(table As PdfPTable, label As String, value As String, labelFont As Font, valueFont As Font)
        Dim left = New PdfPCell(New Phrase(label, labelFont)) With {
            .Border = Rectangle.NO_BORDER,
            .PaddingTop = 2.0F,
            .PaddingBottom = 2.0F
        }
        Dim right = New PdfPCell(New Phrase(value, valueFont)) With {
            .Border = Rectangle.NO_BORDER,
            .HorizontalAlignment = Element.ALIGN_RIGHT,
            .PaddingTop = 2.0F,
            .PaddingBottom = 2.0F
        }
        table.AddCell(left)
        table.AddCell(right)
    End Sub

    Private Shared Sub AddHeaderCell(table As PdfPTable, text As String, font As Font)
        Dim cell = New PdfPCell(New Phrase(text, font)) With {
            .BackgroundColor = New BaseColor(33, 37, 41),
            .HorizontalAlignment = Element.ALIGN_LEFT,
            .Padding = 5.0F
        }
        table.AddCell(cell)
    End Sub

    Private Shared Sub AddBodyCell(table As PdfPTable, text As String, font As Font, Optional align As Integer = Element.ALIGN_LEFT)
        Dim cell = New PdfPCell(New Phrase(If(text, String.Empty), font)) With {
            .HorizontalAlignment = align,
            .Padding = 4.0F,
            .BorderWidthBottom = 0.5F
        }
        table.AddCell(cell)
    End Sub

    Private Shared Sub AddSpacer(document As Document, height As Single)
        document.Add(New Paragraph(" ") With {.SpacingAfter = height})
    End Sub

    Private Shared Function DetermineEffectiveDiscountPercent(order As OrderReceiptData, subtotalGross As Decimal, totalDiscount As Decimal) As Decimal
        Dim candidate = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.DiscountPercent, 0D)), 2, MidpointRounding.AwayFromZero)
        If candidate > 0D Then
            Return candidate
        End If
        If subtotalGross <= 0D OrElse totalDiscount <= 0D Then
            Return 0D
        End If
        Return Decimal.Round((totalDiscount / subtotalGross) * 100D, 2, MidpointRounding.AwayFromZero)
    End Function

    Private Shared Function RoundMoney(amount As Decimal) As Decimal
        Return Decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
    End Function

    Private Shared Function FormatMoney(amount As Decimal) As String
        Return RoundMoney(Math.Max(0D, amount)).ToString("F2", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function FormatPercent(value As Decimal) As String
        Dim safe = Decimal.Round(Math.Max(0D, value), 2, MidpointRounding.AwayFromZero)
        Return safe.ToString("0.##", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function SanitizeFileName(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return $"invoice_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"
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

