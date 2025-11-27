Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Lightweight projection representing a held sale entry for listing purposes.
    ''' </summary>
    Public Class HeldSaleSummary
        Public Property HeldSaleId As Integer
        Public Property ReferenceCode As String
        Public Property DealerName As String
        Public Property ItemsCount As Integer
        Public Property Subtotal As Decimal
        Public Property TotalAmount As Decimal
        Public Property DiscountPercent As Decimal
        Public Property CreatedOn As DateTime
        Public Property CreatedBy As String

        Public ReadOnly Property RelativeAge As String
            Get
                Dim now = DateTime.UtcNow
                Dim createdUtc = DateTime.SpecifyKind(CreatedOn, DateTimeKind.Utc)
                Dim delta = now - createdUtc
                If delta.TotalSeconds < 60 Then
                    Return "moments ago"
                End If
                If delta.TotalMinutes < 60 Then
                    Dim minutes = Math.Max(1, CInt(Math.Floor(delta.TotalMinutes)))
                    Return $"{minutes} min{If(minutes = 1, "", "s")} ago"
                End If
                If delta.TotalHours < 24 Then
                    Dim hours = Math.Max(1, CInt(Math.Floor(delta.TotalHours)))
                    Return $"{hours} hr{If(hours = 1, "", "s")} ago"
                End If
                Dim days = Math.Max(1, CInt(Math.Floor(delta.TotalDays)))
                Return $"{days} day{If(days = 1, "", "s")} ago"
            End Get
        End Property
    End Class
End Namespace
