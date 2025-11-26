Imports System.Collections.Generic

Namespace LocalPOS.Models
    ''' <summary>
    ''' Snapshot of an order's pending payment position used to drive settlements.
    ''' </summary>
    Public Class PendingPaymentContext
        Public Property OrderId As Integer
        Public Property OrderNumber As String
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property TotalAmount As Decimal
        Public Property OutstandingAmount As Decimal
        Public Property VatPercent As Decimal
        Public Property PreviouslyPaid As Decimal
        Public Property Payments As IList(Of OrderPaymentRecord)
    End Class
End Namespace
