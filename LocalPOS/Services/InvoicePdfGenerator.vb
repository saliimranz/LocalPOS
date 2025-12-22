Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Text.RegularExpressions
Imports iTextSharp.text.pdf
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Services
    ''' <summary>
    ''' Generates an invoice PDF by filling a PDF form template (AcroForm).
    ''' This is intentionally isolated from business logic; it only maps already-calculated values.
    ''' </summary>
    Public Class InvoicePdfGenerator
        Private Shared ReadOnly CurrencyCulture As CultureInfo = CultureInfo.InvariantCulture

        Public Function Generate(order As OrderReceiptData, templatePath As String, billToBlock As String, remarks As String) As ReceiptDocument
            If order Is Nothing Then Throw New ArgumentNullException(NameOf(order))
            If String.IsNullOrWhiteSpace(templatePath) Then Throw New ArgumentNullException(NameOf(templatePath))

            If Not File.Exists(templatePath) Then
                Throw New FileNotFoundException("Invoice PDF template was not found.", templatePath)
            End If

            Try
                Using reader As New PdfReader(templatePath)
                    Using output As New MemoryStream()
                        Using stamper As New PdfStamper(reader, output)
                            Dim form = stamper.AcroFields
                            If form Is Nothing Then
                                Trace.TraceError($"[InvoicePdfGenerator] Template '{templatePath}' has no AcroFields.")
                                Throw New InvalidOperationException("Invoice template does not contain fillable fields.")
                            End If

                            Dim fieldNames = SafeFieldNames(form)
                            If fieldNames.Count = 0 Then
                                Trace.TraceError($"[InvoicePdfGenerator] Template '{templatePath}' contains 0 form fields.")
                                Throw New InvalidOperationException("Invoice template does not contain fillable fields.")
                            End If

                            PopulateHeader(form, fieldNames, order, billToBlock, remarks)
                            PopulateLineItems(form, fieldNames, order.LineItems)
                            PopulateTotals(form, fieldNames, order)

                            ' Flatten to reduce risk of client-side PDF form behavior differences.
                            stamper.FormFlattening = True
                        End Using

                        Dim fileName = SanitizeFileName($"Invoice_{order.OrderNumber}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf")
                        Dim document As New ReceiptDocument() With {
                            .FileName = fileName,
                            .ContentType = "application/pdf",
                            .Content = output.ToArray()
                        }
                        document.EnsureValid()
                        Return document
                    End Using
                End Using
            Catch ex As Exception
                Trace.TraceError($"[InvoicePdfGenerator] Failed to generate invoice PDF for orderId={order.OrderId}, orderNumber='{order.OrderNumber}'. Template='{templatePath}'. Error: {ex}")
                Throw
            End Try
        End Function

        Private Shared Sub PopulateHeader(form As AcroFields, fieldNames As IList(Of String), order As OrderReceiptData, billToBlock As String, remarks As String)
            Dim invoiceNo = If(String.IsNullOrWhiteSpace(order.OrderNumber), "-", order.OrderNumber)
            Dim dateText = order.OrderDate.ToString("dd-MMM-yyyy", CultureInfo.InvariantCulture)
            Dim billTo = billToBlock
            If String.IsNullOrWhiteSpace(billTo) Then
                billTo = If(String.IsNullOrWhiteSpace(order.CustomerName), "Walk-in Customer", order.CustomerName)
            End If

            SetFieldValue(form, fieldNames, invoiceNo, "InvoiceNumber", "InvoiceNo", "InvNo", "INV_NO", "Invoice #", "Invoice No.")
            SetFieldValue(form, fieldNames, dateText, "InvoiceDate", "Date", "InvDate", "Invoice Date")
            SetFieldValue(form, fieldNames, billTo, "BillTo", "Bill To", "BILL_TO", "Customer", "CustomerName", "Customer Name")

            If Not String.IsNullOrWhiteSpace(remarks) Then
                SetFieldValue(form, fieldNames, remarks, "Remarks", "Remark", "Notes", "Note")
            End If
        End Sub

        Private Shared Sub PopulateTotals(form As AcroFields, fieldNames As IList(Of String), order As OrderReceiptData)
            ' Keep legacy invoice calculations intact; only map additional fields if already available.
            Dim subtotalBeforeDiscount = RoundMoney(Math.Max(0D, order.Subtotal))
            Dim totalDiscount = RoundMoney(Math.Max(0D, order.DiscountAmount))
            Dim subtotalDiscountOnly = RoundMoney(Math.Max(0D, order.SubtotalDiscountAmount))
            Dim totalBeforeVat = RoundMoney(Math.Max(0D, subtotalBeforeDiscount - totalDiscount))

            Dim vatAmount = RoundMoney(Math.Max(0D, order.TaxAmount))
            Dim totalAmount = RoundMoney(Math.Max(0D, order.TotalAmount))
            Dim paidAmount = RoundMoney(Math.Max(0D, order.PaidAmount))
            Dim balance = RoundMoney(Math.Max(0D, order.OutstandingAmount))

            SetFieldValue(form, fieldNames, FormatMoney(subtotalBeforeDiscount), "Subtotal", "SubTotal", "SUBTOTAL")

            ' New field: Subtotal Discount (subtotal-level discounts only, excludes VAT and item-level).
            SetFieldValue(form, fieldNames, FormatMoney(subtotalDiscountOnly), "SubtotalDiscount", "Subtotal Discount", "Subtotal_Discount", "Discount_Subtotal")

            ' Existing: Total before VAT (net after total discount, legacy behavior).
            SetFieldValue(form, fieldNames, FormatMoney(totalBeforeVat), "TotalBeforeVAT", "Total Before VAT", "Total_Before_VAT", "TotalBeforeVat", "NetTotal", "Net Total")

            SetFieldValue(form, fieldNames, FormatMoney(vatAmount), "VAT", "Vat", "VATAmount", "VAT Amount", "VatAmount", "Tax", "TaxAmount", "Tax Amount")
            SetFieldValue(form, fieldNames, FormatMoney(totalAmount), "Total", "TotalAmount", "Total Amount", "GrandTotal", "Grand Total")
            SetFieldValue(form, fieldNames, FormatMoney(paidAmount), "Paid", "PaidAmount", "Paid Amount")
            SetFieldValue(form, fieldNames, FormatMoney(balance), "Balance", "BalanceAmount", "Outstanding", "OutstandingAmount", "Outstanding Amount")
        End Sub

        Private Shared Sub PopulateLineItems(form As AcroFields, fieldNames As IList(Of String), items As IList(Of OrderLineItem))
            Dim lineItems = items
            If lineItems Is Nothing OrElse lineItems.Count = 0 Then
                Trace.TraceInformation("[InvoicePdfGenerator] No line items to populate.")
                Return
            End If

            Dim map = DetectLineItemFields(fieldNames)
            If map.Count = 0 Then
                Trace.TraceWarning("[InvoicePdfGenerator] Unable to detect line item fields in PDF template. Line items will not be filled.")
                Return
            End If

            Dim maxRow = map.Keys.Max()
            Dim rowsToFill = Math.Min(lineItems.Count, maxRow)

            For i = 1 To rowsToFill
                If Not map.ContainsKey(i) Then
                    Continue For
                End If
                Dim item = lineItems(i - 1)
                Dim name = If(String.IsNullOrWhiteSpace(item.Name), "Item", item.Name)

                Dim qty = item.Quantity.ToString(CultureInfo.InvariantCulture)
                Dim unit = FormatMoney(RoundMoney(Math.Max(0D, item.UnitPrice)))
                Dim lineTotal = FormatMoney(RoundMoney(Math.Max(0D, item.LineTotal)))
                Dim lineVat = FormatMoney(RoundMoney(Math.Max(0D, item.TaxAmount)))

                Dim rowMap = map(i)
                If rowMap.ContainsKey("description") Then SetFieldValueExact(form, rowMap("description"), name)
                If rowMap.ContainsKey("qty") Then SetFieldValueExact(form, rowMap("qty"), qty)
                If rowMap.ContainsKey("unit") Then SetFieldValueExact(form, rowMap("unit"), unit)
                If rowMap.ContainsKey("total") Then SetFieldValueExact(form, rowMap("total"), lineTotal)
                If rowMap.ContainsKey("vat") Then SetFieldValueExact(form, rowMap("vat"), lineVat)
            Next
        End Sub

        Private Shared Function DetectLineItemFields(fieldNames As IList(Of String)) As Dictionary(Of Integer, Dictionary(Of String, String))
            Dim results As New Dictionary(Of Integer, Dictionary(Of String, String))()
            If fieldNames Is Nothing OrElse fieldNames.Count = 0 Then
                Return results
            End If

            Dim patterns As New Dictionary(Of String, Regex) From {
                {"description", New Regex("(?i)^(desc|description|item|product)[_\-\s]*0*(\d+)$")},
                {"qty", New Regex("(?i)^(qty|quantity)[_\-\s]*0*(\d+)$")},
                {"unit", New Regex("(?i)^(unit|unitprice|unit_price|price)[_\-\s]*0*(\d+)$")},
                {"total", New Regex("(?i)^(total|amount|linetotal|line_total)[_\-\s]*0*(\d+)$")},
                {"vat", New Regex("(?i)^(vat|tax)[_\-\s]*0*(\d+)$")}
            }

            For Each fieldName In fieldNames
                Dim name = NormalizeFieldName(fieldName)
                For Each kvp In patterns
                    Dim m = kvp.Value.Match(name)
                    If Not m.Success Then Continue For

                    Dim idx As Integer
                    If Not Integer.TryParse(m.Groups(2).Value, NumberStyles.Integer, CultureInfo.InvariantCulture, idx) Then
                        Continue For
                    End If
                    If idx <= 0 Then Continue For

                    If Not results.ContainsKey(idx) Then
                        results(idx) = New Dictionary(Of String, String)(StringComparer.OrdinalIgnoreCase)
                    End If
                    If Not results(idx).ContainsKey(kvp.Key) Then
                        results(idx)(kvp.Key) = fieldName
                    End If
                Next
            Next

            ' Only keep rows that have at least a description and total, otherwise mapping is too ambiguous.
            Dim filtered As New Dictionary(Of Integer, Dictionary(Of String, String))()
            For Each row In results.Keys.OrderBy(Function(k) k)
                Dim rowMap = results(row)
                If rowMap.ContainsKey("description") AndAlso rowMap.ContainsKey("total") Then
                    filtered(row) = rowMap
                End If
            Next
            Return filtered
        End Function

        Private Shared Function SafeFieldNames(form As AcroFields) As IList(Of String)
            Try
                Dim keys = form.Fields.Keys
                If keys Is Nothing Then Return New List(Of String)()
                Return keys.Cast(Of String)().ToList()
            Catch ex As Exception
                Trace.TraceError($"[InvoicePdfGenerator] Unable to enumerate PDF form fields: {ex}")
                Return New List(Of String)()
            End Try
        End Function

        Private Shared Sub SetFieldValue(form As AcroFields, fieldNames As IList(Of String), value As String, ParamArray candidates As String())
            If form Is Nothing OrElse fieldNames Is Nothing OrElse candidates Is Nothing OrElse candidates.Length = 0 Then
                Return
            End If

            Dim chosen As String = Nothing

            ' Exact match candidates first.
            For Each candidate In candidates
                If String.IsNullOrWhiteSpace(candidate) Then Continue For
                Dim exact = fieldNames.FirstOrDefault(Function(n) String.Equals(n, candidate, StringComparison.OrdinalIgnoreCase))
                If Not String.IsNullOrWhiteSpace(exact) Then
                    chosen = exact
                    Exit For
                End If
            Next

            ' Fuzzy match by normalized tokens (safer than failing hard).
            If String.IsNullOrWhiteSpace(chosen) Then
                Dim normalizedCandidates = candidates.
                    Where(Function(c) Not String.IsNullOrWhiteSpace(c)).
                    Select(Function(c) NormalizeFieldName(c)).
                    Where(Function(c) c.Length > 0).
                    ToList()

                chosen = fieldNames.FirstOrDefault(Function(n)
                                                       Dim nn = NormalizeFieldName(n)
                                                       Return normalizedCandidates.Any(Function(c) nn.Contains(c, StringComparison.OrdinalIgnoreCase))
                                                   End Function)
            End If

            If String.IsNullOrWhiteSpace(chosen) Then
                Trace.TraceWarning($"[InvoicePdfGenerator] Missing template field for candidates: {String.Join(", ", candidates.Where(Function(c) Not String.IsNullOrWhiteSpace(c)))}")
                Return
            End If

            SetFieldValueExact(form, chosen, value)
        End Sub

        Private Shared Sub SetFieldValueExact(form As AcroFields, fieldName As String, value As String)
            If String.IsNullOrWhiteSpace(fieldName) Then Return
            Dim safeValue = If(value, String.Empty)

            Try
                Dim ok = form.SetField(fieldName, safeValue)
                If Not ok Then
                    Trace.TraceWarning($"[InvoicePdfGenerator] Failed to set PDF field '{fieldName}'.")
                End If
            Catch ex As Exception
                Trace.TraceWarning($"[InvoicePdfGenerator] Error setting PDF field '{fieldName}': {ex.Message}")
            End Try
        End Sub

        Private Shared Function NormalizeFieldName(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then Return String.Empty
            Dim cleaned = Regex.Replace(value.Trim(), "\s+", "_")
            cleaned = cleaned.Replace("-", "_")
            cleaned = cleaned.Replace(".", "")
            Return cleaned
        End Function

        Private Shared Function RoundMoney(amount As Decimal) As Decimal
            Return Decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
        End Function

        Private Shared Function FormatMoney(amount As Decimal) As String
            Return amount.ToString("F2", CurrencyCulture)
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
End Namespace

