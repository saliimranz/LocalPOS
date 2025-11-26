Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents an individual SKU definition captured from the UI.
    ''' </summary>
    Public Class ProductSkuRequest
        Public Property SkuCode As String
        Public Property DisplayName As String
        Public Property Description As String
        Public Property RetailPrice As Decimal
        Public Property TaxRate As Decimal
        Public Property StockQuantity As Integer
        Public Property MinStockThreshold As Integer
        Public Property ImageUrl As String
    End Class
End Namespace
