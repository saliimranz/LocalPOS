Imports System.Data.SqlClient
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Data
    Public Class DealerRepository
        Inherits SqlRepositoryBase

        Public Function GetDealers() As IList(Of Dealer)
            Dim dealers As New List(Of Dealer) From {
                New Dealer() With {
                    .Id = 0,
                    .DealerName = "Walk-in Customer",
                    .DealerCode = "WALKIN"
                }
            }

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT ID, DealerName, DealerID, ContactPerson, CellNumber, City FROM dbo.TBL_DEALERS WHERE ISNULL(Status,1) = 1 ORDER BY DealerName"
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            dealers.Add(MapDealer(reader))
                        End While
                    End Using
                End Using
            End Using

            Return dealers
        End Function

        Public Function GetDealer(dealerId As Integer) As Dealer
            If dealerId = 0 Then
                Return New Dealer() With {
                    .Id = 0,
                    .DealerName = "Walk-in Customer",
                    .DealerCode = "WALKIN"
                }
            End If

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT TOP 1 ID, DealerName, DealerID, ContactPerson, CellNumber, City FROM dbo.TBL_DEALERS WHERE ID = @Id"
                    command.Parameters.AddWithValue("@Id", dealerId)
                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return MapDealer(reader)
                        End If
                    End Using
                End Using
            End Using
            Return Nothing
        End Function

        Private Shared Function MapDealer(reader As SqlDataReader) As Dealer
            Dim dealer As New Dealer() With {
                .Id = reader.GetInt32(reader.GetOrdinal("ID")),
                .DealerName = If(reader.IsDBNull(reader.GetOrdinal("DealerName")), "Dealer", reader.GetString(reader.GetOrdinal("DealerName"))),
                .DealerCode = If(reader.IsDBNull(reader.GetOrdinal("DealerID")), String.Empty, reader.GetString(reader.GetOrdinal("DealerID"))),
                .ContactPerson = If(reader.IsDBNull(reader.GetOrdinal("ContactPerson")), Nothing, reader.GetString(reader.GetOrdinal("ContactPerson"))),
                .CellNumber = If(reader.IsDBNull(reader.GetOrdinal("CellNumber")), Nothing, reader.GetString(reader.GetOrdinal("CellNumber"))),
                .City = If(reader.IsDBNull(reader.GetOrdinal("City")), Nothing, reader.GetString(reader.GetOrdinal("City")))
            }
            Return dealer
        End Function
    End Class
End Namespace
