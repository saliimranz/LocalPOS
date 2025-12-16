Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Explicit discount intent sent during checkout.
    ''' This decouples "intent/scope" from any internal allocation used for tax correctness.
    ''' </summary>
    Public Class CheckoutDiscount
        ''' <summary>
        ''' Discount scope/intent. Phase 1 supports: ITEM, SUBTOTAL.
        ''' </summary>
        Public Property Scope As String

        ''' <summary>
        ''' How the discount is expressed: PERCENT or AMOUNT.
        ''' </summary>
        Public Property ValueType As String

        ''' <summary>
        ''' Percent (0-100) or amount in currency units depending on ValueType.
        ''' </summary>
        Public Property Value As Decimal

        ''' <summary>
        ''' Optional target product id for ITEM scoped discounts.
        ''' </summary>
        Public Property TargetProductId As Integer?

        ''' <summary>
        ''' Source/intent metadata for auditing (e.g. Manual, PromoCode, CustomerDefault).
        ''' </summary>
        Public Property Source As String

        ''' <summary>
        ''' Optional human reference like promo code or reason.
        ''' </summary>
        Public Property Reference As String

        ''' <summary>
        ''' Optional display description.
        ''' </summary>
        Public Property Description As String

        ''' <summary>
        ''' Higher priority resolves first when a future rules engine is added.
        ''' </summary>
        Public Property Priority As Integer

        ''' <summary>
        ''' Future: whether this discount is allowed to stack with others.
        ''' </summary>
        Public Property IsStackable As Boolean = True
    End Class
End Namespace

