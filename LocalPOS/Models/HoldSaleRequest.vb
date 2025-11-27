Imports System.Collections.Generic

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents the data required to persist a cart as a held sale.
    ''' </summary>
    Public Class HoldSaleRequest
        Public Property HeldSaleId As Integer?
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property DiscountPercent As Decimal
        Public Property DiscountAmount As Decimal
        Public Property TaxPercent As Decimal
        Public Property TaxAmount As Decimal
        Public Property Subtotal As Decimal
        Public Property TotalAmount As Decimal
        Public Property Items As IList(Of CartItem)
        Public Property CreatedBy As String
    End Class
End Namespace
