Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports ClosedXML.Excel
Imports LocalPOS.LocalPOS.Models

Public Class OrderInvoiceGenerator
    Private Const LineItemsStartRow As Integer = 8
    Private Const DefaultLineItemRows As Integer = 12
    Private Const SubtotalRowIndex As Integer = 23
    Private Const RemarksRowIndex As Integer = 24
    Private Const SubtotalAfterDiscountRowIndex As Integer = 25
    Private Const VatRowIndex As Integer = 26
    Private Const TotalAmountRowIndex As Integer = 27
    Private Const PaidAmountRowIndex As Integer = 28
    Private Const BalanceRowIndex As Integer = 29
    Private Const DescriptionColumn As Integer = 2
    Private Const UnitPriceColumn As Integer = 3
    Private Const VatColumn As Integer = 4
    Private Const TotalColumn As Integer = 7
    Private Const RemarksValueColumn As Integer = 3
    Private Const DateCellAddress As String = "G3"
    Private Const InvoiceNumberCellAddress As String = "G4"
    Private Const BillToCellAddress As String = "B4"
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
            Dim extraRows = PopulateLineItems(worksheet, order.LineItems)
            PopulateSummary(worksheet, order, remarks, extraRows)

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
        Dim dateCell = worksheet.Cell(DateCellAddress)
        dateCell.Value = order.OrderDate
        dateCell.Style.DateFormat.Format = DateFormat

        worksheet.Cell(InvoiceNumberCellAddress).Value = If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber)

        Dim billToValue = billToBlock
        If String.IsNullOrWhiteSpace(billToValue) Then
            billToValue = If(String.IsNullOrWhiteSpace(order.CustomerName), "Walk-in Customer", order.CustomerName)
        End If

        Dim billToCell = worksheet.Cell(BillToCellAddress)
        billToCell.Value = billToValue
        billToCell.Style.Alignment.WrapText = True
    End Sub

    Private Shared Function PopulateLineItems(worksheet As IXLWorksheet, items As IList(Of OrderLineItem)) As Integer
        Dim lineItems = items
        If lineItems Is Nothing Then
            lineItems = New List(Of OrderLineItem)()
        End If

        Dim rowsRequired = Math.Max(lineItems.Count, DefaultLineItemRows)
        Dim extraRows = rowsRequired - DefaultLineItemRows
        If extraRows > 0 Then
            worksheet.Row(SubtotalRowIndex).InsertRowsAbove(extraRows)
        End If

        Dim lastDataRow = LineItemsStartRow + rowsRequired - 1
        For r = LineItemsStartRow To lastDataRow
            worksheet.Cell(r, DescriptionColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, UnitPriceColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, VatColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(r, TotalColumn).Clear(XLClearOptions.Contents)
        Next

        Dim currentRow = LineItemsStartRow
        For Each item In lineItems
            worksheet.Cell(currentRow, DescriptionColumn).Value = If(String.IsNullOrWhiteSpace(item.Name), "Item", item.Name)
            worksheet.Cell(currentRow, UnitPriceColumn).Value = Decimal.Round(item.UnitPrice, 2, MidpointRounding.AwayFromZero)
            worksheet.Cell(currentRow, VatColumn).Clear(XLClearOptions.Contents)
            worksheet.Cell(currentRow, TotalColumn).Value = Decimal.Round(item.LineTotal, 2, MidpointRounding.AwayFromZero)
            currentRow += 1
        Next

        Return extraRows
    End Function

    Private Shared Sub PopulateSummary(worksheet As IXLWorksheet, order As OrderReceiptData, remarks As String, extraRowOffset As Integer)
        Dim subtotalRow = SubtotalRowIndex + extraRowOffset
        Dim remarksRow = RemarksRowIndex + extraRowOffset
        Dim subtotalAfterDiscountRow = SubtotalAfterDiscountRowIndex + extraRowOffset
        Dim vatRow = VatRowIndex + extraRowOffset
        Dim totalRow = TotalAmountRowIndex + extraRowOffset
        Dim paidRow = PaidAmountRowIndex + extraRowOffset
        Dim balanceRow = BalanceRowIndex + extraRowOffset

        Dim subtotalBeforeDiscount = Decimal.Round(Math.Max(0D, order.Subtotal), 2, MidpointRounding.AwayFromZero)
        Dim discountAmount = Decimal.Round(Math.Max(0D, order.DiscountAmount), 2, MidpointRounding.AwayFromZero)
        Dim subtotalAfterDiscount = Decimal.Round(Math.Max(0D, subtotalBeforeDiscount - discountAmount), 2, MidpointRounding.AwayFromZero)
        Dim vatAmount = Decimal.Round(Math.Max(0D, order.TaxAmount), 2, MidpointRounding.AwayFromZero)
        Dim totalAmount = Decimal.Round(Math.Max(0D, order.TotalAmount), 2, MidpointRounding.AwayFromZero)
        Dim paidAmount = Decimal.Round(Math.Max(0D, order.PaidAmount), 2, MidpointRounding.AwayFromZero)
        Dim outstandingAmount = Decimal.Round(Math.Max(0D, order.OutstandingAmount), 2, MidpointRounding.AwayFromZero)

        worksheet.Cell(subtotalRow, TotalColumn).Value = subtotalBeforeDiscount
        worksheet.Cell(remarksRow, TotalColumn).Value = discountAmount
        worksheet.Cell(subtotalAfterDiscountRow, TotalColumn).Value = subtotalAfterDiscount
        worksheet.Cell(vatRow, TotalColumn).Value = vatAmount
        worksheet.Cell(totalRow, TotalColumn).Value = totalAmount
        worksheet.Cell(paidRow, TotalColumn).Value = paidAmount
        worksheet.Cell(balanceRow, TotalColumn).Value = outstandingAmount

        Dim remarksValue = remarks
        If String.IsNullOrWhiteSpace(remarksValue) Then
            remarksValue = If(outstandingAmount > 0D, "Partial payment", "Paid in full")
        End If

        Dim remarksCell = worksheet.Cell(remarksRow, RemarksValueColumn)
        remarksCell.Value = remarksValue
        remarksCell.Style.Alignment.WrapText = True
    End Sub
End Class
