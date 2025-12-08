Imports System
Imports System.Globalization
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Namespace LocalPOS
    Public Class ReceiptDownloadHandler
        Implements IHttpHandler

        Private ReadOnly _posService As New PosService()
        Private ReadOnly _receiptGenerator As New ReceiptGenerator()

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            If context Is Nothing Then
                Throw New ArgumentNullException(NameOf(context))
            End If

            Try
                Dim mode = Convert.ToString(context.Request("mode"), CultureInfo.InvariantCulture)
                If String.IsNullOrWhiteSpace(mode) Then
                    mode = "sale"
                End If

                Select Case mode.ToLowerInvariant()
                    Case "settlement"
                        StreamSettlementReceipt(context)
                    Case Else
                        StreamOrderReceipt(context)
                End Select
            Catch ex As Exception
                context.Response.Clear()
                context.Response.StatusCode = 500
                context.Response.ContentType = "text/plain"
                context.Response.Write($"Unable to download receipt: {ex.Message}")
            End Try
        End Sub

        Private Sub StreamOrderReceipt(context As HttpContext)
            Dim orderId As Integer
            If Not Integer.TryParse(context.Request("orderId"), NumberStyles.Integer, CultureInfo.InvariantCulture, orderId) OrElse orderId <= 0 Then
                WriteBadRequest(context, "Missing or invalid order id.")
                Return
            End If

            Dim receipt = _posService.GetOrderReceipt(orderId)
            If receipt Is Nothing Then
                WriteNotFound(context, "Order was not found.")
                Return
            End If

            Dim document = _receiptGenerator.Generate(receipt)
            WriteDocument(context, document)
        End Sub

        Private Sub StreamSettlementReceipt(context As HttpContext)
            Dim orderId As Integer
            If Not Integer.TryParse(context.Request("orderId"), NumberStyles.Integer, CultureInfo.InvariantCulture, orderId) OrElse orderId <= 0 Then
                WriteBadRequest(context, "Missing or invalid order id.")
                Return
            End If

            Dim receiptNumber = Convert.ToString(context.Request("receipt"), CultureInfo.InvariantCulture)
            If String.IsNullOrWhiteSpace(receiptNumber) Then
                WriteBadRequest(context, "Missing receipt reference.")
                Return
            End If

            Dim receipt = _posService.GetSettlementReceipt(orderId, receiptNumber)
            If receipt Is Nothing Then
                WriteNotFound(context, "Settlement receipt was not found.")
                Return
            End If

            Dim document = _receiptGenerator.GenerateSettlementReceipt(receipt)
            WriteDocument(context, document)
        End Sub

        Private Shared Sub WriteDocument(context As HttpContext, document As ReceiptDocument)
            If document Is Nothing Then
                WriteNotFound(context, "Receipt is empty.")
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

        Private Shared Sub WriteBadRequest(context As HttpContext, message As String)
            context.Response.Clear()
            context.Response.StatusCode = 400
            context.Response.ContentType = "text/plain"
            context.Response.Write(message)
        End Sub

        Private Shared Sub WriteNotFound(context As HttpContext, message As String)
            context.Response.Clear()
            context.Response.StatusCode = 404
            context.Response.ContentType = "text/plain"
            context.Response.Write(message)
        End Sub

        Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
            Get
                Return False
            End Get
        End Property
    End Class
End Namespace
