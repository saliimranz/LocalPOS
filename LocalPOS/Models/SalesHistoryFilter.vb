Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents UI filter selections for the sales history page.
    ''' </summary>
    Public Class SalesHistoryFilter
        Public Property FromDate As DateTime?
        Public Property ToDate As DateTime?
        Public Property OrderNumber As String
    End Class
End Namespace
