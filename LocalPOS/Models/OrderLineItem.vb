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
        ' Item-level discount (stored on dbo.TBL_SP_PSO_ITEM.Total_Discount).
        Public Property DiscountAmount As Decimal
        ' Subtotal-level discount impact for this product (from dbo.TBL_POS_DISCOUNT_ALLOCATION).
        ' This is populated for invoice rendering and tax-safe reporting, without changing legacy flows.
        Public Property SubtotalDiscountAllocationAmount As Decimal
        Public Property TaxAmount As Decimal
    End Class
End Namespace
