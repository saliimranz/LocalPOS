Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a walk-in customer or a registered dealer/customer.
    ''' </summary>
    Public Class Dealer
        Public Property Id As Integer
        Public Property DealerName As String
        Public Property DealerCode As String
        Public Property DealerOldCode As String
        Public Property ContactPerson As String
        Public Property CellNumber As String
        Public Property AlternateNumbers As String
        Public Property Phone1 As String
        Public Property Phone2 As String
        Public Property Phone3 As String
        Public Property City As String
        Public Property State As String
        Public Property Country As String
        Public Property Address As String
        Public Property Website As String
        Public Property Email As String
        Public Property CustomerEmail As String
        Public Property Cnic As String
        Public Property CnicExpiry As Date?
        Public Property Ntn As String
        Public Property Stn As String
        Public Property Branch As String
        Public Property BranchCode As String
        Public Property SalesPersonName As String
        Public Property SalesPersonId As Integer?
        Public Property LoginName As String
        Public Property LoginPassword As String
        Public Property TypeCode As String
        Public Property ParentId As Integer?
        Public Property SmsEnabled As Boolean
        Public Property AppLoginEnabled As Boolean
        Public Property StatusActive As Boolean
        Public Property DealerInvestment As String
        Public Property ContactNumberNotes As String
        Public Property EmailNotes As String
        Public Property SalesTerritoryCode As Integer?
        Public Property CreditLimit As Decimal?
        Public Property OutstandingBalance As Decimal?
        Public Property IpAddress As String
        Public Property CreatedOn As Date?

        Public Property TradeLicenseDocument As DealerDocument
        Public Property CnicDocument As DealerDocument
        Public Property NtnDocument As DealerDocument
        Public Property LogoDocument As DealerDocument

        Public ReadOnly Property IsWalkIn As Boolean
            Get
                Return Id = 0
            End Get
        End Property
    End Class
End Namespace
