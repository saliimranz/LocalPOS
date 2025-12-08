Imports System
Imports System.Collections.Generic
Imports System.IO
Imports ClosedXML.Excel
Imports LocalPOS.LocalPOS.Models

Public Class CustomerLedgerReportGenerator
    Private Const DataStartRow As Integer = 7
    Private Const DefaultTemplateRows As Integer = 7
    Private Const FirstDataColumn As Integer = 2
    Private Const LastDataColumn As Integer = 7
    Private Const DateFormat As String = "dd/MM/yyyy"

    Public Function Generate(report As CustomerLedgerReport, templatePath As String) As ReportDocument
        If report Is Nothing Then
            Throw New ArgumentNullException(NameOf(report))
        End If
        If String.IsNullOrWhiteSpace(templatePath) Then
            Throw New ArgumentNullException(NameOf(templatePath))
        End If
        If Not File.Exists(templatePath) Then
            Throw New FileNotFoundException("Customer ledger template was not found.", templatePath)
        End If

        Dim entries = report.Entries
        If entries Is Nothing Then
            entries = New List(Of CustomerLedgerEntry)()
        End If

        Using workbook = New XLWorkbook(templatePath)
            Dim worksheet = workbook.Worksheet(1)
            PopulateHeader(worksheet, report)
            PopulateWorksheet(worksheet, entries, report)

            Using stream As New MemoryStream()
                workbook.SaveAs(stream)
                Dim document As New ReportDocument() With {
                    .FileName = $"CustomerLedger_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx",
                    .ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    .Content = stream.ToArray()
                }

                document.EnsureValid()
                Return document
            End Using
        End Using
    End Function

    Private Shared Sub PopulateHeader(worksheet As IXLWorksheet, report As CustomerLedgerReport)
        worksheet.Cell(2, 3).Value = If(String.IsNullOrWhiteSpace(report.DealerCode), "-", report.DealerCode)

        Dim lastPaymentCell = worksheet.Cell(2, 5)
        lastPaymentCell.Clear(XLClearOptions.Contents)
        If report.LastPaymentDate.HasValue Then
            lastPaymentCell.Value = report.LastPaymentDate.Value
            lastPaymentCell.Style.DateFormat.Format = DateFormat
        End If

        worksheet.Cell(2, 7).Value = Decimal.Round(report.OpeningBalance, 2, MidpointRounding.AwayFromZero)
        worksheet.Cell(3, 3).Value = If(String.IsNullOrWhiteSpace(report.DealerName), "-", report.DealerName)
        worksheet.Cell(3, 5).Value = Decimal.Round(report.PendingAmount, 2, MidpointRounding.AwayFromZero)
    End Sub

    Private Shared Sub PopulateWorksheet(worksheet As IXLWorksheet, entries As IList(Of CustomerLedgerEntry), report As CustomerLedgerReport)
        Dim rowsRequired = Math.Max(entries.Count, DefaultTemplateRows)
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
        Dim totalPayments As Decimal = 0D
        Dim endingBalance As Decimal = Decimal.Round(report.OpeningBalance, 2, MidpointRounding.AwayFromZero)

        For Each entry In entries
            worksheet.Cell(currentRow, 2).Value = serial
            If entry.EntryDate.HasValue Then
                worksheet.Cell(currentRow, 3).Value = entry.EntryDate.Value
                worksheet.Cell(currentRow, 3).Style.DateFormat.Format = DateFormat
            End If
            worksheet.Cell(currentRow, 4).Value = entry.Description

            Dim amount = Decimal.Round(entry.AmountDue, 2, MidpointRounding.AwayFromZero)
            Dim payment = Decimal.Round(entry.PaymentReceived, 2, MidpointRounding.AwayFromZero)
            Dim balance = Decimal.Round(entry.BalanceAfterEntry, 2, MidpointRounding.AwayFromZero)

            If amount <> 0D Then
                worksheet.Cell(currentRow, 5).Value = amount
            End If
            If payment <> 0D Then
                worksheet.Cell(currentRow, 6).Value = payment
            End If
            worksheet.Cell(currentRow, 7).Value = balance

            totalAmount += amount
            totalPayments += payment
            endingBalance = balance

            serial += 1
            currentRow += 1
        Next

        worksheet.Cell(totalRowIndex, 5).Value = Decimal.Round(totalAmount, 2, MidpointRounding.AwayFromZero)
        worksheet.Cell(totalRowIndex, 6).Value = Decimal.Round(totalPayments, 2, MidpointRounding.AwayFromZero)
        worksheet.Cell(totalRowIndex, 7).Value = Decimal.Round(endingBalance, 2, MidpointRounding.AwayFromZero)
    End Sub
End Class
