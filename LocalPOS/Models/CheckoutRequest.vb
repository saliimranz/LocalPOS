Namespace LocalPOS.Models
    Public Class CheckoutRequest
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property PaymentMethod As String
        Public Property PartialAmount As Decimal?
        Public Property CorporatePaymentType As String
        Public Property PaymentAmount As Decimal
        Public Property CashReceived As Decimal?
        Public Property CashChange As Decimal?
        Public Property CardRrn As String
        Public Property CardAuthCode As String
        Public Property CardStatus As String
        Public Property TaxPercent As Decimal
        ' Legacy field (deprecated). Prefer Discounts with explicit scope + type + source.
        Public Property DiscountPercent As Decimal
        Public Property Discounts As IList(Of CheckoutDiscount)
        Public Property Subtotal As Decimal
        Public Property TaxAmount As Decimal
        Public Property TotalDue As Decimal
        Public Property CartItems As IList(Of CartItem)
        Public Property CreatedBy As String
    End Class
End Namespace
