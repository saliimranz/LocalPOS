Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports ClosedXML.Excel
Imports LocalPOS.LocalPOS.Models

Public Class OrderInvoiceGenerator
    ' INVOICE.xlsx template mapping (Sales Tax Invoice - Revamped discounts)
    Private Const LineItemsStartRow As Integer = 15
    ' The current template contains 6 pre-defined product rows (15-20).
    Private Const DefaultLineItemRows As Integer = 6
    Private Const TotalsRowIndex As Integer = 21
    Private Const SubtotalRowIndex As Integer = 24
    Private Const SubtotalAfterItemDiscountRowIndex As Integer = 25
    Private Const SubtotalDiscountRowIndex As Integer = 26
    Private Const TotalBeforeVatRowIndex As Integer = 27
    Private Const RemarksRowIndex As Integer = 28
    Private Const VatRowIndex As Integer = 28
    Private Const TotalIncVatRowIndex As Integer = 29
    Private Const TotalPaidRowIndex As Integer = 30
    Private Const AmountDueRowIndex As Integer = 31

    Private Const SnoColumn As Integer = 1 ' A
    Private Const PartNoColumn As Integer = 2 ' B
    Private Const ProductNameColumn As Integer = 3 ' C
    Private Const QtyColumn As Integer = 4 ' D
    Private Const RateColumn As Integer = 5 ' E
    Private Const AmountColumn As Integer = 6 ' F
    Private Const ItemDiscountPercentColumn As Integer = 7 ' G
    Private Const ItemDiscountValueColumn As Integer = 8 ' H
    Private Const AmountAfterDiscountColumn As Integer = 9 ' I
    Private Const VatPercentColumn As Integer = 10 ' J
    Private Const VatValueColumn As Integer = 11 ' K
    Private Const NetAmountColumn As Integer = 12 ' L

    Private Const InvoiceNumberCellAddress As String = "G6"
    Private Const CustomerNameCellAddress As String = "B8"
    Private Const CustomerAddressCellAddress As String = "B9"
    Private Const CustomerIdCellAddress As String = "B10"
    Private Const CustomerTelCellAddress As String = "B11"
    Private Const OrderNumberCellAddress As String = "K8"
    Private Const DateCellAddress As String = "K9"
    ' NOTE: Amount-in-words and remarks cells move down when extra rows are inserted.
    Private Const DateFormat As String = "dd-MMM-yyyy"

    Public Function Generate(order As OrderReceiptData, templatePath As String, billToBlock As String, remarks As String) As ReportDocument
        If order Is Nothing Then
            Throw New ArgumentNullException(NameOf(order))
        End If
        If String.IsNullOrWhiteSpace(templatePath) Then
            Throw New ArgumentNullException(NameOf(templatePath))
        End If
        If Not File.Exists(templatePath) Then
            Throw New FileNotFoundException("Invoice template was not found.", templatePath)
        End If

        Using workbook = New XLWorkbook(templatePath)
            Dim worksheet = workbook.Worksheet(1)
            PopulateHeader(worksheet, order, billToBlock)
            Dim extraRows = PopulateLineItems(worksheet, order)
            PopulateTotalsAndSummary(worksheet, order, remarks, extraRows)

            Using stream As New MemoryStream()
                workbook.SaveAs(stream)
                Dim document As New ReportDocument() With {
                    .FileName = $"Invoice_{order.OrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
                    .ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    .Content = stream.ToArray()
                }
                document.EnsureValid()
                Return document
            End Using
        End Using
    End Function

    Private Shared Sub PopulateHeader(worksheet As IXLWorksheet, order As OrderReceiptData, billToBlock As String)
        Dim invoiceNo = If(String.IsNullOrWhiteSpace(order.ReceiptNumber), order.OrderNumber, order.ReceiptNumber)
        worksheet.Cell(InvoiceNumberCellAddress).Value = If(String.IsNullOrWhiteSpace(invoiceNo), "-", invoiceNo)

        Dim orderNo = If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber)
        worksheet.Cell(OrderNumberCellAddress).Value = orderNo

        Dim dateCell = worksheet.Cell(DateCellAddress)
        dateCell.Value = order.OrderDate
        dateCell.Style.DateFormat.Format = DateFormat

        Dim customerName As String = Nothing
        Dim address As String = Nothing
        Dim phone As String = Nothing
        ExtractCustomerBlocks(billToBlock, order.CustomerName, customerName, address, phone)

        worksheet.Cell(CustomerNameCellAddress).Value = customerName
        worksheet.Cell(CustomerNameCellAddress).Style.Alignment.WrapText = True
        worksheet.Cell(CustomerAddressCellAddress).Value = address
        worksheet.Cell(CustomerAddressCellAddress).Style.Alignment.WrapText = True

        worksheet.Cell(CustomerIdCellAddress).Value = If(order.DealerId > 0, order.DealerId.ToString(CultureInfo.InvariantCulture), "-")
        worksheet.Cell(CustomerTelCellAddress).Value = phone
    End Sub

    Private Shared Function PopulateLineItems(worksheet As IXLWorksheet, order As OrderReceiptData) As Integer
        Dim lineItems = If(order IsNot Nothing AndAlso order.LineItems IsNot Nothing, order.LineItems, New List(Of OrderLineItem)())

        Dim rowsRequired = Math.Max(lineItems.Count, DefaultLineItemRows)
        Dim extraRows = rowsRequired - DefaultLineItemRows
        If extraRows > 0 Then
            worksheet.Row(TotalsRowIndex).InsertRowsAbove(extraRows)
        End If

        Dim lastDataRow = LineItemsStartRow + rowsRequired - 1
        For r = LineItemsStartRow To lastDataRow
            worksheet.Cell(r, SnoColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, PartNoColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, ProductNameColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, QtyColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, RateColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, AmountColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, ItemDiscountPercentColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, ItemDiscountValueColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, AmountAfterDiscountColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, VatPercentColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, VatValueColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, NetAmountColumn).Clear(XLClearOptions.Contents)
        Next

        ' If allocations were not hydrated (e.g. legacy order, allocation table missing),
        ' fall back to proportionally distributing the order-level subtotal discount across lines.
        EnsureSubtotalDiscountAllocations(lineItems, Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.SubtotalDiscountAmount, 0D)), 2, MidpointRounding.AwayFromZero))

        ' Compute a reconciled per-line base-after-discount so totals match the order summary.
        Dim subtotalGross = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.Subtotal, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim itemDiscountTotal = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.ItemDiscountAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim subtotalDiscountTotal = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.SubtotalDiscountAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim expectedAfterDiscountTotal = Decimal.Round(Math.Max(0D, subtotalGross - itemDiscountTotal - subtotalDiscountTotal), 2, MidpointRounding.AwayFromZero)

        Dim computedAfterDiscount = New List(Of Decimal)()
        For Each item In lineItems
            Dim gross = Decimal.Round(Math.Max(0D, item.LineTotal), 2, MidpointRounding.AwayFromZero)
            Dim itemDisc = Decimal.Round(Math.Max(0D, item.DiscountAmount), 2, MidpointRounding.AwayFromZero)
            Dim allocDisc = Decimal.Round(Math.Max(0D, item.SubtotalDiscountAllocationAmount), 2, MidpointRounding.AwayFromZero)
            Dim afterDisc = Decimal.Round(Math.Max(0D, gross - itemDisc - allocDisc), 2, MidpointRounding.AwayFromZero)
            computedAfterDiscount.Add(afterDisc)
        Next

        If lineItems.Count > 0 Then
            Dim delta = Decimal.Round(expectedAfterDiscountTotal - computedAfterDiscount.Sum(), 2, MidpointRounding.AwayFromZero)
            If delta <> 0D Then
                Dim lastIndex = computedAfterDiscount.Count - 1
                computedAfterDiscount(lastIndex) = Decimal.Round(Math.Max(0D, computedAfterDiscount(lastIndex) + delta), 2, MidpointRounding.AwayFromZero)
            End If
        End If

        Dim currentRow = LineItemsStartRow
        For i = 0 To lineItems.Count - 1
            Dim item = lineItems(i)
            Dim gross = Decimal.Round(Math.Max(0D, item.LineTotal), 2, MidpointRounding.AwayFromZero)
            Dim itemDisc = Decimal.Round(Math.Max(0D, item.DiscountAmount), 2, MidpointRounding.AwayFromZero)
            Dim itemDiscPercent = 0D
            If gross > 0D AndAlso itemDisc > 0D Then
                itemDiscPercent = Decimal.Round((itemDisc / gross) * 100D, 2, MidpointRounding.AwayFromZero)
            End If
            Dim afterDisc = computedAfterDiscount(i)
            Dim vatPercent = Decimal.Round(Math.Max(0D, item.TaxRate), 2, MidpointRounding.AwayFromZero)
            Dim vatValue = Decimal.Round(Math.Max(0D, item.TaxAmount), 2, MidpointRounding.AwayFromZero)
            Dim net = Decimal.Round(Math.Max(0D, afterDisc + vatValue), 2, MidpointRounding.AwayFromZero)

            worksheet.Cell(currentRow, SnoColumn).Value = (i + 1)
            worksheet.Cell(currentRow, PartNoColumn).Value = If(item.ProductId > 0, item.ProductId.ToString(CultureInfo.InvariantCulture), String.Empty)
            worksheet.Cell(currentRow, ProductNameColumn).Value = If(String.IsNullOrWhiteSpace(item.Name), "Item", item.Name)
            worksheet.Cell(currentRow, QtyColumn).Value = Math.Max(0, item.Quantity)
            worksheet.Cell(currentRow, RateColumn).Value = Decimal.Round(Math.Max(0D, item.UnitPrice), 2, MidpointRounding.AwayFromZero)
            worksheet.Cell(currentRow, AmountColumn).Value = gross
            worksheet.Cell(currentRow, ItemDiscountPercentColumn).Value = itemDiscPercent
            worksheet.Cell(currentRow, ItemDiscountValueColumn).Value = itemDisc
            worksheet.Cell(currentRow, AmountAfterDiscountColumn).Value = afterDisc
            worksheet.Cell(currentRow, VatPercentColumn).Value = vatPercent
            worksheet.Cell(currentRow, VatValueColumn).Value = vatValue
            worksheet.Cell(currentRow, NetAmountColumn).Value = net
            currentRow += 1
        Next

        ' Fill remaining template rows (when items < DefaultLineItemRows) with serial numbers to keep formatting consistent.
        For i = lineItems.Count To rowsRequired - 1
            worksheet.Cell(currentRow, SnoColumn).Value = (i + 1)
            currentRow += 1
        Next

        Return extraRows
    End Function

    Private Shared Sub PopulateTotalsAndSummary(worksheet As IXLWorksheet, order As OrderReceiptData, remarks As String, extraRowOffset As Integer)
        Dim totalsRow = TotalsRowIndex + extraRowOffset
        Dim subtotalRow = SubtotalRowIndex + extraRowOffset
        Dim subtotalAfterItemRow = SubtotalAfterItemDiscountRowIndex + extraRowOffset
        Dim subtotalDiscountRow = SubtotalDiscountRowIndex + extraRowOffset
        Dim totalBeforeVatRow = TotalBeforeVatRowIndex + extraRowOffset
        Dim remarksRow = RemarksRowIndex + extraRowOffset
        Dim totalIncVatRow = TotalIncVatRowIndex + extraRowOffset
        Dim totalPaidRow = TotalPaidRowIndex + extraRowOffset
        Dim amountDueRow = AmountDueRowIndex + extraRowOffset

        Dim items = If(order IsNot Nothing AndAlso order.LineItems IsNot Nothing, order.LineItems, New List(Of OrderLineItem)())

        Dim subtotalGross = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.Subtotal, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim itemDiscount = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.ItemDiscountAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim subtotalDiscount = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.SubtotalDiscountAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim totalDiscount = Decimal.Round(Math.Max(0D, itemDiscount + subtotalDiscount), 2, MidpointRounding.AwayFromZero)
        If totalDiscount > subtotalGross Then totalDiscount = subtotalGross

        Dim subtotalAfterItem = Decimal.Round(Math.Max(0D, subtotalGross - itemDiscount), 2, MidpointRounding.AwayFromZero)
        Dim totalBeforeVat = Decimal.Round(Math.Max(0D, subtotalGross - totalDiscount), 2, MidpointRounding.AwayFromZero)
        Dim vatAmount = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.TaxAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim totalIncVat = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.TotalAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim paidAmount = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.PaidAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim dueAmount = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.OutstandingAmount, 0D)), 2, MidpointRounding.AwayFromZero)
        Dim vatPercent = Decimal.Round(Math.Max(0D, If(order IsNot Nothing, order.TaxPercent, 0D)), 2, MidpointRounding.AwayFromZero)

        ' Totals row (row 21 in template).
        Dim sumAmount = subtotalGross
        Dim sumItemDiscount = itemDiscount
        Dim sumVat = vatAmount

        Dim avgItemDiscountPercent = 0D
        If subtotalGross > 0D AndAlso itemDiscount > 0D Then
            avgItemDiscountPercent = Decimal.Round((itemDiscount / subtotalGross) * 100D, 2, MidpointRounding.AwayFromZero)
        End If

        Dim avgVatPercent = vatPercent
        If avgVatPercent <= 0D AndAlso items.Count > 0 Then
            avgVatPercent = Decimal.Round(CDec(items.Select(Function(i) Math.Max(0D, i.TaxRate)).DefaultIfEmpty(0D).Average()), 2, MidpointRounding.AwayFromZero)
        End If

        worksheet.Cell(totalsRow, AmountColumn).Value = sumAmount
        worksheet.Cell(totalsRow, ItemDiscountPercentColumn).Value = avgItemDiscountPercent
        worksheet.Cell(totalsRow, ItemDiscountValueColumn).Value = sumItemDiscount
        worksheet.Cell(totalsRow, AmountAfterDiscountColumn).Value = totalBeforeVat
        worksheet.Cell(totalsRow, VatPercentColumn).Value = avgVatPercent
        worksheet.Cell(totalsRow, VatValueColumn).Value = sumVat
        worksheet.Cell(totalsRow, NetAmountColumn).Value = totalIncVat

        ' Summary block (values in column L).
        worksheet.Cell(subtotalRow, NetAmountColumn).Value = subtotalGross
        worksheet.Cell(subtotalAfterItemRow, NetAmountColumn).Value = subtotalAfterItem
        worksheet.Cell(subtotalDiscountRow, NetAmountColumn).Value = subtotalDiscount
        worksheet.Cell(totalBeforeVatRow, NetAmountColumn).Value = totalBeforeVat
        worksheet.Cell(VatRowIndex + extraRowOffset, NetAmountColumn).Value = vatAmount
        worksheet.Cell(totalIncVatRow, NetAmountColumn).Value = totalIncVat
        worksheet.Cell(totalPaidRow, NetAmountColumn).Value = paidAmount
        worksheet.Cell(amountDueRow, NetAmountColumn).Value = dueAmount

        ' Amount in words (merged C25:H26 in the template).
        Dim words = ToAedWords(totalIncVat)
        Dim amountWordsCell = worksheet.Cell(subtotalAfterItemRow, ProductNameColumn)
        amountWordsCell.Value = words
        amountWordsCell.Style.Alignment.WrapText = True

        ' Remarks.
        Dim remarksValue = remarks
        If String.IsNullOrWhiteSpace(remarksValue) Then
            remarksValue = If(dueAmount > 0D, "Partial payment", "Paid in full")
        End If
        Dim remarksCell = worksheet.Cell(remarksRow, ProductNameColumn)
        remarksCell.Value = remarksValue
        remarksCell.Style.Alignment.WrapText = True
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
            ElseIf line.StartsWith("Attn:", StringComparison.OrdinalIgnoreCase) Then
                ' Keep Attn in address block to avoid losing the contact.
                addressParts.Add(line)
            Else
                addressParts.Add(line)
            End If
        Next

        address = String.Join(Environment.NewLine, addressParts)
    End Sub

    Private Shared Sub EnsureSubtotalDiscountAllocations(items As IList(Of OrderLineItem), subtotalDiscountTotal As Decimal)
        If items Is Nothing OrElse items.Count = 0 Then
            Return
        End If

        Dim existing = Decimal.Round(items.Sum(Function(i) Math.Max(0D, i.SubtotalDiscountAllocationAmount)), 2, MidpointRounding.AwayFromZero)
        If existing > 0D OrElse subtotalDiscountTotal <= 0D Then
            Return
        End If

        Dim bases = items.Select(Function(i) Decimal.Round(Math.Max(0D, i.LineTotal - Math.Max(0D, i.DiscountAmount)), 2, MidpointRounding.AwayFromZero)).ToList()
        Dim baseSum = bases.Sum()
        Dim allocations As New List(Of Decimal)()
        If baseSum <= 0D Then
            Dim even = Decimal.Round(subtotalDiscountTotal / items.Count, 2, MidpointRounding.AwayFromZero)
            For i = 0 To items.Count - 1
                allocations.Add(even)
            Next
        Else
            For i = 0 To items.Count - 1
                allocations.Add(Decimal.Round((bases(i) / baseSum) * subtotalDiscountTotal, 2, MidpointRounding.AwayFromZero))
            Next
        End If

        Dim delta = Decimal.Round(subtotalDiscountTotal - allocations.Sum(), 2, MidpointRounding.AwayFromZero)
        If allocations.Count > 0 AndAlso delta <> 0D Then
            allocations(allocations.Count - 1) = Decimal.Round(Math.Max(0D, allocations(allocations.Count - 1) + delta), 2, MidpointRounding.AwayFromZero)
        End If

        For i = 0 To items.Count - 1
            items(i).SubtotalDiscountAllocationAmount = allocations(i)
        Next
    End Sub

    Private Shared Function ToAedWords(amount As Decimal) As String
        Dim safe = Decimal.Round(Math.Max(0D, amount), 2, MidpointRounding.AwayFromZero)
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
End Class
