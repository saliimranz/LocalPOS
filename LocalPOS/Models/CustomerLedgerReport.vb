Imports System
Imports System.Collections.Generic

Namespace LocalPOS.LocalPOS.Models
    Public Class CustomerLedgerReport
        Public Sub New()
            Entries = New List(Of CustomerLedgerEntry)()
        End Sub

        Public Property DealerId As Integer
        Public Property DealerCode As String
        Public Property DealerName As String
        Public Property LastPaymentDate As Nullable(Of DateTime)
        Public Property PendingAmount As Decimal
        Public Property OpeningBalance As Decimal
        Public Property Entries As IList(Of CustomerLedgerEntry)
    End Class
End Namespace
