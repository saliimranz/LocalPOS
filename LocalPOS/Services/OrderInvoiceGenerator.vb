Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Diagnostics
Imports System.IO
Imports ClosedXML.Excel
Imports DocumentFormat.OpenXml.Packaging
Imports iTextSharp.text
Imports iTextSharp.text.pdf
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

        Dim extension = Path.GetExtension(templatePath)
        If extension IsNot Nothing AndAlso extension.Equals(".docx", StringComparison.OrdinalIgnoreCase) Then
            Return GenerateFromWordTemplate(order, templatePath, billToBlock, remarks)
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

    Private Function GenerateFromWordTemplate(order As OrderReceiptData, templatePath As String, billToBlock As String, remarks As String) As ReportDocument
        Dim tempRoot = Path.Combine(Path.GetTempPath(), "LocalPOSInvoices")
        Directory.CreateDirectory(tempRoot)

        Dim stamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture)
        Dim safeOrder = If(String.IsNullOrWhiteSpace(order.OrderNumber), "order", order.OrderNumber)
        For Each c In Path.GetInvalidFileNameChars()
            safeOrder = safeOrder.Replace(c, "_"c)
        Next

        Dim workingDocx = Path.Combine(tempRoot, $"invoice_{safeOrder}_{stamp}.docx")
        Dim workingPdf = Path.Combine(tempRoot, $"invoice_{safeOrder}_{stamp}.pdf")

        Try
            File.Copy(templatePath, workingDocx, True)

            Dim replacements = BuildWordReplacements(order, billToBlock, remarks)
            TryPopulateWordTemplateInPlace(workingDocx, replacements)

            Dim converted = TryConvertDocxToPdf(workingDocx, tempRoot, workingPdf)
            If Not converted OrElse Not File.Exists(workingPdf) Then
                Throw New InvalidOperationException("Unable to convert invoice template to PDF. Ensure LibreOffice/soffice is installed on the server and accessible (PATH or standard install location).")
            End If

            Return New ReportDocument() With {
                .FileName = $"Invoice_{order.OrderNumber}_{stamp}.pdf",
                .ContentType = "application/pdf",
                .Content = File.ReadAllBytes(workingPdf)
            }
        Finally
            Try
                If File.Exists(workingDocx) Then File.Delete(workingDocx)
            Catch
            End Try
            Try
                If File.Exists(workingPdf) Then File.Delete(workingPdf)
            Catch
            End Try
        End Try
    End Function

    Private Shared Function BuildWordReplacements(order As OrderReceiptData, billToBlock As String, remarks As String) As IDictionary(Of String, String)
        Dim map As New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)

        Dim orderNumber = If(String.IsNullOrWhiteSpace(order.OrderNumber), String.Empty, order.OrderNumber)
        Dim customerName = If(String.IsNullOrWhiteSpace(order.CustomerName), String.Empty, order.CustomerName)
        Dim billTo = If(String.IsNullOrWhiteSpace(billToBlock), customerName, billToBlock)
        Dim invoiceDate = order.OrderDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)

        map("InvoiceNumber") = orderNumber
        map("InvoiceNo") = orderNumber
        map("Invoice_No") = orderNumber
        map("INVOICE_NO") = orderNumber
        map("OrderNo") = orderNumber
        map("Order_No") = orderNumber
        map("Date") = invoiceDate
        map("InvoiceDate") = invoiceDate
        map("CustomerName") = customerName
        map("Customer") = customerName
        map("BillTo") = billTo
        map("Bill_To") = billTo
        map("PaymentMethod") = If(String.IsNullOrWhiteSpace(order.PaymentMethod), String.Empty, order.PaymentMethod)
        map("Cashier") = If(String.IsNullOrWhiteSpace(order.CashierName), String.Empty, order.CashierName)

        map("Subtotal") = order.Subtotal.ToString("F2", CultureInfo.InvariantCulture)
        map("DiscountAmount") = order.DiscountAmount.ToString("F2", CultureInfo.InvariantCulture)
        map("DiscountPercent") = order.DiscountPercent.ToString("F2", CultureInfo.InvariantCulture)
        map("VatPercent") = order.TaxPercent.ToString("F2", CultureInfo.InvariantCulture)
        map("VatAmount") = order.TaxAmount.ToString("F2", CultureInfo.InvariantCulture)
        map("TotalAmount") = order.TotalAmount.ToString("F2", CultureInfo.InvariantCulture)
        map("PaidAmount") = order.PaidAmount.ToString("F2", CultureInfo.InvariantCulture)
        map("Balance") = order.OutstandingAmount.ToString("F2", CultureInfo.InvariantCulture)
        map("OutstandingAmount") = order.OutstandingAmount.ToString("F2", CultureInfo.InvariantCulture)

        map("Remarks") = If(String.IsNullOrWhiteSpace(remarks), String.Empty, remarks)

        Return map
    End Function

    Private Shared Sub TryPopulateWordTemplateInPlace(docxPath As String, replacements As IDictionary(Of String, String))
        If String.IsNullOrWhiteSpace(docxPath) OrElse replacements Is Nothing OrElse replacements.Count = 0 Then
            Return
        End If

        Try
            Using doc = WordprocessingDocument.Open(docxPath, True)
                If doc.MainDocumentPart Is Nothing OrElse doc.MainDocumentPart.Document Is Nothing Then
                    Return
                End If

                Dim texts = doc.MainDocumentPart.Document.Descendants(Of DocumentFormat.OpenXml.Wordprocessing.Text)()
                For Each t In texts
                    If t Is Nothing OrElse String.IsNullOrEmpty(t.Text) Then
                        Continue For
                    End If

                    Dim value = t.Text
                    For Each kvp In replacements
                        Dim key = kvp.Key
                        Dim repl = If(kvp.Value, String.Empty)

                        value = value.Replace($"{{{{{key}}}}}", repl)
                        value = value.Replace($"<<{key}>>", repl)
                        value = value.Replace($"[[{key}]]", repl)
                        value = value.Replace($"${{{key}}}", repl)
                    Next

                    t.Text = value
                Next

                doc.MainDocumentPart.Document.Save()
            End Using
        Catch
            ' Template population is best-effort. Never crash invoice print.
        End Try
    End Sub

    Private Shared Function TryConvertDocxToPdf(docxPath As String, outDir As String, expectedPdfPath As String) As Boolean
        If String.IsNullOrWhiteSpace(docxPath) OrElse Not File.Exists(docxPath) Then
            Return False
        End If
        If String.IsNullOrWhiteSpace(outDir) Then
            outDir = Path.GetDirectoryName(docxPath)
        End If
        If String.IsNullOrWhiteSpace(outDir) OrElse Not Directory.Exists(outDir) Then
            Return False
        End If

        Dim candidates As New List(Of String)()

        ' 1) Allow explicit override via environment variable (safe, optional).
        Dim envOverride = Environment.GetEnvironmentVariable("LIBREOFFICE_PATH")
        If Not String.IsNullOrWhiteSpace(envOverride) Then
            candidates.Add(envOverride)
        End If

        ' 2) Common PATH-resolvable names.
        candidates.AddRange(New String() {"soffice", "libreoffice", "soffice.exe", "libreoffice.exe"})

        ' 3) Common absolute locations (handles servers where LibreOffice isn't on PATH).
        Try
            Dim isWindows = Environment.OSVersion.Platform = PlatformID.Win32NT
            If isWindows Then
                Dim pf = Environment.GetEnvironmentVariable("ProgramFiles")
                Dim pfx86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)")
                If Not String.IsNullOrWhiteSpace(pf) Then
                    candidates.Add(Path.Combine(pf, "LibreOffice", "program", "soffice.exe"))
                End If
                If Not String.IsNullOrWhiteSpace(pfx86) Then
                    candidates.Add(Path.Combine(pfx86, "LibreOffice", "program", "soffice.exe"))
                End If
            Else
                candidates.AddRange(New String() {
                    "/usr/bin/soffice",
                    "/usr/bin/libreoffice",
                    "/snap/bin/libreoffice",
                    "/opt/libreoffice/program/soffice"
                })
            End If
        Catch
        End Try

        For Each exe In candidates
            Try
                If exe.IndexOf(Path.DirectorySeparatorChar) >= 0 OrElse exe.IndexOf(Path.AltDirectorySeparatorChar) >= 0 Then
                    If Not File.Exists(exe) Then
                        Continue For
                    End If
                End If

                Dim psi As New ProcessStartInfo() With {
                    .FileName = exe,
                    .Arguments = $"--headless --nologo --nolockcheck --nodefault --nofirststartwizard --convert-to pdf --outdir ""{outDir}"" ""{docxPath}""",
                    .CreateNoWindow = True,
                    .UseShellExecute = False,
                    .RedirectStandardOutput = True,
                    .RedirectStandardError = True
                }

                Using p = Process.Start(psi)
                    If p Is Nothing Then
                        Continue For
                    End If
                    p.WaitForExit(120000)
                End Using

                If File.Exists(expectedPdfPath) Then
                    Return True
                End If

                ' LibreOffice sometimes uses the input filename for output; fallback to that.
                Dim guessed = Path.Combine(outDir, Path.GetFileNameWithoutExtension(docxPath) & ".pdf")
                If File.Exists(guessed) Then
                    Try
                        File.Copy(guessed, expectedPdfPath, True)
                    Catch
                    End Try
                    Return File.Exists(expectedPdfPath)
                End If
            Catch
                ' Try next candidate.
            End Try
        Next

        Return False
    End Function

    Private Shared Function GenerateFallbackPdf(order As OrderReceiptData, billToBlock As String, remarks As String) As Byte()
        Dim defaultMargin As Single = 36.0F
        Using stream As New MemoryStream()
            Using doc As New iTextSharp.text.Document(iTextSharp.text.PageSize.A4, defaultMargin, defaultMargin, defaultMargin, defaultMargin)
                PdfWriter.GetInstance(doc, stream)
                doc.Open()

                Dim titleFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 16.0F, BaseColor.BLACK)
                doc.Add(New iTextSharp.text.Paragraph("Invoice", titleFont))
                doc.Add(New iTextSharp.text.Paragraph($"Order: {If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber)}"))
                doc.Add(New iTextSharp.text.Paragraph($"Date: {order.OrderDate.ToString("f", CultureInfo.CurrentCulture)}"))
                doc.Add(New iTextSharp.text.Paragraph(" "))

                Dim customer = If(String.IsNullOrWhiteSpace(billToBlock), If(String.IsNullOrWhiteSpace(order.CustomerName), "-", order.CustomerName), billToBlock)
                doc.Add(New iTextSharp.text.Paragraph("Bill To:", FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 11.0F, BaseColor.BLACK)))
                doc.Add(New iTextSharp.text.Paragraph(customer))
                doc.Add(New iTextSharp.text.Paragraph(" "))

                Dim items = If(order.LineItems, New List(Of OrderLineItem)())
                Dim table = New PdfPTable(4)
                table.WidthPercentage = 100
                table.SetWidths(New Single() {50.0F, 15.0F, 15.0F, 20.0F})

                AddFallbackHeaderCell(table, "Item")
                AddFallbackHeaderCell(table, "Qty")
                AddFallbackHeaderCell(table, "Unit")
                AddFallbackHeaderCell(table, "Total")

                For Each it In items
                    AddFallbackBodyCell(table, If(String.IsNullOrWhiteSpace(it.Name), "Item", it.Name))
                    AddFallbackBodyCell(table, it.Quantity.ToString(CultureInfo.InvariantCulture))
                    AddFallbackBodyCell(table, Decimal.Round(it.UnitPrice, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture))
                    AddFallbackBodyCell(table, Decimal.Round(it.LineTotal, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture))
                Next

                doc.Add(table)
                doc.Add(New iTextSharp.text.Paragraph(" "))

                Dim totals = New PdfPTable(2)
                totals.WidthPercentage = 40
                totals.HorizontalAlignment = Element.ALIGN_RIGHT
                totals.SetWidths(New Single() {60.0F, 40.0F})
                AddFallbackTotalRow(totals, "Subtotal", order.Subtotal)
                AddFallbackTotalRow(totals, $"Discount ({order.DiscountPercent.ToString("F2", CultureInfo.InvariantCulture)}%)", order.DiscountAmount)
                AddFallbackTotalRow(totals, $"VAT ({order.TaxPercent.ToString("F2", CultureInfo.InvariantCulture)}%)", order.TaxAmount)
                AddFallbackTotalRow(totals, "Total", order.TotalAmount)
                AddFallbackTotalRow(totals, "Paid", order.PaidAmount)
                AddFallbackTotalRow(totals, "Amount Due", order.OutstandingAmount)
                doc.Add(totals)

                If Not String.IsNullOrWhiteSpace(remarks) Then
                    doc.Add(New iTextSharp.text.Paragraph(" "))
                    doc.Add(New iTextSharp.text.Paragraph($"Remarks: {remarks}"))
                End If
            End Using
            Return stream.ToArray()
        End Using
    End Function

    Private Shared Sub AddFallbackHeaderCell(table As PdfPTable, text As String)
        Dim font = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 10.0F, BaseColor.WHITE)
        Dim cell = New PdfPCell(New Phrase(text, font)) With {
            .BackgroundColor = New BaseColor(33, 37, 41),
            .Padding = 6.0F
        }
        table.AddCell(cell)
    End Sub

    Private Shared Sub AddFallbackBodyCell(table As PdfPTable, text As String)
        Dim font = FontFactory.GetFont(FontFactory.HELVETICA, 9.0F, BaseColor.BLACK)
        Dim cell = New PdfPCell(New Phrase(If(text, String.Empty), font)) With {
            .Padding = 6.0F,
            .BorderWidthBottom = 0.5F
        }
        table.AddCell(cell)
    End Sub

    Private Shared Sub AddFallbackTotalRow(table As PdfPTable, label As String, amount As Decimal)
        Dim labelFont = FontFactory.GetFont(FontFactory.HELVETICA, 9.0F, BaseColor.BLACK)
        Dim valueFont = FontFactory.GetFont(FontFactory.HELVETICA_BOLD, 9.0F, BaseColor.BLACK)
        table.AddCell(New PdfPCell(New Phrase(label, labelFont)) With {.Border = Rectangle.NO_BORDER, .Padding = 3.0F})
        table.AddCell(New PdfPCell(New Phrase(Decimal.Round(Math.Max(0D, amount), 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture), valueFont)) With {.Border = Rectangle.NO_BORDER, .Padding = 3.0F, .HorizontalAlignment = Element.ALIGN_RIGHT})
    End Sub

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
