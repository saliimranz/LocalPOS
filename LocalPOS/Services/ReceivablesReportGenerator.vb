Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports LocalPOS.Models

Namespace LocalPOS.Services
    Public Class ReceivablesReportGenerator
        Private Const DataStartRow As Integer = 4
        Private Const DefaultTemplateRows As Integer = 7
        Private Const FirstDataColumn As Integer = 2
        Private Const LastDataColumn As Integer = 9

        Public Function Generate(entries As IList(Of ReceivableReportRow), templatePath As String) As ReportDocument
            If String.IsNullOrWhiteSpace(templatePath) Then
                Throw New ArgumentNullException(NameOf(templatePath))
            End If
            If Not File.Exists(templatePath) Then
                Throw New FileNotFoundException("Receivables template was not found.", templatePath)
            End If

            Dim rows = entries
            If rows Is Nothing Then
                rows = New List(Of ReceivableReportRow)()
            End If

            Using workbook = New XLWorkbook(templatePath)
                Dim worksheet = workbook.Worksheet(1)
                PopulateWorksheet(worksheet, rows)

                Using stream As New MemoryStream()
                    workbook.SaveAs(stream)
                    Dim payload As New ReportDocument() With {
                        .FileName = $"Receivables_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
                        .ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                        .Content = stream.ToArray()
                    }
                    payload.EnsureValid()
                    Return payload
                End Using
            End Using
        End Function

        Private Shared Sub PopulateWorksheet(worksheet As IXLWorksheet, rows As IList(Of ReceivableReportRow))
            Dim rowsRequired = Math.Max(rows.Count, DefaultTemplateRows)
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

            Dim currentRow = DataStartRow
            Dim serial = 1
            Dim totalAmount As Decimal = 0D
            Dim paidAmount As Decimal = 0D
            Dim outstandingAmount As Decimal = 0D

            For Each entry In rows
                worksheet.Cell(currentRow, 2).Value = serial
                worksheet.Cell(currentRow, 3).Value = entry.CustomerName
                worksheet.Cell(currentRow, 4).Value = entry.InvoiceNumber
                worksheet.Cell(currentRow, 5).Value = entry.InvoiceDate
                worksheet.Cell(currentRow, 5).Style.DateFormat.Format = "dd-mmm-yyyy"
                worksheet.Cell(currentRow, 6).Value = Decimal.Round(entry.TotalAmount, 2, MidpointRounding.AwayFromZero)
                worksheet.Cell(currentRow, 7).Value = Decimal.Round(entry.PaidAmount, 2, MidpointRounding.AwayFromZero)
                worksheet.Cell(currentRow, 8).Value = Decimal.Round(entry.OutstandingAmount, 2, MidpointRounding.AwayFromZero)

                totalAmount += entry.TotalAmount
                paidAmount += entry.PaidAmount
                outstandingAmount += entry.OutstandingAmount

                serial += 1
                currentRow += 1
            Next

            worksheet.Cell(totalRowIndex, 2).Value = "GRAND TOTAL"
            worksheet.Cell(totalRowIndex, 5).Clear(XLClearOptions.Contents)
            worksheet.Cell(totalRowIndex, 6).Value = Decimal.Round(totalAmount, 2, MidpointRounding.AwayFromZero)
            worksheet.Cell(totalRowIndex, 7).Value = Decimal.Round(paidAmount, 2, MidpointRounding.AwayFromZero)
            worksheet.Cell(totalRowIndex, 8).Value = Decimal.Round(outstandingAmount, 2, MidpointRounding.AwayFromZero)
        End Sub
    End Class
End Namespace
