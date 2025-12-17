Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a single line item within the in-memory cart.
    ''' </summary>
    Public Class CartItem
        Public Property ProductId As Integer
        Public Property SkuCode As String
        Public Property Name As String
        Public Property UnitPrice As Decimal
        Public Property Quantity As Integer
        Public Property TaxRate As Decimal
        Public Property Thumbnail As String

        ' Phase 1: optional manual item-level discount input (UI).
        ' Stored only in-session / held-sale intent records (not persisted as part of the SKU).
        Public Property ItemDiscountMode As String = "Percent" ' Percent | Amount
        Public Property ItemDiscountValue As Decimal = 0D

        Public ReadOnly Property LineTotal As Decimal
            Get
                Return UnitPrice * Quantity
            End Get
        End Property

        Public Function Clone() As CartItem
            Return CType(MemberwiseClone(), CartItem)
        End Function
    End Class
End Namespace
