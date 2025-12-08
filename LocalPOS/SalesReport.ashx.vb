Imports System
Imports System.Globalization
Imports System.IO
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class SalesReportHandler
    Implements IHttpHandler

    Private ReadOnly _posService As New PosService()
    Private ReadOnly _reportGenerator As New SalesReportGenerator()

    Public Sub ProcessRequest(context As HttpContext) Implements IHttpHandler.ProcessRequest
        If context Is Nothing Then
            Throw New ArgumentNullException(NameOf(context))
        End If

        Try
            Dim filter = BuildFilter(context)
            Dim templatePath = context.Server.MapPath("~/Templates/Sales.xlsx")
            Dim orders = _posService.GetSalesHistory(filter)
            Dim document = _reportGenerator.Generate(orders, templatePath)
            WriteDocument(context, document)
        Catch ex As FileNotFoundException
            WriteError(context, 500, ex.Message)
        Catch ex As Exception
            WriteError(context, 500, $"Unable to generate sales report. {ex.Message}")
        End Try
    End Sub

    Private Shared Function BuildFilter(context As HttpContext) As SalesHistoryFilter
        If context Is Nothing Then
            Return Nothing
        End If

        Dim query = context.Request.QueryString
        Dim filter As New SalesHistoryFilter()
        Dim hasFilter = False

        Dim fromValue = query("from")
        Dim toValue = query("to")
        Dim orderNumber = query("order")

        Dim fromDate = ParseDate(fromValue)
        Dim toDate = ParseDate(toValue)

        If fromDate.HasValue Then
            filter.FromDate = fromDate
            hasFilter = True
        End If
        If toDate.HasValue Then
            filter.ToDate = toDate
            hasFilter = True
        End If

        If filter.FromDate.HasValue AndAlso filter.ToDate.HasValue AndAlso filter.FromDate.Value > filter.ToDate.Value Then
            Dim temp = filter.FromDate
            filter.FromDate = filter.ToDate
            filter.ToDate = temp
        End If

        If Not String.IsNullOrWhiteSpace(orderNumber) Then
            filter.OrderNumber = orderNumber.Trim()
            hasFilter = True
        End If

        If Not hasFilter Then
            Return Nothing
        End If

        Return filter
    End Function

    Private Shared Function ParseDate(value As String) As DateTime?
        If String.IsNullOrWhiteSpace(value) Then
            Return Nothing
        End If

        Dim parsed As DateTime
        If DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, parsed) Then
            Return parsed.Date
        End If
        If DateTime.TryParse(value, parsed) Then
            Return parsed.Date
        End If

        Return Nothing
    End Function

    Private Shared Sub WriteDocument(context As HttpContext, document As ReportDocument)
        If document Is Nothing Then
            WriteError(context, 404, "Sales report is empty.")
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
