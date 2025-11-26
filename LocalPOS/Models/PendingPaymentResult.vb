Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Result returned after applying a pending payment settlement.
    ''' </summary>
    Public Class PendingPaymentResult
        Public Property OrderId As Integer
        Public Property OrderNumber As String
        Public Property ReceiptNumber As String
        Public Property DealerName As String
        Public Property TotalOrderAmount As Decimal
        Public Property PreviouslyPaid As Decimal
        Public Property OutstandingBeforePayment As Decimal
        Public Property SettledAmount As Decimal
        Public Property VatPercent As Decimal
        Public Property PaymentMethod As String
        Public Property CardRrn As String
        Public Property CardAuthCode As String
        Public Property CashReceived As Decimal?
        Public Property CashChange As Decimal?
        Public Property CashierName As String
        Public Property CompletedOn As DateTime
        Public Property ReceiptFilePath As String
    End Class
End Namespace
