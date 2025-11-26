Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Lightweight projection of an order with payment progress for profile views.
    ''' </summary>
    Public Class CustomerOrderSummary
        Public Property OrderId As Integer
        Public Property OrderNumber As String
        Public Property InquiryNumber As String
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property Status As String
        Public Property CreatedOn As DateTime
        Public Property TotalAmount As Decimal
        Public Property TotalPaid As Decimal
        Public Property Outstanding As Decimal
        Public Property LastPaymentOn As DateTime?

        Public ReadOnly Property HasOutstanding As Boolean
            Get
                Return Outstanding > 0D
            End Get
        End Property
    End Class
End Namespace
