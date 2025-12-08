Imports System
Imports System.IO
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Namespace LocalPOS
    Public Class CustomerLedgerReportHandler
        Implements IHttpHandler

        Private ReadOnly _posService As New PosService()
        Private ReadOnly _reportGenerator As New CustomerLedgerReportGenerator()

        Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
            If context Is Nothing Then
                Throw New ArgumentNullException(NameOf(context))
            End If

            Try
                Dim dealerId = ParseCustomerId(context)
                If Not dealerId.HasValue Then
                    WriteError(context, 400, "customerId is required.")
                    Return
                End If

                Dim ledger = _posService.GetCustomerLedger(dealerId.Value)
                If ledger Is Nothing Then
                    WriteError(context, 404, "Customer could not be found.")
                    Return
                End If

                Dim templatePath = context.Server.MapPath("~/Templates/Customer_Ledger.xlsx")
                Dim document = _reportGenerator.Generate(ledger, templatePath)
                WriteDocument(context, document)
            Catch ex As FileNotFoundException
                WriteError(context, 500, ex.Message)
            Catch ex As Exception
                WriteError(context, 500, $"Unable to generate customer ledger. {ex.Message}")
            End Try
        End Sub

        Private Shared Function ParseCustomerId(context As HttpContext) As Integer?
            If context Is Nothing Then
                Return Nothing
            End If

            Dim raw = context.Request("customerId")
            Dim parsed As Integer
            If Integer.TryParse(raw, parsed) AndAlso parsed >= 0 Then
                Return parsed
            End If
            Return Nothing
        End Function

        Private Shared Sub WriteDocument(context As HttpContext, document As ReportDocument)
            If document Is Nothing Then
                WriteError(context, 404, "Customer ledger is empty.")
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
            context.ApplicationInstance.CompleteRequest()
        End Sub

        Public ReadOnly Property IsReusable As Boolean Implements IHttpHandler.IsReusable
            Get
                Return False
            End Get
        End Property
    End Class
End Namespace
