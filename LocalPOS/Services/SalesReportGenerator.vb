Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports LocalPOS.LocalPOS.Models

Public Class SalesReportGenerator
    Private Const DataStartRow As Integer = 4
    Private Const DefaultTemplateRows As Integer = 7
    Private Const FirstDataColumn As Integer = 2
    Private Const LastDataColumn As Integer = 11
    Private Const DateTimeFormat As String = "dd-MMM-yyyy HH:mm"
    Private Const CheckboxMarker As String = "X"

    Public Function Generate(orders As IList(Of SalesHistoryOrder), templatePath As String) As ReportDocument
        If String.IsNullOrWhiteSpace(templatePath) Then
            Throw New ArgumentNullException(NameOf(templatePath))
        End If
        If Not File.Exists(templatePath) Then
            Throw New FileNotFoundException("Sales report template was not found.", templatePath)
        End If

        Dim rows = orders
        If rows Is Nothing Then
            rows = New List(Of SalesHistoryOrder)()
        End If

        Using workbook = New XLWorkbook(templatePath)
            Dim worksheet = workbook.Worksheet(1)
            PopulateWorksheet(worksheet, rows)

            Using stream As New MemoryStream()
                workbook.SaveAs(stream)
                Dim document As New ReportDocument() With {
                    .FileName = $"Sales_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
                    .ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    .Content = stream.ToArray()
                }

                document.EnsureValid()
                Return document
            End Using
        End Using
    End Function

    Private Shared Sub PopulateWorksheet(worksheet As IXLWorksheet, orders As IList(Of SalesHistoryOrder))
        Dim rowsRequired = Math.Max(orders.Count, DefaultTemplateRows)
        Dim totalRowIndex = DataStartRow + DefaultTemplateRows
        Dim extraRows = rowsRequired - DefaultTemplateRows
        If extraRows > 0 Then
            worksheet.Row(totalRowIndex).InsertRowsAbove(extraRows)
            totalRowIndex += extraRows
        End If

        Dim lastDataRow = DataStartRow + rowsRequired - 1
        For r = DataStartRow To lastDataRow
            For c = FirstDataColumn To LastDataColumn
                worksheet.Cell(r, c).Clear(XLClearOptions.Contents)
            Next
        Next

        For c = FirstDataColumn + 1 To LastDataColumn
            worksheet.Cell(totalRowIndex, c).Clear(XLClearOptions.Contents)
        Next

        Dim currentRow = DataStartRow
        Dim serial = 1
        Dim totalSubtotal As Decimal = 0D
        Dim totalDiscount As Decimal = 0D
        Dim totalNet As Decimal = 0D
        Dim totalCredit As Decimal = 0D

        For Each order In orders
            Dim subtotal = DetermineSubtotal(order)
            Dim discount = Decimal.Round(Math.Max(0D, order.DiscountAmount), 2, MidpointRounding.AwayFromZero)
            Dim netAmount = Decimal.Round(Math.Max(0D, order.TotalAmount), 2, MidpointRounding.AwayFromZero)
            Dim outstanding = Math.Max(0D, order.OutstandingAmount)
            Dim creditValue As Decimal = 0D
            If outstanding > 0D Then
                creditValue = Decimal.Round(-outstanding, 2, MidpointRounding.AwayFromZero)
            End If

            worksheet.Cell(currentRow, 2).Value = serial
            worksheet.Cell(currentRow, 3).Value = order.OrderNumber
            worksheet.Cell(currentRow, 4).Value = order.CreatedOn
            worksheet.Cell(currentRow, 4).Style.DateFormat.Format = DateTimeFormat
            worksheet.Cell(currentRow, 5).Value = If(String.IsNullOrWhiteSpace(order.CustomerName), "Walk-in Customer", order.CustomerName)
            worksheet.Cell(currentRow, 6).Value = subtotal
            worksheet.Cell(currentRow, 7).Value = discount
            worksheet.Cell(currentRow, 8).Value = netAmount

            worksheet.Cell(currentRow, 9).Value = If(HasPaymentKeyword(order.PaymentMethod, "cash"), CheckboxMarker, String.Empty)
            worksheet.Cell(currentRow, 10).Value = If(HasPaymentKeyword(order.PaymentMethod, "card"), CheckboxMarker, String.Empty)
            If creditValue <> 0D Then
                worksheet.Cell(currentRow, 11).Value = creditValue
            Else
                worksheet.Cell(currentRow, 11).Clear(XLClearOptions.Contents)
            End If

            totalSubtotal += subtotal
            totalDiscount += discount
            totalNet += netAmount
            totalCredit += creditValue

            serial += 1
            currentRow += 1
        Next

        worksheet.Cell(totalRowIndex, 6).Value = Decimal.Round(totalSubtotal, 2, MidpointRounding.AwayFromZero)
        worksheet.Cell(totalRowIndex, 7).Value = Decimal.Round(totalDiscount, 2, MidpointRounding.AwayFromZero)
        worksheet.Cell(totalRowIndex, 8).Value = Decimal.Round(totalNet, 2, MidpointRounding.AwayFromZero)
        worksheet.Cell(totalRowIndex, 11).Value = Decimal.Round(totalCredit, 2, MidpointRounding.AwayFromZero)
    End Sub

    Private Shared Function DetermineSubtotal(order As SalesHistoryOrder) As Decimal
        If order Is Nothing Then
            Return 0D
        End If

        Dim subtotal = Math.Max(0D, order.Subtotal)
        If subtotal <= 0D Then
            subtotal = Math.Max(0D, order.TotalAmount) + Math.Max(0D, order.DiscountAmount)
        End If

        Return Decimal.Round(subtotal, 2, MidpointRounding.AwayFromZero)
    End Function

    Private Shared Function HasPaymentKeyword(paymentMethod As String, keyword As String) As Boolean
        If String.IsNullOrWhiteSpace(paymentMethod) OrElse String.IsNullOrWhiteSpace(keyword) Then
            Return False
        End If

        Return paymentMethod.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
    End Function
End Class
