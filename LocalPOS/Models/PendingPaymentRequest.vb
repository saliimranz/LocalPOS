Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' User-supplied inputs required to close an outstanding balance.
    ''' </summary>
    Public Class PendingPaymentRequest
        Public Property OrderId As Integer
        Public Property PaymentMethod As String
        Public Property PaymentAmount As Decimal
        Public Property CashReceived As Decimal?
        Public Property CashChange As Decimal?
        Public Property CardRrn As String
        Public Property CardAuthCode As String
        Public Property CardStatus As String
        Public Property CreatedBy As String
    End Class
End Namespace
