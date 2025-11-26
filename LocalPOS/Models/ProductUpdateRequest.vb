Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents the fields that can be updated for an existing SKU/product.
    ''' </summary>
    Public Class ProductUpdateRequest
        Public Property ProductId As Integer
        Public Property DisplayName As String
        Public Property Description As String
        Public Property RetailPrice As Decimal
        Public Property TaxRate As Decimal
        Public Property StockQuantity As Integer
        Public Property MinStockThreshold As Integer
        Public Property ImageUrl As String
    End Class
End Namespace
