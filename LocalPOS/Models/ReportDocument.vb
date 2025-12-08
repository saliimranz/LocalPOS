Imports System

Namespace LocalPOS.Models
    ''' <summary>
    ''' Represents a binary document (typically a spreadsheet) that can be streamed to the browser.
    ''' </summary>
    Public Class ReportDocument
        Public Property FileName As String
        Public Property ContentType As String = "application/octet-stream"
        Public Property Content As Byte()

        Public Sub EnsureValid()
            If String.IsNullOrWhiteSpace(FileName) Then
                FileName = $"report_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx"
            End If
            If Content Is Nothing Then
                Content = Array.Empty(Of Byte)()
            End If
            If String.IsNullOrWhiteSpace(ContentType) Then
                ContentType = "application/octet-stream"
            End If
        End Sub
    End Class
End Namespace
