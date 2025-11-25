Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a walk-in customer or a registered dealer/customer.
    ''' </summary>
    Public Class Dealer
        Public Property Id As Integer
        Public Property DealerName As String
        Public Property DealerCode As String
        Public Property ContactPerson As String
        Public Property CellNumber As String
        Public Property City As String
        Public Property CreditLimit As Decimal?
        Public Property OutstandingBalance As Decimal?

        Public ReadOnly Property IsWalkIn As Boolean
            Get
                Return Id = 0
            End Get
        End Property
    End Class
End Namespace
