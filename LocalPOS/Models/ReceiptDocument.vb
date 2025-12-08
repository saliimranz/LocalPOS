Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents an in-memory receipt payload that can be streamed to the browser.
    ''' </summary>
    Public Class ReceiptDocument
        Public Property FileName As String
        Public Property ContentType As String = "application/pdf"
        Public Property Content As Byte()

        Public Sub EnsureValid()
            If String.IsNullOrWhiteSpace(FileName) Then
                FileName = $"receipt_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf"
            End If
            If Content Is Nothing Then
                Content = Array.Empty(Of Byte)()
            End If
            If String.IsNullOrWhiteSpace(ContentType) Then
                ContentType = "application/pdf"
            End If
        End Sub
    End Class
End Namespace
