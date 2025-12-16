Namespace LocalPOS.Models
    ''' <summary>
    ''' Persisted/calculated discount record for receipts/audit.
    ''' This captures BOTH intent (scope/type/value/source) and computed amounts (base + applied amount).
    ''' </summary>
    Public Class AppliedDiscountSummary
        Public Property Scope As String
        Public Property ValueType As String
        Public Property Value As Decimal
        Public Property AppliedBaseAmount As Decimal
        Public Property AppliedAmount As Decimal
        Public Property Source As String
        Public Property Reference As String
        Public Property Description As String
        Public Property Priority As Integer
        Public Property IsStackable As Boolean
        Public Property TargetProductId As Integer?
    End Class
End Namespace

