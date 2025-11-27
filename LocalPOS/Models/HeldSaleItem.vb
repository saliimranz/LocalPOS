Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a single line item that was parked as part of a held sale.
    ''' </summary>
    Public Class HeldSaleItem
        Public Property HeldSaleId As Integer
        Public Property ProductId As Integer
        Public Property SkuCode As String
        Public Property Name As String
        Public Property UnitPrice As Decimal
        Public Property Quantity As Integer
        Public Property TaxRate As Decimal
        Public Property LineTotal As Decimal
        Public Property ThumbnailUrl As String
    End Class
End Namespace
