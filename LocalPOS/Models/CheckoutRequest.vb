Namespace LocalPOS.Models
    Public Class CheckoutRequest
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property PaymentMethod As String
        Public Property PartialAmount As Decimal?
        Public Property DiscountPercent As Decimal
        Public Property Subtotal As Decimal
        Public Property TaxAmount As Decimal
        Public Property TotalDue As Decimal
        Public Property CartItems As IList(Of CartItem)
        Public Property CreatedBy As String
    End Class
End Namespace
