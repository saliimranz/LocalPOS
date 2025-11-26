Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents the product/SKU combination that can be sold through the POS.
    ''' </summary>
    Public Class Product
        Public Property Id As Integer
        Public Property SparePartId As Integer
        Public Property SkuCode As String
        Public Property DisplayName As String
        Public Property Category As String
        Public Property Brand As String
        Public Property ImageUrl As String
        Public Property UnitPrice As Decimal
        Public Property TaxRate As Decimal
        Public Property Description As String
        Public Property StockQuantity As Integer
    End Class
End Namespace
