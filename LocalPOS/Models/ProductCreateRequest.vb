Imports System.Collections.Generic

Namespace LocalPOS.Models
    ''' <summary>
    ''' Encapsulates all data needed to create a new spare part with its SKUs.
    ''' </summary>
    Public Class ProductCreateRequest
        Public Sub New()
            Skus = New List(Of ProductSkuRequest)()
        End Sub

        Public Property SparePartCode As String
        Public Property DisplayName As String
        Public Property Description As String
        Public Property Category As String
        Public Property Brand As String
        Public Property Skus As IList(Of ProductSkuRequest)
    End Class
End Namespace
