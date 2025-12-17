Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a persisted order line item used in history and receipt views.
    ''' </summary>
    Public Class OrderLineItem
        Public Property ProductId As Integer
        Public Property Name As String
        Public Property Quantity As Integer
        Public Property UnitPrice As Decimal
        Public Property LineTotal As Decimal
        Public Property TaxRate As Decimal
        Public Property DiscountAmount As Decimal
        Public Property TaxAmount As Decimal
    End Class
End Namespace
