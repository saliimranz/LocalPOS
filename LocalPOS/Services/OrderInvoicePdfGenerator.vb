Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports iTextSharp.text
Imports iTextSharp.text.pdf
Imports LocalPOS.LocalPOS.Models

''' <summary>
''' Generates a PDF invoice that mirrors the INVOICE.xlsx layout (tables/merged headers/branding).
''' </summary>
Public Class OrderInvoicePdfGenerator
    Private Const Margin As Single = 18.0F

    Public Function Generate(order As OrderReceiptData, billToBlock As String, remarks As String, logoPngPath As String) As ReportDocument
        If order Is Nothing Then Throw New ArgumentNullException(NameOf(order))

        Dim bytes As Byte()
        Using stream As New MemoryStream()
            Using doc As New Document(PageSize.A4.Rotate(), Margin, Margin, Margin, Margin)
                PdfWriter.GetInstance(doc, stream)
                doc.Open()

                Dim items = If(order.LineItems, New List(Of OrderLineItem)())

                ' Ensure allocations exist so "Amount (After Discount)" matches the Excel invoice.
                EnsureSubtotalDiscountAllocations(items, RoundMoney(Math.Max(0D, order.SubtotalDiscountAmount)))

                ' Compute per-line after-discount with reconciliation (match OrderInvoiceGenerator behavior).
                Dim computedAfterDiscount = ComputeAfterDiscountPerLine(order, items)

                Dim invoiceNo = If(String.IsNullOrWhiteSpace(order.ReceiptNumber), order.OrderNumber, order.ReceiptNumber)

                AddTopHeader(doc, invoiceNo, logoPngPath)
                AddCustomerBlock(doc, order, billToBlock)
                AddItemsTable(doc, order, items, computedAfterDiscount)
                AddBottomSummary(doc, order, items, computedAfterDiscount, remarks)
            End Using

            bytes = stream.ToArray()
        End Using

        Dim safeInvoiceNo = If(String.IsNullOrWhiteSpace(order.ReceiptNumber), order.OrderNumber, order.ReceiptNumber)
        If String.IsNullOrWhiteSpace(safeInvoiceNo) Then safeInvoiceNo = "invoice"

        Dim payload As New ReportDocument() With {
            .FileName = SanitizeFileName($"Invoice_{safeInvoiceNo}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"),
            .ContentType = "application/pdf",
            .Content = bytes
        }
        payload.EnsureValid()
        Return payload
    End Function

    Private Shared Sub AddTopHeader(doc As Document, invoiceNo As String, logoPngPath As String)
        Dim table As New PdfPTable(3) With {.WidthPercentage = 100}
        table.SetWidths(New Single() {32.0F, 36.0F, 32.0F})

        ' Left: logo.
        Dim logoCell As New PdfPCell() With {.Border = Rectangle.NO_BORDER, .Padding = 0.0F, .HorizontalAlignment = Element.ALIGN_LEFT}
        If Not String.IsNullOrWhiteSpace(logoPngPath) AndAlso File.Exists(logoPngPath) Then
            Dim img = Image.GetInstance(logoPngPath)
            img.ScaleToFit(160.0F, 40.0F)
            img.Alignment = Image.ALIGN_LEFT
            logoCell.AddElement(img)
        End If
        table.AddCell(logoCell)

        ' Middle: empty (keeps layout consistent with template).
        table.AddCell(New PdfPCell(New Phrase(String.Empty, FontFactory.GetFont(FontFactory.HELVETICA, 8.0F))) With {
            .Border = Rectangle.NO_BORDER
        })

        ' Right: invoice no block.
        Dim rightTable As New PdfPTable(1) With {.WidthPercentage = 100}
        Dim small = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.0F, BaseColor.BLACK)
        Dim valueF = FontFactory.GetFont(FontFactory.HELVETICA, 8.0F, BaseColor.BLACK)

        rightTable.AddCell(New PdfPCell(New Phrase("Invoice No:", small)) With {.Border = Rectangle.NO_BORDER, .HorizontalAlignment = Element.ALIGN_RIGHT, .PaddingBottom = 0.0F})
        rightTable.AddCell(New PdfPCell(New Phrase(If(String.IsNullOrWhiteSpace(invoiceNo), "-", invoiceNo), valueF)) With {.Border = Rectangle.NO_BORDER, .HorizontalAlignment = Element.ALIGN_RIGHT, .PaddingTop = 0.0F})

        Dim rightCell As New PdfPCell(rightTable) With {.Border = Rectangle.NO_BORDER, .Padding = 0.0F}
        table.AddCell(rightCell)

        doc.Add(table)

        ' Center title bar.
        Dim titleTable As New PdfPTable(1) With {.WidthPercentage = 100}
        Dim titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.5F, BaseColor.BLACK)
        titleTable.AddCell(New PdfPCell(New Phrase("Sales Tax Invoice", titleFont)) With {
            .HorizontalAlignment = Element.ALIGN_CENTER,
            .PaddingTop = 4.0F,
            .PaddingBottom = 4.0F,
            .BorderWidth = 0.8F
        })
        titleTable.SpacingBefore = 2.0F
        titleTable.SpacingAfter = 4.0F
        doc.Add(titleTable)
    End Sub

    Private Shared Sub AddCustomerBlock(doc As Document, order As OrderReceiptData, billToBlock As String)
        Dim outer As New PdfPTable(2) With {.WidthPercentage = 100}
        outer.SetWidths(New Single() {70.0F, 30.0F})

        Dim font = FontFactory.GetFont(FontFactory.HELVETICA, 8.0F, BaseColor.BLACK)
        Dim bold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 8.0F, BaseColor.BLACK)

        ' Left block (Customer/Address/ID/Tel).
        Dim left As New PdfPTable(2) With {.WidthPercentage = 100}
        left.SetWidths(New Single() {18.0F, 82.0F})

        Dim customerName As String = Nothing
        Dim address As String = Nothing
        Dim phone As String = Nothing
        ExtractCustomerBlocks(billToBlock, order.CustomerName, customerName, address, phone)

        AddLabelValue(left, "Customer :", customerName, bold, font)
        AddLabelValue(left, "Address :", address, bold, font)
        AddLabelValue(left, "Customer ID :", If(order.DealerId > 0, order.DealerId.ToString(CultureInfo.InvariantCulture), String.Empty), bold, font)
        AddLabelValue(left, "Tel No :", phone, bold, font)

        outer.AddCell(WrapInBorderCell(left))

        ' Right block (Order No / Date).
        Dim right As New PdfPTable(2) With {.WidthPercentage = 100}
        right.SetWidths(New Single() {45.0F, 55.0F})
        AddLabelValue(right, "Order No :", If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber), bold, font, Element.ALIGN_LEFT, Element.ALIGN_RIGHT)
        AddLabelValue(right, "Date :", order.OrderDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture), bold, font, Element.ALIGN_LEFT, Element.ALIGN_RIGHT)
        outer.AddCell(WrapInBorderCell(right))

        outer.SpacingAfter = 6.0F
        doc.Add(outer)
    End Sub

    Private Shared Sub AddItemsTable(doc As Document, order As OrderReceiptData, items As IList(Of OrderLineItem), computedAfterDiscount As IList(Of Decimal))
        Dim table As New PdfPTable(12) With {.WidthPercentage = 100}
        table.SetWidths(New Single() {5.5F, 9.0F, 21.0F, 6.0F, 8.0F, 9.0F, 6.0F, 8.0F, 10.0F, 6.0F, 8.0F, 9.5F})

        Dim headerBg As New BaseColor(230, 230, 230)
        Dim headerFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.5F, BaseColor.BLACK)
        Dim subHeaderFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.0F, BaseColor.BLACK)
        Dim bodyFont = FontFactory.GetFont(FontFactory.HELVETICA, 7.2F, BaseColor.BLACK)

        ' Header row 1 with merged group titles.
        AddHeaderCell(table, "SNO", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "PART NO", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "PRODUCT NAME", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "QTY", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "RATE", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "AMOUNT", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "ITEM DISCOUNT", headerFont, headerBg, colspan:=2)
        AddHeaderCell(table, "AMOUNT (After Discount)", headerFont, headerBg, rowspan:=2)
        AddHeaderCell(table, "VAT", headerFont, headerBg, colspan:=2)
        AddHeaderCell(table, "NET AMT", headerFont, headerBg, rowspan:=2)

        ' Header row 2 (sub-columns).
        AddHeaderCell(table, "%", subHeaderFont, headerBg)
        AddHeaderCell(table, "VAL", subHeaderFont, headerBg)
        AddHeaderCell(table, "%", subHeaderFont, headerBg)
        AddHeaderCell(table, "VAL", subHeaderFont, headerBg)

        Dim safeItems = If(items, New List(Of OrderLineItem)())
        If safeItems.Count = 0 Then
            Dim msg = New PdfPCell(New Phrase("No line items available.", bodyFont)) With {
                .Colspan = 12,
                .HorizontalAlignment = Element.ALIGN_LEFT,
                .PaddingTop = 6.0F,
                .PaddingBottom = 6.0F,
                .BorderWidth = 0.8F
            }
            table.AddCell(msg)
        Else
            For i = 0 To safeItems.Count - 1
                Dim it = safeItems(i)
                Dim gross = RoundMoney(Math.Max(0D, it.LineTotal))
                Dim itemDisc = RoundMoney(Math.Max(0D, it.DiscountAmount))
                Dim itemDiscPercent = If(gross > 0D AndAlso itemDisc > 0D, Decimal.Round((itemDisc / gross) * 100D, 2, MidpointRounding.AwayFromZero), 0D)
                Dim afterDisc = computedAfterDiscount(i)
                Dim vatPercent = Decimal.Round(Math.Max(0D, it.TaxRate), 2, MidpointRounding.AwayFromZero)
                Dim vatVal = RoundMoney(Math.Max(0D, it.TaxAmount))
                Dim net = RoundMoney(Math.Max(0D, afterDisc + vatVal))

                AddBodyCell(table, (i + 1).ToString(CultureInfo.InvariantCulture), bodyFont, Element.ALIGN_CENTER)
                AddBodyCell(table, If(it.ProductId > 0, it.ProductId.ToString(CultureInfo.InvariantCulture), String.Empty), bodyFont, Element.ALIGN_CENTER)
                AddBodyCell(table, If(String.IsNullOrWhiteSpace(it.Name), String.Empty, it.Name), bodyFont, Element.ALIGN_LEFT)
                AddBodyCell(table, Math.Max(0, it.Quantity).ToString(CultureInfo.InvariantCulture), bodyFont, Element.ALIGN_CENTER)
                AddBodyCell(table, FormatNumber(it.UnitPrice), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatNumber(gross), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatPercent(itemDiscPercent), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatNumber(itemDisc), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatNumber(afterDisc), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatPercent(vatPercent), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatNumber(vatVal), bodyFont, Element.ALIGN_RIGHT)
                AddBodyCell(table, FormatNumber(net), bodyFont, Element.ALIGN_RIGHT)
            Next
        End If

        ' Totals row (mirrors template's totals line).
        Dim subtotalGross = RoundMoney(Math.Max(0D, order.Subtotal))
        Dim itemDiscount = RoundMoney(Math.Max(0D, order.ItemDiscountAmount))
        Dim subtotalDiscount = RoundMoney(Math.Max(0D, order.SubtotalDiscountAmount))
        Dim totalDiscount = RoundMoney(Math.Min(subtotalGross, Math.Max(0D, itemDiscount + subtotalDiscount)))
        Dim totalBeforeVat = RoundMoney(Math.Max(0D, subtotalGross - totalDiscount))
        Dim vatAmount = RoundMoney(Math.Max(0D, order.TaxAmount))
        Dim totalIncVat = RoundMoney(Math.Max(0D, order.TotalAmount))
        Dim vatPercent = Decimal.Round(Math.Max(0D, order.TaxPercent), 2, MidpointRounding.AwayFromZero)
        Dim avgItemDiscountPercent = If(subtotalGross > 0D AndAlso itemDiscount > 0D, Decimal.Round((itemDiscount / subtotalGross) * 100D, 2, MidpointRounding.AwayFromZero), 0D)

        Dim totalsBg As New BaseColor(245, 245, 245)
        AddBodyCell(table, String.Empty, bodyFont, Element.ALIGN_CENTER, totalsBg)
        AddBodyCell(table, String.Empty, bodyFont, Element.ALIGN_CENTER, totalsBg)
        AddBodyCell(table, String.Empty, bodyFont, Element.ALIGN_LEFT, totalsBg)
        AddBodyCell(table, String.Empty, bodyFont, Element.ALIGN_CENTER, totalsBg)
        AddBodyCell(table, String.Empty, bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatNumber(subtotalGross), bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatPercent(avgItemDiscountPercent), bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatNumber(itemDiscount), bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatNumber(totalBeforeVat), bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatPercent(vatPercent), bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatNumber(vatAmount), bodyFont, Element.ALIGN_RIGHT, totalsBg)
        AddBodyCell(table, FormatNumber(totalIncVat), bodyFont, Element.ALIGN_RIGHT, totalsBg)

        doc.Add(table)
        table.SpacingAfter = 8.0F
    End Sub

    Private Shared Sub AddBottomSummary(doc As Document, order As OrderReceiptData, items As IList(Of OrderLineItem), computedAfterDiscount As IList(Of Decimal), remarks As String)
        Dim outer As New PdfPTable(2) With {.WidthPercentage = 100}
        outer.SetWidths(New Single() {72.0F, 28.0F})

        Dim font = FontFactory.GetFont(FontFactory.HELVETICA, 7.8F, BaseColor.BLACK)
        Dim bold = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 7.8F, BaseColor.BLACK)

        Dim subtotalGross = RoundMoney(Math.Max(0D, order.Subtotal))
        Dim itemDiscount = RoundMoney(Math.Max(0D, order.ItemDiscountAmount))
        Dim subtotalDiscount = RoundMoney(Math.Max(0D, order.SubtotalDiscountAmount))
        Dim subtotalAfterItem = RoundMoney(Math.Max(0D, subtotalGross - itemDiscount))
        Dim totalBeforeVat = RoundMoney(Math.Max(0D, subtotalAfterItem - subtotalDiscount))
        Dim vatPercent = Decimal.Round(Math.Max(0D, order.TaxPercent), 2, MidpointRounding.AwayFromZero)
        Dim vatAmount = RoundMoney(Math.Max(0D, order.TaxAmount))
        Dim totalIncVat = RoundMoney(Math.Max(0D, order.TotalAmount))
        Dim paid = RoundMoney(Math.Max(0D, order.PaidAmount))
        Dim due = RoundMoney(Math.Max(0D, order.OutstandingAmount))
        Dim subtotalDiscountPercent = DetermineSubtotalDiscountPercent(subtotalAfterItem, subtotalDiscount)

        ' Left block: Amount in words + remarks line.
        Dim left As New PdfPTable(2) With {.WidthPercentage = 100}
        left.SetWidths(New Single() {8.0F, 92.0F})
        Dim words = ToAedWords(totalIncVat)
        left.AddCell(New PdfPCell(New Phrase("DHS", bold)) With {.Border = Rectangle.NO_BORDER, .PaddingTop = 6.0F, .PaddingLeft = 6.0F})
        left.AddCell(New PdfPCell(New Phrase(words, font)) With {.Border = Rectangle.NO_BORDER, .PaddingTop = 6.0F})

        ' spacer row
        left.AddCell(New PdfPCell(New Phrase(String.Empty, font)) With {.Border = Rectangle.NO_BORDER, .Colspan = 2, .FixedHeight = 22.0F})

        Dim remarksText = remarks
        If String.IsNullOrWhiteSpace(remarksText) Then
            remarksText = If(due > 0D, "Status: Partial payment", "Status: Paid in full")
        End If
        left.AddCell(New PdfPCell(New Phrase("Remarks", font)) With {.Border = Rectangle.NO_BORDER, .PaddingLeft = 6.0F, .PaddingBottom = 6.0F})
        left.AddCell(New PdfPCell(New Phrase(remarksText, font)) With {.Border = Rectangle.NO_BORDER, .PaddingBottom = 6.0F})

        outer.AddCell(WrapInBorderCell(left))

        ' Right block: summary table.
        Dim right As New PdfPTable(2) With {.WidthPercentage = 100}
        right.SetWidths(New Single() {64.0F, 36.0F})

        AddSummaryRow(right, "Sub Total", FormatNumber(subtotalGross), bold, bold)
        AddSummaryRow(right, "Sub Total(After Item Discount)", FormatNumber(subtotalAfterItem), bold, bold)
        AddSummaryRow(right, $"Discount({FormatPercent(subtotalDiscountPercent)}%)", FormatNumber(subtotalDiscount), bold, bold)
        AddSummaryRow(right, "Total Before VAT", FormatNumber(totalBeforeVat), bold, bold)
        AddSummaryRow(right, $"VAT({FormatPercent(vatPercent)}%)", FormatNumber(vatAmount), bold, bold)
        AddSummaryRow(right, "Total inc VAT", FormatNumber(totalIncVat), bold, bold)
        AddSummaryRow(right, "Total Paid", FormatNumber(paid), bold, bold)
        AddSummaryRow(right, "Amount Due", FormatNumber(due), bold, bold)

        outer.AddCell(WrapInBorderCell(right))
        doc.Add(outer)
    End Sub

    Private Shared Sub AddLabelValue(t As PdfPTable,
                                     label As String,
                                     value As String,
                                     labelFont As Font,
                                     valueFont As Font,
                                     Optional labelAlign As Integer = Element.ALIGN_LEFT,
                                     Optional valueAlign As Integer = Element.ALIGN_LEFT)
        t.AddCell(New PdfPCell(New Phrase(label, labelFont)) With {.Border = Rectangle.NO_BORDER, .HorizontalAlignment = labelAlign, .PaddingLeft = 6.0F, .PaddingTop = 3.0F, .PaddingBottom = 3.0F})
        t.AddCell(New PdfPCell(New Phrase(If(value, String.Empty), valueFont)) With {.Border = Rectangle.NO_BORDER, .HorizontalAlignment = valueAlign, .PaddingTop = 3.0F, .PaddingBottom = 3.0F})
    End Sub

    Private Shared Function WrapInBorderCell(inner As PdfPTable) As PdfPCell
        Return New PdfPCell(inner) With {.BorderWidth = 0.8F, .Padding = 0.0F}
    End Function

    Private Shared Sub AddHeaderCell(t As PdfPTable, text As String, font As Font, bg As BaseColor, Optional colspan As Integer = 1, Optional rowspan As Integer = 1)
        Dim cell As New PdfPCell(New Phrase(text, font)) With {
            .BackgroundColor = bg,
            .HorizontalAlignment = Element.ALIGN_CENTER,
            .VerticalAlignment = Element.ALIGN_MIDDLE,
            .PaddingTop = 3.0F,
            .PaddingBottom = 3.0F,
            .BorderWidth = 0.8F,
            .Colspan = colspan,
            .Rowspan = rowspan
        }
        t.AddCell(cell)
    End Sub

    Private Shared Sub AddBodyCell(t As PdfPTable, text As String, font As Font, align As Integer, Optional bg As BaseColor = Nothing)
        Dim cell As New PdfPCell(New Phrase(If(text, String.Empty), font)) With {
            .HorizontalAlignment = align,
            .VerticalAlignment = Element.ALIGN_MIDDLE,
            .PaddingTop = 3.0F,
            .PaddingBottom = 3.0F,
            .PaddingLeft = 3.0F,
            .PaddingRight = 3.0F,
            .BorderWidth = 0.8F
        }
        If bg IsNot Nothing Then
            cell.BackgroundColor = bg
        End If
        t.AddCell(cell)
    End Sub

    Private Shared Sub AddSummaryRow(t As PdfPTable, label As String, value As String, labelFont As Font, valueFont As Font)
        t.AddCell(New PdfPCell(New Phrase(label, labelFont)) With {.Border = Rectangle.NO_BORDER, .PaddingTop = 2.0F, .PaddingBottom = 2.0F, .PaddingLeft = 6.0F})
        t.AddCell(New PdfPCell(New Phrase(value, valueFont)) With {.Border = Rectangle.NO_BORDER, .HorizontalAlignment = Element.ALIGN_RIGHT, .PaddingTop = 2.0F, .PaddingBottom = 2.0F, .PaddingRight = 6.0F})
    End Sub

    Private Shared Function ComputeAfterDiscountPerLine(order As OrderReceiptData, items As IList(Of OrderLineItem)) As List(Of Decimal)
        Dim lineItems = If(items, New List(Of OrderLineItem)())

        Dim subtotalGross = RoundMoney(Math.Max(0D, If(order IsNot Nothing, order.Subtotal, 0D)))
        Dim itemDiscountTotal = RoundMoney(Math.Max(0D, If(order IsNot Nothing, order.ItemDiscountAmount, 0D)))
        Dim subtotalDiscountTotal = RoundMoney(Math.Max(0D, If(order IsNot Nothing, order.SubtotalDiscountAmount, 0D)))
        Dim expectedAfterDiscountTotal = RoundMoney(Math.Max(0D, subtotalGross - itemDiscountTotal - subtotalDiscountTotal))

        Dim computed As New List(Of Decimal)()
        For Each it In lineItems
            Dim gross = RoundMoney(Math.Max(0D, it.LineTotal))
            Dim itemDisc = RoundMoney(Math.Max(0D, it.DiscountAmount))
            Dim allocDisc = RoundMoney(Math.Max(0D, it.SubtotalDiscountAllocationAmount))
            Dim afterDisc = RoundMoney(Math.Max(0D, gross - itemDisc - allocDisc))
            computed.Add(afterDisc)
        Next

        If computed.Count > 0 Then
            Dim delta = RoundMoney(expectedAfterDiscountTotal - computed.Sum())
            If delta <> 0D Then
                computed(computed.Count - 1) = RoundMoney(Math.Max(0D, computed(computed.Count - 1) + delta))
            End If
        End If

        Return computed
    End Function

    Private Shared Sub EnsureSubtotalDiscountAllocations(items As IList(Of OrderLineItem), subtotalDiscountTotal As Decimal)
        If items Is Nothing OrElse items.Count = 0 Then
            Return
        End If

        Dim existing = RoundMoney(items.Sum(Function(i) Math.Max(0D, i.SubtotalDiscountAllocationAmount)))
        If existing > 0D OrElse subtotalDiscountTotal <= 0D Then
            Return
        End If

        Dim bases = items.Select(Function(i) RoundMoney(Math.Max(0D, i.LineTotal - Math.Max(0D, i.DiscountAmount)))).ToList()
        Dim baseSum = bases.Sum()
        Dim allocations As New List(Of Decimal)()
        If baseSum <= 0D Then
            Dim even = RoundMoney(subtotalDiscountTotal / items.Count)
            For i = 0 To items.Count - 1
                allocations.Add(even)
            Next
        Else
            For i = 0 To items.Count - 1
                allocations.Add(RoundMoney((bases(i) / baseSum) * subtotalDiscountTotal))
            Next
        End If

        Dim delta = RoundMoney(subtotalDiscountTotal - allocations.Sum())
        If allocations.Count > 0 AndAlso delta <> 0D Then
            allocations(allocations.Count - 1) = RoundMoney(Math.Max(0D, allocations(allocations.Count - 1) + delta))
        End If

        For i = 0 To items.Count - 1
            items(i).SubtotalDiscountAllocationAmount = allocations(i)
        Next
    End Sub

    Private Shared Sub ExtractCustomerBlocks(billToBlock As String, fallbackCustomerName As String, ByRef customerName As String, ByRef address As String, ByRef phone As String)
        Dim safeFallbackName = If(String.IsNullOrWhiteSpace(fallbackCustomerName), "Walk-in Customer", fallbackCustomerName)
        customerName = safeFallbackName
        address = String.Empty
        phone = String.Empty

        If String.IsNullOrWhiteSpace(billToBlock) Then
            Return
        End If

        Dim lines = billToBlock.Split(New String() {Environment.NewLine, vbLf}, StringSplitOptions.RemoveEmptyEntries) _
                               .Select(Function(l) l.Trim()) _
                               .Where(Function(l) Not String.IsNullOrWhiteSpace(l)) _
                               .ToList()
        If lines.Count = 0 Then
            Return
        End If

        customerName = lines(0)
        Dim addressParts As New List(Of String)()
        For i = 1 To lines.Count - 1
            Dim line = lines(i)
            If line.StartsWith("Phone:", StringComparison.OrdinalIgnoreCase) Then
                phone = line.Substring("Phone:".Length).Trim()
            Else
                addressParts.Add(line)
            End If
        Next

        address = String.Join(" ", addressParts)
    End Sub

    Private Shared Function DetermineSubtotalDiscountPercent(subtotalAfterItem As Decimal, subtotalDiscount As Decimal) As Decimal
        If subtotalAfterItem <= 0D OrElse subtotalDiscount <= 0D Then
            Return 0D
        End If
        Return Decimal.Round((subtotalDiscount / subtotalAfterItem) * 100D, 2, MidpointRounding.AwayFromZero)
    End Function

    Private Shared Function RoundMoney(amount As Decimal) As Decimal
        Return Decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
    End Function

    Private Shared Function FormatNumber(value As Decimal) As String
        Dim safe = RoundMoney(value)
        Return safe.ToString("0.##", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function FormatPercent(value As Decimal) As String
        Dim safe = Decimal.Round(Math.Max(0D, value), 2, MidpointRounding.AwayFromZero)
        Return safe.ToString("0.##", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function ToAedWords(amount As Decimal) As String
        Dim safe = RoundMoney(Math.Max(0D, amount))
        Dim dirhams = CInt(Math.Floor(safe))
        Dim fils = CInt(Math.Round((safe - dirhams) * 100D, 0, MidpointRounding.AwayFromZero))

        Dim dirhamWords = If(dirhams = 0, "Zero", NumberToWords(dirhams))
        Dim filsWords = If(fils = 0, "Zero", NumberToWords(fils))

        Return $"DHS {dirhamWords} AND {filsWords} FILS ONLY"
    End Function

    Private Shared Function NumberToWords(number As Integer) As String
        If number = 0 Then
            Return "Zero"
        End If
        If number < 0 Then
            Return "Minus " & NumberToWords(Math.Abs(number))
        End If

        Dim words As String = ""

        If (number \ 1000000) > 0 Then
            words &= NumberToWords(number \ 1000000) & " Million "
            number = number Mod 1000000
        End If

        If (number \ 1000) > 0 Then
            words &= NumberToWords(number \ 1000) & " Thousand "
            number = number Mod 1000
        End If

        If (number \ 100) > 0 Then
            words &= NumberToWords(number \ 100) & " Hundred "
            number = number Mod 100
        End If

        If number > 0 Then
            If words <> "" Then
                words &= "And "
            End If

            Dim unitsMap() As String = {"Zero", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine", "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen"}
            Dim tensMap() As String = {"Zero", "Ten", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety"}

            If number < 20 Then
                words &= unitsMap(number)
            Else
                words &= tensMap(number \ 10)
                If (number Mod 10) > 0 Then
                    words &= "-" & unitsMap(number Mod 10)
                End If
            End If
        End If

        Return words.Trim()
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

