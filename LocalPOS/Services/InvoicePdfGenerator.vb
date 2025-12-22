Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports iTextSharp.text
Imports iTextSharp.text.pdf
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Services
    ''' <summary>
    ''' Generates an invoice by stamping order data onto a PDF template.
    ''' This keeps business logic intact and only maps already-available values.
    ''' </summary>
    Public Class InvoicePdfGenerator
        Private Const PageWidth As Single = 612.0F
        Private Const PageHeight As Single = 792.0F

        ' Main line-items table grid (extracted from the template geometry).
        Private Const TableLeft As Single = 33.5F
        Private Const TableRight As Single = 577.5F
        Private Const TableHeaderTopFromTop As Single = 292.5F
        Private Const TableHeaderBottomFromTop As Single = 339.5F
        Private Const TableRowHeight As Single = 24.0F
        Private Const MaxItemRows As Integer = 6 ' Template has 6 item rows + 1 totals row.

        ' Totals/summary box sits under the table, aligned to the right portion.
        Private Const TotalsLeft As Single = 381.5F
        Private Const TotalsMid As Single = 511.5F
        Private Const TotalsRight As Single = 577.5F

        ' Column boundaries for the main table.
        Private ReadOnly _col As Dictionary(Of String, Single) = New Dictionary(Of String, Single)(StringComparer.OrdinalIgnoreCase) From {
            {"SnoCenter", (33.5F + 69.5F) / 2.0F},
            {"PartNoCenter", (69.5F + 109.5F) / 2.0F},
            {"ProductLeft", 112.0F},
            {"QtyCenter", (185.5F + 218.5F) / 2.0F},
            {"RateCenter", (218.5F + 262.5F) / 2.0F},
            {"AmountCenter", (262.5F + 321.5F) / 2.0F},
            {"DiscPctCenter", (321.5F + 348.5F) / 2.0F},
            {"DiscValCenter", (348.5F + 381.5F) / 2.0F},
            {"AfterDiscCenter", (381.5F + 442.5F) / 2.0F},
            {"VatPctCenter", (442.5F + 472.5F) / 2.0F},
            {"VatValCenter", (472.5F + 511.5F) / 2.0F},
            {"NetCenter", (511.5F + 577.5F) / 2.0F}
        }

        Public Function Generate(order As OrderReceiptData, templatePath As String, dealer As Dealer, remarks As String) As ReportDocument
            If order Is Nothing Then Throw New ArgumentNullException(NameOf(order))
            If String.IsNullOrWhiteSpace(templatePath) Then Throw New ArgumentNullException(NameOf(templatePath))
            If Not File.Exists(templatePath) Then Throw New FileNotFoundException("Invoice template was not found.", templatePath)

            Dim safeOrderNumber = If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber)
            Dim safeDate = order.OrderDate

            Dim customerName = If(String.IsNullOrWhiteSpace(order.CustomerName), "Walk-in Customer", order.CustomerName)
            If dealer IsNot Nothing AndAlso Not String.IsNullOrWhiteSpace(dealer.DealerName) Then
                customerName = dealer.DealerName
            End If

            Dim address = If(dealer IsNot Nothing, dealer.Address, Nothing)
            If String.IsNullOrWhiteSpace(address) AndAlso dealer IsNot Nothing Then
                Dim parts As New List(Of String)()
                If Not String.IsNullOrWhiteSpace(dealer.City) Then parts.Add(dealer.City)
                If Not String.IsNullOrWhiteSpace(dealer.Country) Then parts.Add(dealer.Country)
                If parts.Count > 0 Then address = String.Join(", ", parts)
            End If

            Dim customerId = If(dealer IsNot Nothing, dealer.DealerCode, Nothing)
            Dim tel = If(dealer IsNot Nothing, dealer.CellNumber, Nothing)

            Dim subtotal = RoundMoney(Math.Max(0D, order.Subtotal))
            Dim itemDiscount = RoundMoney(Math.Max(0D, order.ItemDiscountAmount))
            Dim subtotalDiscount = RoundMoney(Math.Max(0D, order.SubtotalDiscountAmount))
            Dim totalDiscount = RoundMoney(Math.Max(0D, order.DiscountAmount))
            If totalDiscount > subtotal Then totalDiscount = subtotal

            Dim taxableBase = RoundMoney(Math.Max(0D, subtotal - totalDiscount))
            Dim vatPercent = Decimal.Round(Math.Max(0D, order.TaxPercent), 2, MidpointRounding.AwayFromZero)
            Dim vatAmount = RoundMoney(Math.Max(0D, order.TaxAmount))
            Dim totalAmount = RoundMoney(Math.Max(0D, order.TotalAmount))
            Dim paidAmount = RoundMoney(Math.Max(0D, order.PaidAmount))
            Dim dueAmount = RoundMoney(Math.Max(0D, order.OutstandingAmount))

            Dim bytes As Byte()
            Using reader = New PdfReader(templatePath)
                Using output = New MemoryStream()
                    Using stamper = New PdfStamper(reader, output)
                        Dim cb = stamper.GetOverContent(1)
                        Dim baseFont = BaseFont.CreateFont(BaseFont.HELVETICA, BaseFont.CP1252, BaseFont.NOT_EMBEDDED)

                        ' Header fields (cover any existing sample content and write new values).
                        ClearRect(cb, 295.0F, 628.0F, 160.0F, 18.0F) ' invoice number sample area
                        WriteText(cb, baseFont, safeOrderNumber, 300.0F, 632.0F, 12.0F, Element.ALIGN_LEFT)

                        ClearRect(cb, 100.0F, 578.0F, 250.0F, 16.0F) ' customer value area
                        WriteText(cb, baseFont, customerName, 100.0F, 582.0F, 10.0F, Element.ALIGN_LEFT)

                        ClearRect(cb, 100.0F, 559.0F, 250.0F, 16.0F) ' address
                        WriteText(cb, baseFont, SafeSingleLine(address), 100.0F, 563.0F, 9.5F, Element.ALIGN_LEFT)

                        ClearRect(cb, 110.0F, 540.0F, 240.0F, 16.0F) ' customer id
                        WriteText(cb, baseFont, SafeSingleLine(customerId), 110.0F, 544.0F, 9.5F, Element.ALIGN_LEFT)

                        ClearRect(cb, 90.0F, 521.0F, 260.0F, 16.0F) ' tel
                        WriteText(cb, baseFont, SafeSingleLine(tel), 90.0F, 525.0F, 9.5F, Element.ALIGN_LEFT)

                        ClearRect(cb, 500.0F, 578.0F, 80.0F, 16.0F) ' order no (right header)
                        WriteText(cb, baseFont, safeOrderNumber, 500.0F, 582.0F, 10.0F, Element.ALIGN_LEFT)

                        ClearRect(cb, 500.0F, 559.0F, 90.0F, 16.0F) ' date (right header)
                        WriteText(cb, baseFont, safeDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture), 500.0F, 563.0F, 9.5F, Element.ALIGN_LEFT)

                        ' Line items
                        Dim items = If(order.LineItems, New List(Of OrderLineItem)())
                        If items.Count > MaxItemRows Then
                            Trace.TraceError($"[invoice-pdf] Order {safeOrderNumber} has {items.Count} items; template supports {MaxItemRows}. Extra items will be omitted.")
                        End If

                        For i = 0 To Math.Min(items.Count, MaxItemRows) - 1
                            Dim item = items(i)
                            Dim rowIndex = i + 1 ' 1..MaxItemRows
                            Dim y = GetItemRowBaselineY(rowIndex)

                            Dim qty = Math.Max(0, item.Quantity)
                            Dim rate = RoundMoney(Math.Max(0D, item.UnitPrice))
                            Dim gross = RoundMoney(Math.Max(0D, item.LineTotal))
                            Dim lineItemDiscount = RoundMoney(Math.Max(0D, item.DiscountAmount))
                            If lineItemDiscount > gross Then lineItemDiscount = gross
                            Dim afterDisc = RoundMoney(Math.Max(0D, gross - lineItemDiscount))
                            Dim lineVatPct = Decimal.Round(Math.Max(0D, item.TaxRate), 2, MidpointRounding.AwayFromZero)
                            Dim lineVat = RoundMoney(Math.Max(0D, item.TaxAmount))
                            Dim net = RoundMoney(Math.Max(0D, afterDisc + lineVat))

                            Dim discPct = 0D
                            If gross > 0D AndAlso lineItemDiscount > 0D Then
                                discPct = Decimal.Round((lineItemDiscount / gross) * 100D, 2, MidpointRounding.AwayFromZero)
                            End If

                            WriteText(cb, baseFont, (i + 1).ToString(CultureInfo.InvariantCulture), _col("SnoCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            ' PART NO not available in current model; leave blank.
                            WriteText(cb, baseFont, FitText(baseFont, If(String.IsNullOrWhiteSpace(item.Name), "Item", item.Name), 9.0F, 70.0F), _col("ProductLeft"), y, 9.0F, Element.ALIGN_LEFT)
                            WriteText(cb, baseFont, qty.ToString(CultureInfo.InvariantCulture), _col("QtyCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, FormatNumber(rate), _col("RateCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, FormatNumber(gross), _col("AmountCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, If(discPct > 0D, discPct.ToString("F2", CultureInfo.InvariantCulture), String.Empty), _col("DiscPctCenter"), y, 8.5F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, If(lineItemDiscount > 0D, FormatNumber(lineItemDiscount), String.Empty), _col("DiscValCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, FormatNumber(afterDisc), _col("AfterDiscCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, If(lineVatPct > 0D, lineVatPct.ToString("F2", CultureInfo.InvariantCulture), String.Empty), _col("VatPctCenter"), y, 8.5F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, If(lineVat > 0D, FormatNumber(lineVat), String.Empty), _col("VatValCenter"), y, 9.0F, Element.ALIGN_CENTER)
                            WriteText(cb, baseFont, FormatNumber(net), _col("NetCenter"), y, 9.0F, Element.ALIGN_CENTER)
                        Next

                        ' Totals row inside the main table (last row).
                        Dim totalsY = GetTotalsRowBaselineY()
                        ' Clear the totals numeric cells to avoid template sample numbers (avoid wiping grid lines).
                        ClearCell(cb, 262.5F, 321.5F, 483.5F, 507.5F) ' Amount
                        ClearCell(cb, 348.5F, 381.5F, 483.5F, 507.5F) ' Discount Val
                        ClearCell(cb, 381.5F, 442.5F, 483.5F, 507.5F) ' Amount After Discount
                        ClearCell(cb, 472.5F, 511.5F, 483.5F, 507.5F) ' VAT Val
                        ClearCell(cb, 511.5F, 577.5F, 483.5F, 507.5F) ' NET AMT
                        Dim totalAfterDiscount = RoundMoney(Math.Max(0D, subtotal - totalDiscount))
                        Dim totalNet = RoundMoney(Math.Max(0D, totalAfterDiscount + vatAmount))
                        WriteText(cb, baseFont, FormatNumber(subtotal), _col("AmountCenter"), totalsY, 9.5F, Element.ALIGN_CENTER)
                        WriteText(cb, baseFont, FormatNumber(totalDiscount), _col("DiscValCenter"), totalsY, 9.5F, Element.ALIGN_CENTER)
                        WriteText(cb, baseFont, FormatNumber(totalAfterDiscount), _col("AfterDiscCenter"), totalsY, 9.5F, Element.ALIGN_CENTER)
                        WriteText(cb, baseFont, FormatNumber(vatAmount), _col("VatValCenter"), totalsY, 9.5F, Element.ALIGN_CENTER)
                        WriteText(cb, baseFont, FormatNumber(totalNet), _col("NetCenter"), totalsY, 9.5F, Element.ALIGN_CENTER)

                        ' Dhs: amount-in-words (overwrite any sample text).
                        Dim words = AmountToDhsWords(totalAmount)
                        ClearRect(cb, 65.0F, 250.0F, 500.0F, 18.0F)
                        WriteText(cb, baseFont, FitText(baseFont, words, 10.0F, 470.0F), 65.0F, 254.0F, 10.0F, Element.ALIGN_LEFT)

                        ' Remarks
                        ClearRect(cb, 95.0F, 214.0F, 320.0F, 26.0F)
                        WriteText(cb, baseFont, FitText(baseFont, SafeSingleLine(remarks), 10.0F, 300.0F), 95.0F, 218.0F, 10.0F, Element.ALIGN_LEFT)

                        ' Totals box: clear cell interiors (avoid wiping grid lines), then write values.
                        ' Row 1: 507.5..531.5
                        ClearCell(cb, TotalsLeft, TotalsMid, 507.5F, 531.5F) ' label cell
                        ClearCell(cb, TotalsMid, TotalsRight, 507.5F, 531.5F) ' value cell
                        ' Row 2: 531.5..555.5
                        ClearCell(cb, TotalsLeft, TotalsMid, 531.5F, 555.5F)
                        ClearCell(cb, TotalsMid, TotalsRight, 531.5F, 555.5F)
                        ' Row 3: 555.5..578.5
                        ClearCell(cb, TotalsLeft, TotalsMid, 555.5F, 578.5F)
                        ClearCell(cb, TotalsMid, TotalsRight, 555.5F, 578.5F)
                        ' Row 4: 578.5..602.5
                        ClearCell(cb, TotalsLeft, TotalsMid, 578.5F, 602.5F)
                        ClearCell(cb, TotalsMid, TotalsRight, 578.5F, 602.5F)
                        ' Row 5: 602.5..627.0
                        ClearCell(cb, TotalsLeft, TotalsMid, 602.5F, 627.0F)
                        ClearCell(cb, TotalsMid, TotalsRight, 602.5F, 627.0F)

                        ' Subtotal Discount: render before Total Before VAT (same row, slightly above).
                        Dim row1BottomY = FromTopToY(531.5F) ' bottom border of first totals row
                        Dim totalBeforeVatY = row1BottomY + 6.0F
                        Dim subtotalDiscountY = totalBeforeVatY + 10.0F
                        WriteText(cb, baseFont, "Subtotal Discount", TotalsLeft + 6.0F, subtotalDiscountY, 8.5F, Element.ALIGN_LEFT)
                        WriteText(cb, baseFont, If(subtotalDiscount > 0D, FormatNumber(subtotalDiscount), FormatNumber(0D)), TotalsRight - 8.0F, subtotalDiscountY, 8.5F, Element.ALIGN_RIGHT)

                        WriteText(cb, baseFont, "Total Before VAT", TotalsLeft + 6.0F, totalBeforeVatY, 9.5F, Element.ALIGN_LEFT)
                        WriteText(cb, baseFont, FormatNumber(taxableBase), TotalsRight - 8.0F, totalBeforeVatY, 9.5F, Element.ALIGN_RIGHT)

                        Dim vatRowY = FromTopToY(555.5F) + 6.0F
                        WriteText(cb, baseFont, $"VAT (%{vatPercent.ToString("F2", CultureInfo.InvariantCulture)})", TotalsLeft + 6.0F, vatRowY, 9.5F, Element.ALIGN_LEFT)
                        WriteText(cb, baseFont, FormatNumber(vatAmount), TotalsRight - 8.0F, vatRowY, 9.5F, Element.ALIGN_RIGHT)

                        Dim totalIncVatY = FromTopToY(578.5F) + 6.0F
                        WriteText(cb, baseFont, "Total Inc VAT", TotalsLeft + 6.0F, totalIncVatY, 10.0F, Element.ALIGN_LEFT)
                        WriteText(cb, baseFont, FormatNumber(totalAmount), TotalsRight - 8.0F, totalIncVatY, 10.0F, Element.ALIGN_RIGHT)

                        Dim totalPaidY = FromTopToY(602.5F) + 6.0F
                        WriteText(cb, baseFont, "Total Paid", TotalsLeft + 6.0F, totalPaidY, 10.0F, Element.ALIGN_LEFT)
                        WriteText(cb, baseFont, FormatNumber(paidAmount), TotalsRight - 8.0F, totalPaidY, 10.0F, Element.ALIGN_RIGHT)

                        Dim amountDueY = FromTopToY(627.0F) + 6.0F
                        WriteText(cb, baseFont, "Amount Due", TotalsLeft + 6.0F, amountDueY, 10.0F, Element.ALIGN_LEFT)
                        WriteText(cb, baseFont, FormatNumber(dueAmount), TotalsRight - 8.0F, amountDueY, 10.0F, Element.ALIGN_RIGHT)
                    End Using
                    bytes = output.ToArray()
                End Using
            End Using

            Dim doc As New ReportDocument() With {
                .FileName = $"Invoice_{safeOrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf",
                .ContentType = "application/pdf",
                .Content = bytes
            }
            doc.EnsureValid()
            Return doc
        End Function

        Private Shared Function RoundMoney(value As Decimal) As Decimal
            Return Decimal.Round(value, 2, MidpointRounding.AwayFromZero)
        End Function

        Private Shared Function FormatNumber(value As Decimal) As String
            Return Decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("F2", CultureInfo.InvariantCulture)
        End Function

        Private Shared Function SafeSingleLine(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If
            Return value.Replace(vbCrLf, " ").Replace(vbCr, " ").Replace(vbLf, " ").Trim()
        End Function

        Private Shared Sub WriteText(cb As PdfContentByte, font As BaseFont, text As String, x As Single, y As Single, fontSize As Single, align As Integer)
            If cb Is Nothing OrElse font Is Nothing Then Return
            If String.IsNullOrWhiteSpace(text) Then Return
            Dim phrase = New Phrase(text, New Font(font, fontSize, Font.NORMAL, BaseColor.BLACK))
            ColumnText.ShowTextAligned(cb, align, phrase, x, y, 0)
        End Sub

        Private Shared Sub ClearRect(cb As PdfContentByte, x As Single, y As Single, width As Single, height As Single)
            If cb Is Nothing Then Return
            cb.SaveState()
            cb.SetColorFill(BaseColor.WHITE)
            cb.Rectangle(x, y, width, height)
            cb.Fill()
            cb.RestoreState()
        End Sub

        Private Shared Sub ClearCell(cb As PdfContentByte, x0 As Single, x1 As Single, topFromTop As Single, bottomFromTop As Single)
            ' Clears within a bordered cell, leaving a small inset to preserve grid lines.
            Dim inset As Single = 1.2F
            Dim x = x0 + inset
            Dim y = FromTopToY(bottomFromTop) + inset
            Dim w = Math.Max(0.0F, (x1 - x0) - (2 * inset))
            Dim h = Math.Max(0.0F, (bottomFromTop - topFromTop) - (2 * inset))
            If w <= 0.5F OrElse h <= 0.5F Then Return
            ClearRect(cb, x, y, w, h)
        End Sub

        Private Shared Function GetItemRowBaselineY(rowIndex As Integer) As Single
            ' RowIndex: 1..MaxItemRows. Rows start immediately after table header.
            Dim rowBottomFromTop = TableHeaderBottomFromTop + (rowIndex * TableRowHeight)
            Return FromTopToY(rowBottomFromTop) + 7.0F
        End Function

        Private Shared Function GetTotalsRowBaselineY() As Single
            ' Totals row is the last row in the table (row 7): bottom border at 507.5 from top.
            Dim totalsRowBottomFromTop = TableHeaderBottomFromTop + (7 * TableRowHeight)
            Return FromTopToY(totalsRowBottomFromTop) + 7.0F
        End Function

        Private Shared Function FromTopToY(fromTop As Single) As Single
            Return PageHeight - fromTop
        End Function

        Private Shared Function FitText(font As BaseFont, text As String, fontSize As Single, maxWidth As Single) As String
            If font Is Nothing OrElse String.IsNullOrEmpty(text) Then Return If(text, String.Empty)
            Dim cleaned = text.Trim()
            If cleaned.Length = 0 Then Return String.Empty

            Dim width = font.GetWidthPoint(cleaned, fontSize)
            If width <= maxWidth Then Return cleaned

            Const ellipsis As String = "…"
            Dim lo = 0
            Dim hi = cleaned.Length
            While lo < hi
                Dim mid = (lo + hi) \ 2
                Dim candidate = cleaned.Substring(0, Math.Max(0, mid)).TrimEnd() & ellipsis
                If font.GetWidthPoint(candidate, fontSize) <= maxWidth Then
                    lo = mid + 1
                Else
                    hi = mid
                End If
            End While

            Dim take = Math.Max(0, lo - 1)
            If take <= 0 Then Return ellipsis
            Return cleaned.Substring(0, take).TrimEnd() & ellipsis
        End Function

        ' Minimal AED words converter (kept local to invoice rendering).
        Private Shared Function AmountToDhsWords(amount As Decimal) As String
            Dim safe = Decimal.Round(Math.Max(0D, amount), 2, MidpointRounding.AwayFromZero)
            Dim dhs = CInt(Math.Truncate(safe))
            Dim fils = CInt(Decimal.Round((safe - dhs) * 100D, 0, MidpointRounding.AwayFromZero))

            Dim sb As New StringBuilder()
            sb.Append(NumberToWords(dhs).ToUpperInvariant())
            sb.Append(" DIRHAMS")
            sb.Append(" AND ")
            sb.Append(fils.ToString("00", CultureInfo.InvariantCulture))
            sb.Append("/100 FILS ONLY")
            Return sb.ToString()
        End Function

        Private Shared Function NumberToWords(n As Integer) As String
            If n = 0 Then Return "zero"
            If n < 0 Then Return "minus " & NumberToWords(Math.Abs(n))

            Dim units = New String() {"", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve", "thirteen", "fourteen", "fifteen", "sixteen", "seventeen", "eighteen", "nineteen"}
            Dim tens = New String() {"", "", "twenty", "thirty", "forty", "fifty", "sixty", "seventy", "eighty", "ninety"}

            Dim words As New List(Of String)()

            Dim billions = n \ 1000000000
            If billions > 0 Then
                words.Add(NumberToWords(billions))
                words.Add("billion")
                n = n Mod 1000000000
            End If

            Dim millions = n \ 1000000
            If millions > 0 Then
                words.Add(NumberToWords(millions))
                words.Add("million")
                n = n Mod 1000000
            End If

            Dim thousands = n \ 1000
            If thousands > 0 Then
                words.Add(NumberToWords(thousands))
                words.Add("thousand")
                n = n Mod 1000
            End If

            Dim hundreds = n \ 100
            If hundreds > 0 Then
                words.Add(units(hundreds))
                words.Add("hundred")
                n = n Mod 100
            End If

            If n > 0 Then
                If n < 20 Then
                    words.Add(units(n))
                Else
                    Dim t = n \ 10
                    Dim u = n Mod 10
                    words.Add(tens(t))
                    If u > 0 Then
                        words.Add(units(u))
                    End If
                End If
            End If

            Return String.Join(" ", words).Replace("  ", " ").Trim()
        End Function
    End Class
End Namespace

