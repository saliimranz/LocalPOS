Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class OrderInvoiceHandler
    Implements IHttpHandler

    Private ReadOnly _posService As New PosService()
    Private ReadOnly _invoiceGenerator As New OrderInvoiceGenerator()
    Private ReadOnly _invoicePdfGenerator As New OrderInvoicePdfGenerator()

    Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
        If context Is Nothing Then
            Throw New ArgumentNullException(NameOf(context))
        End If

        Try
            Dim orderId As Integer
            If Not Integer.TryParse(context.Request("orderId"), NumberStyles.Integer, CultureInfo.InvariantCulture, orderId) OrElse orderId <= 0 Then
                WriteError(context, 400, "Missing or invalid order id.")
                Return
            End If

            Dim order = _posService.GetOrderReceipt(orderId)
            If order Is Nothing Then
                WriteError(context, 404, "Order was not found.")
                Return
            End If

            Dim templatePath = context.Server.MapPath("~/Templates/INVOICE.xlsx")
            Dim dealer = ResolveDealer(order)
            Dim billTo = BuildBillToBlock(order, dealer)
            Dim remarks = BuildRemarks(order)
            Dim document = ResolveDocument(context, order, templatePath, billTo, remarks)
            WriteDocument(context, document)
        Catch ex As FileNotFoundException
            WriteError(context, 500, ex.Message)
        Catch ex As Exception
            WriteError(context, 500, $"Unable to generate invoice. {ex.Message}")
        End Try
    End Sub

    Private Function ResolveDocument(context As HttpContext, order As OrderReceiptData, templatePath As String, billTo As String, remarks As String) As ReportDocument
        Dim format = If(context IsNot Nothing, context.Request("format"), Nothing)
        If Not String.IsNullOrWhiteSpace(format) AndAlso format.Trim().Equals("xlsx", StringComparison.OrdinalIgnoreCase) Then
            Return _invoiceGenerator.Generate(order, templatePath, billTo, remarks)
        End If

        ' Default: PDF invoice.
        Return _invoicePdfGenerator.Generate(order, billTo, remarks)
    End Function

    Private Function ResolveDealer(order As OrderReceiptData) As Dealer
        If order Is Nothing OrElse order.DealerId <= 0 Then
            Return Nothing
        End If

        Return _posService.GetDealer(order.DealerId)
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
