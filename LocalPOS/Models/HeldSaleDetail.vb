Imports System
Imports System.Collections.Generic

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a held sale including all of its items so it can be restored into the cart.
    ''' </summary>
    Public Class HeldSaleDetail
        Public Property HeldSaleId As Integer
        Public Property ReferenceCode As String
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property DiscountPercent As Decimal
        Public Property DiscountAmount As Decimal
        Public Property TaxPercent As Decimal
        Public Property TaxAmount As Decimal
        Public Property Subtotal As Decimal
        Public Property TotalAmount As Decimal
        Public Property CreatedOn As DateTime
        Public Property CreatedBy As String
        Public Property Items As IList(Of HeldSaleItem)
    End Class
End Namespace
