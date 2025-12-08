Imports System
Imports System.IO
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class ReceivablesReportHandler
    Implements IHttpHandler

    Private ReadOnly _posService As New PosService()
    Private ReadOnly _reportGenerator As New ReceivablesReportGenerator()

    Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
        If context Is Nothing Then
            Throw New ArgumentNullException(NameOf(context))
        End If

        Try
            Dim templatePath = context.Server.MapPath("~/Templates/Receivables.xlsx")
            Dim receivables = _posService.GetReceivables()
            Dim document = _reportGenerator.Generate(receivables, templatePath)
            WriteDocument(context, document)
        Catch ex As FileNotFoundException
            WriteError(context, 500, ex.Message)
        Catch ex As Exception
            WriteError(context, 500, $"Unable to generate receivables report. {ex.Message}")
        End Try
    End Sub

    Private Shared Sub WriteDocument(context As HttpContext, document As ReportDocument)
        If document Is Nothing Then
            WriteError(context, 404, "Receivables report is empty.")
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
