Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a binary document attachment captured during dealer onboarding.
    ''' </summary>
    Public Class DealerDocument
        Public Property FileName As String
        Public Property ContentType As String
        Public Property Content As Byte()

        Public ReadOnly Property HasContent As Boolean
            Get
                Return Content IsNot Nothing AndAlso Content.Length > 0
            End Get
        End Property
    End Class

    ''' <summary>
    ''' Upsert payload for creating or updating a dealer record.
    ''' </summary>
    Public Class DealerUpsertRequest
        Public Property DealerId As Integer
        Public Property DealerName As String
        Public Property DealerCode As String
        Public Property DealerOldCode As String
        Public Property ContactPerson As String
        Public Property Cnic As String
        Public Property CnicExpiry As Date?
        Public Property Email As String
        Public Property CustomerEmail As String
        Public Property Address As String
        Public Property City As String
        Public Property State As String
        Public Property Country As String
        Public Property Website As String
        Public Property Branch As String
        Public Property BranchCode As String
        Public Property SalesPersonName As String
        Public Property SalesPersonId As Integer?
        Public Property LoginName As String
        Public Property LoginPassword As String
        Public Property TypeCode As String
        Public Property ParentId As Integer?
        Public Property StatusActive As Boolean
        Public Property SmsEnabled As Boolean
        Public Property AppLoginEnabled As Boolean
        Public Property DealerInvestment As String
        Public Property ContactNumberNotes As String
        Public Property EmailNotes As String
        Public Property SalesTerritoryCode As Integer?
        Public Property CellNumber As String
        Public Property AlternateNumbers As String
        Public Property Phone1 As String
        Public Property Phone2 As String
        Public Property Phone3 As String
        Public Property Ntn As String
        Public Property Stn As String
        Public Property DefaultDiscountPercentage As Decimal?
        Public Property IpAddress As String

        Public Property TradeLicenseDocument As DealerDocument
        Public Property CnicDocument As DealerDocument
        Public Property NtnDocument As DealerDocument
        Public Property LogoDocument As DealerDocument
    End Class
End Namespace
