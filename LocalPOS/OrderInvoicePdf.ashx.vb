Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Globalization
Imports System.IO
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class OrderInvoicePdfHandler
    Implements IHttpHandler

    Private ReadOnly _posService As New PosService()
    Private ReadOnly _pdfInvoiceGenerator As New InvoicePdfGenerator()
    Private ReadOnly _xlsxInvoiceGenerator As New OrderInvoiceGenerator()

    Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
        If context Is Nothing Then
            Throw New ArgumentNullException(NameOf(context))
        End If

        Dim orderId As Integer
        If Not Integer.TryParse(context.Request("orderId"), NumberStyles.Integer, CultureInfo.InvariantCulture, orderId) OrElse orderId <= 0 Then
            WriteError(context, 400, "Missing or invalid order id.")
            Return
        End If

        Dim order As OrderReceiptData = Nothing
        Try
            order = _posService.GetOrderReceipt(orderId)
        Catch ex As Exception
            Trace.TraceError($"[invoice-pdf] Failed to load order receipt for orderId={orderId}. {ex}")
        End Try

        If order Is Nothing Then
            WriteError(context, 404, "Order was not found.")
            Return
        End If

        Dim dealer = ResolveDealer(order)
        Dim remarks = BuildRemarks(order)

        ' Preferred: PDF template-based invoice for Print Invoice flow.
        Try
            Dim templatePath = context.Server.MapPath("~/Templates/Invoice_Template.pdf")
            Dim document = _pdfInvoiceGenerator.Generate(order, templatePath, dealer, remarks)
            WriteDocument(context, document)
            Return
        Catch ex As FileNotFoundException
            Trace.TraceError($"[invoice-pdf] Template missing. {ex.FileName}. {ex}")
        Catch ex As Exception
            Trace.TraceError($"[invoice-pdf] Unable to generate PDF invoice for orderId={orderId}. {ex}")
        End Try

        ' Safety fallback: legacy Excel invoice generation (keeps system stable).
        Try
            Dim legacyTemplatePath = context.Server.MapPath("~/Templates/SalesTaxInvoice.xlsx")
            Dim billTo = BuildBillToBlock(order, dealer)
            Dim legacyDoc = _xlsxInvoiceGenerator.Generate(order, legacyTemplatePath, billTo, remarks)
            WriteDocument(context, legacyDoc)
        Catch ex As Exception
            Trace.TraceError($"[invoice-pdf] Legacy fallback also failed for orderId={orderId}. {ex}")
            WriteError(context, 500, "Unable to generate invoice right now. Please try again or contact support.")
        End Try
    End Sub

    Private Function ResolveDealer(order As OrderReceiptData) As Dealer
        If order Is Nothing OrElse order.DealerId <= 0 Then
            Return Nothing
        End If

        Try
            Return _posService.GetDealer(order.DealerId)
        Catch ex As Exception
            Trace.TraceError($"[invoice-pdf] Failed to load dealer for dealerId={order.DealerId}. {ex}")
            Return Nothing
        End Try
    End Function

    Private Shared Function BuildBillToBlock(order As OrderReceiptData, dealer As Dealer) As String
        Dim lines As New List(Of String)()
        Dim primaryName = If(String.IsNullOrWhiteSpace(order.CustomerName), "Walk-in Customer", order.CustomerName)
        lines.Add(primaryName)

        If dealer IsNot Nothing Then
            If Not String.IsNullOrWhiteSpace(dealer.ContactPerson) Then
                Dim attnLine = $"Attn: {dealer.ContactPerson}"
                If Not lines.Contains(attnLine) Then
                    lines.Add(attnLine)
                End If
            End If
            If Not String.IsNullOrWhiteSpace(dealer.City) Then
                lines.Add(dealer.City)
            End If
            If Not String.IsNullOrWhiteSpace(dealer.CellNumber) Then
                lines.Add($"Phone: {dealer.CellNumber}")
            End If
        End If

        If lines.Count = 1 Then
            lines.Add("Dubai, UAE")
        End If

        Return String.Join(Environment.NewLine, lines)
    End Function

    Private Shared Function BuildRemarks(order As OrderReceiptData) As String
        Dim parts As New List(Of String)()

        If Not String.IsNullOrWhiteSpace(order.PaymentMethod) Then
            parts.Add($"Payment: {order.PaymentMethod}")
        End If

        parts.Add($"Paid: AED {order.PaidAmount.ToString("F2", CultureInfo.InvariantCulture)}")

        If order.OutstandingAmount > 0D Then
            parts.Add($"Outstanding: AED {order.OutstandingAmount.ToString("F2", CultureInfo.InvariantCulture)}")
            parts.Add("Status: Partial payment")
        Else
            parts.Add("Status: Paid in full")
        End If

        Return String.Join(" | ", parts)
    End Function

    Private Shared Sub WriteDocument(context As HttpContext, document As ReportDocument)
        If document Is Nothing Then
            WriteError(context, 404, "Invoice is empty.")
            Return
        End If

        document.EnsureValid()
        context.Response.Clear()
        context.Response.ContentType = document.ContentType
        context.Response.AddHeader("Content-Disposition", $"attachment; filename=""{document.FileName}""")
        context.Response.BinaryWrite(document.Content)
        context.Response.Flush()
        context.ApplicationInstance.CompleteRequest()
    End Sub

    Private Shared Sub WriteError(context As HttpContext, statusCode As Integer, message As String)
        context.Response.Clear()
        context.Response.StatusCode = statusCode
        context.Response.ContentType = "text/plain"
        context.Response.Write(message)
    End Sub

    Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
        Get
            Return False
        End Get
    End Property
End Class

