Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a persisted payment row taken against an order.
    ''' </summary>
    Public Class OrderPaymentRecord
        Public Property PaymentId As Integer
        Public Property OrderId As Integer
        Public Property PaymentReference As String
        Public Property PaymentMethod As String
        Public Property PaidAmount As Decimal
        Public Property OutstandingAfterPayment As Decimal
        Public Property IsPartial As Boolean
        Public Property CreatedOn As DateTime
        Public Property Notes As String
    End Class
End Namespace
