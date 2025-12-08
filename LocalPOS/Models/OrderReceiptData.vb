Imports System
Imports System.Collections.Generic

Namespace LocalPOS.Models
    ''' <summary>
    ''' Fully-populated representation of an order required to render a receipt.
    ''' </summary>
    Public Class OrderReceiptData
        Public Property OrderId As Integer
        Public Property OrderNumber As String
        Public Property ReceiptNumber As String
        Public Property OrderDate As DateTime
        Public Property CustomerName As String
        Public Property PaymentMethod As String
        Public Property CashierName As String
        Public Property Subtotal As Decimal
        Public Property DiscountAmount As Decimal
        Public Property DiscountPercent As Decimal
        Public Property TaxPercent As Decimal
        Public Property TaxAmount As Decimal
        Public Property TotalAmount As Decimal
        Public Property PaidAmount As Decimal
        Public Property OutstandingAmount As Decimal
        Public Property LineItems As IList(Of OrderLineItem)
    End Class
End Namespace
