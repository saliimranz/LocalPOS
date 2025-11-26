Imports System.Configuration
Imports System.Data.SqlClient

Namespace LocalPOS.Data
    Public MustInherit Class SqlRepositoryBase
        Private ReadOnly _connectionString As String

        Protected Sub New(Optional connectionStringName As String = "POSConnection")
            Dim settings = ConfigurationManager.ConnectionStrings(connectionStringName)
            If settings Is Nothing Then
                Throw New InvalidOperationException($"Connection string '{connectionStringName}' was not found in Web.config.")
            End If
            _connectionString = settings.ConnectionString
        End Sub

        Protected Function CreateConnection() As SqlConnection
            Dim connection = New SqlConnection(_connectionString)
            connection.Open()
            Return connection
        End Function
    End Class
End Namespace
