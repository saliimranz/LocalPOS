Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a single receivable entry used when generating the receivables XLSX report.
    ''' </summary>
    Public Class ReceivableReportRow
        Public Property OrderId As Integer
        Public Property CustomerName As String
        Public Property InvoiceNumber As String
        Public Property InvoiceDate As DateTime
        Public Property TotalAmount As Decimal
        Public Property PaidAmount As Decimal
        Public Property OutstandingAmount As Decimal
    End Class
End Namespace
