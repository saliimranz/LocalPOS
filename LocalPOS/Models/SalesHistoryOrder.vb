Imports System
Imports System.Collections.Generic

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents an order displayed in the sales history screen.
    ''' </summary>
    Public Class SalesHistoryOrder
        Public Property OrderId As Integer
        Public Property OrderNumber As String
        Public Property CreatedOn As DateTime
        Public Property CustomerName As String
        Public Property PaymentMethod As String
        Public Property TotalAmount As Decimal
        Public Property TotalPaid As Decimal
        Public Property OutstandingAmount As Decimal
        Public Property Subtotal As Decimal
        Public Property DiscountAmount As Decimal
        Public Property DiscountPercent As Decimal
        Public Property TaxPercent As Decimal
        Public Property TaxAmount As Decimal
        Public Property ReceiptNumber As String
        Public Property LineItems As IList(Of OrderLineItem)
        Public Property CashierName As String

        Public ReadOnly Property HasOutstanding As Boolean
            Get
                Return OutstandingAmount > 0D
            End Get
        End Property
    End Class
End Namespace
