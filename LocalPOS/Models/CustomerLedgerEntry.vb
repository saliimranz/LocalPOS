Imports System

Public Class CustomerLedgerEntry
        Public Property OrderId As Integer
        Public Property PaymentId As Nullable(Of Integer)
        Public Property EntryDate As Nullable(Of DateTime)
        Public Property Description As String
        Public Property AmountDue As Decimal
        Public Property PaymentReceived As Decimal
        Public Property BalanceAfterEntry As Decimal
        Public Property EntryType As CustomerLedgerEntryType
    End Class

Public Enum CustomerLedgerEntryType
    Order
    Payment
End Enum
