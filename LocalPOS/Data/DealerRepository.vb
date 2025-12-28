Imports System
Imports System.Data
Imports System.Data.SqlClient
Imports System.Globalization
Imports System.Text
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Data
    Public Class DealerRepository
        Inherits SqlRepositoryBase

        Public Function GetDealers() As IList(Of Dealer)
            Dim dealers As New List(Of Dealer) From {CreateWalkInDealer()}

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT ID, DealerName, DealerID, ContactPerson, CellNumber, City FROM dbo.TBL_DEALERS WHERE ISNULL(Status, 1) = 1 ORDER BY DealerName"
                    Using reader = command.ExecuteReader()
                        While reader.Read()
                            dealers.Add(MapDealerSummary(reader))
                        End While
                    End Using
                End Using
            End Using

            Return dealers
        End Function

        Public Function GetDealer(dealerId As Integer) As Dealer
            If dealerId = 0 Then
                Return CreateWalkInDealer()
            End If

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "SELECT TOP 1" & vbCrLf &
                                        "    ID," & vbCrLf &
                                        "    DealerName," & vbCrLf &
                                        "    DealerID," & vbCrLf &
                                        "    ContactPerson," & vbCrLf &
                                        "    CellNumber," & vbCrLf &
                                        "    City," & vbCrLf &
                                        "    DealerOldID," & vbCrLf &
                                        "    CNIC," & vbCrLf &
                                        "    CNICExpiry," & vbCrLf &
                                        "    Email," & vbCrLf &
                                        "    Email_customer AS CustomerEmail," & vbCrLf &
                                        "    Address," & vbCrLf &
                                        "    State," & vbCrLf &
                                        "    Country," & vbCrLf &
                                        "    website," & vbCrLf &
                                        "    Branch," & vbCrLf &
                                        "    Br_Code," & vbCrLf &
                                        "    SALES_PERSON," & vbCrLf &
                                        "    SLSP_ID," & vbCrLf &
                                        "    Login," & vbCrLf &
                                        "    Password," & vbCrLf &
                                        "    TYPED," & vbCrLf &
                                        "    parentID," & vbCrLf &
                                        "    Status," & vbCrLf &
                                        "    SMS_ACT," & vbCrLf &
                                        "    APP_LOGIN," & vbCrLf &
                                        "    DEALER_INVST," & vbCrLf &
                                        "    CNO_D AS ContactNumberNotes," & vbCrLf &
                                        "    EM_D AS EmailNotes," & vbCrLf &
                                        "    ST_R AS SalesTerritoryCode," & vbCrLf &
                                        "    IP_ADDRESS," & vbCrLf &
                                        "    CDATED," & vbCrLf &
                                        "    CellNumbers AS AlternateNumbers," & vbCrLf &
                                        "    Phone1," & vbCrLf &
                                        "    Phone2," & vbCrLf &
                                        "    Phone3," & vbCrLf &
                                        "    NTN," & vbCrLf &
                                        "    STN," & vbCrLf &
                                        "    Default_Discount_Percentage," & vbCrLf &
                                        "    [Name] AS LicenseFileName," & vbCrLf &
                                        "    ContentType AS LicenseContentType," & vbCrLf &
                                        "    Data AS LicenseFileData," & vbCrLf &
                                        "    NameCNIC," & vbCrLf &
                                        "    ContentTypeCNIC," & vbCrLf &
                                        "    DataCNIC," & vbCrLf &
                                        "    NameNTN," & vbCrLf &
                                        "    ContentTypeNTN," & vbCrLf &
                                        "    DataNTN," & vbCrLf &
                                        "    NameLOGO," & vbCrLf &
                                        "    ContentTypeLOGO," & vbCrLf &
                                        "    DataLOGO" & vbCrLf &
                                        "FROM dbo.TBL_DEALERS" & vbCrLf &
                                        "WHERE ID = @Id;"
                    command.Parameters.AddWithValue("@Id", dealerId)

                    Using reader = command.ExecuteReader()
                        If reader.Read() Then
                            Return MapDealerDetail(reader)
                        End If
                    End Using
                End Using
            End Using

            Return Nothing
        End Function

        Public Function CreateDealer(request As DealerUpsertRequest) As Integer
            ValidateRequest(request, requireId:=False)

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    command.CommandText = "INSERT INTO dbo.TBL_DEALERS" & vbCrLf &
                                        "(DealerName, DealerID, DealerOldID, ContactPerson, CNIC, CNICExpiry, Email, Email_customer, Address, City, State, Country, website, Branch, Br_Code, SALES_PERSON, SLSP_ID, Login, Password, TYPED, parentID, Status, SMS_ACT, APP_LOGIN, DEALER_INVST, CNO_D, EM_D, ST_R, IP_ADDRESS, CellNumber, CellNumbers, Phone1, Phone2, Phone3, NTN, STN, CDATED, [Name], ContentType, Data, NameCNIC, ContentTypeCNIC, DataCNIC, NameNTN, ContentTypeNTN, DataNTN, NameLOGO, ContentTypeLOGO, DataLOGO)" & vbCrLf &
                                        "VALUES" & vbCrLf &
                                        "(@DealerName, @DealerID, @DealerOldID, @ContactPerson, @CNIC, @CNICExpiry, @Email, @CustomerEmail, @Address, @City, @State, @Country, @Website, @Branch, @BranchCode, @SalesPerson, @SalesPersonId, @Login, @Password, @TypeCode, @ParentId, @Status, @SmsAct, @AppLogin, @DealerInvestment, @ContactNotes, @EmailNotes, @SalesTerritory, @IpAddress, @CellNumber, @AlternateNumbers, @Phone1, @Phone2, @Phone3, @NTN, @STN, GETDATE(), @LicenseName, @LicenseContentType, @LicenseData, @CnicDocName, @CnicDocContentType, @CnicDocData, @NtnDocName, @NtnDocContentType, @NtnDocData, @LogoDocName, @LogoDocContentType, @LogoDocData);" & vbCrLf &
                                        "SELECT CAST(SCOPE_IDENTITY() AS INT);"

                    BindCommonParameters(command, request)
                    BindDocumentParameters(command, "License", request.TradeLicenseDocument, includeWhenEmpty:=True)
                    BindDocumentParameters(command, "CnicDoc", request.CnicDocument, includeWhenEmpty:=True)
                    BindDocumentParameters(command, "NtnDoc", request.NtnDocument, includeWhenEmpty:=True)
                    BindDocumentParameters(command, "LogoDoc", request.LogoDocument, includeWhenEmpty:=True)

                    Dim newId = Convert.ToInt32(command.ExecuteScalar(), CultureInfo.InvariantCulture)

                    ' Persist optional default discount percent (safe when column exists).
                    If newId > 0 Then
                        Using updateCmd = connection.CreateCommand()
                            updateCmd.CommandText = "UPDATE dbo.TBL_DEALERS SET Default_Discount_Percentage = @Pct WHERE ID = @Id"
                            updateCmd.Parameters.AddWithValue("@Id", newId)
                            AddNullableDecimal(updateCmd, "@Pct", request.DefaultDiscountPercentage)
                            updateCmd.ExecuteNonQuery()
                        End Using
                    End If

                    Return newId
                End Using
            End Using
        End Function

        Public Sub UpdateDealer(request As DealerUpsertRequest)
            ValidateRequest(request, requireId:=True)

            Using connection = CreateConnection()
                Using command = connection.CreateCommand()
                    BindCommonParameters(command, request)
                    Dim assignments = BuildUpdateAssignments(command, request)

                    Dim builder As New StringBuilder()
                    builder.AppendLine("UPDATE dbo.TBL_DEALERS SET")
                    builder.Append("    ")
                    builder.AppendLine(String.Join("," & vbCrLf & "    ", assignments))
                    builder.AppendLine("WHERE ID = @Id;")

                    command.CommandText = builder.ToString()
                    command.Parameters.AddWithValue("@Id", request.DealerId)

                    Dim affected = command.ExecuteNonQuery()
                    If affected = 0 Then
                        Throw New InvalidOperationException("Dealer could not be updated because it no longer exists.")
                    End If
                End Using
            End Using
        End Sub

        Private Shared Function BuildUpdateAssignments(command As SqlCommand, request As DealerUpsertRequest) As List(Of String)
            Dim assignments As New List(Of String) From {
                "DealerName = @DealerName",
                "DealerID = @DealerID",
                "DealerOldID = @DealerOldID",
                "ContactPerson = @ContactPerson",
                "CNIC = @CNIC",
                "CNICExpiry = @CNICExpiry",
                "Email = @Email",
                "Email_customer = @CustomerEmail",
                "Address = @Address",
                "City = @City",
                "State = @State",
                "Country = @Country",
                "website = @Website",
                "Branch = @Branch",
                "Br_Code = @BranchCode",
                "SALES_PERSON = @SalesPerson",
                "SLSP_ID = @SalesPersonId",
                "Login = @Login",
                "Password = @Password",
                "TYPED = @TypeCode",
                "parentID = @ParentId",
                "Status = @Status",
                "SMS_ACT = @SmsAct",
                "APP_LOGIN = @AppLogin",
                "DEALER_INVST = @DealerInvestment",
                "CNO_D = @ContactNotes",
                "EM_D = @EmailNotes",
                "ST_R = @SalesTerritory",
                "IP_ADDRESS = @IpAddress",
                "CellNumber = @CellNumber",
                "CellNumbers = @AlternateNumbers",
                "Phone1 = @Phone1",
                "Phone2 = @Phone2",
                "Phone3 = @Phone3",
                "NTN = @NTN",
                "STN = @STN",
                "Default_Discount_Percentage = @DefaultDiscountPercentage"
            }

            If BindDocumentParameters(command, "License", request.TradeLicenseDocument, includeWhenEmpty:=False) Then
                assignments.Add("[Name] = @LicenseName")
                assignments.Add("ContentType = @LicenseContentType")
                assignments.Add("Data = @LicenseData")
            End If

            If BindDocumentParameters(command, "CnicDoc", request.CnicDocument, includeWhenEmpty:=False) Then
                assignments.Add("NameCNIC = @CnicDocName")
                assignments.Add("ContentTypeCNIC = @CnicDocContentType")
                assignments.Add("DataCNIC = @CnicDocData")
            End If

            If BindDocumentParameters(command, "NtnDoc", request.NtnDocument, includeWhenEmpty:=False) Then
                assignments.Add("NameNTN = @NtnDocName")
                assignments.Add("ContentTypeNTN = @NtnDocContentType")
                assignments.Add("DataNTN = @NtnDocData")
            End If

            If BindDocumentParameters(command, "LogoDoc", request.LogoDocument, includeWhenEmpty:=False) Then
                assignments.Add("NameLOGO = @LogoDocName")
                assignments.Add("ContentTypeLOGO = @LogoDocContentType")
                assignments.Add("DataLOGO = @LogoDocData")
            End If

            Return assignments
        End Function

        Private Shared Function MapDealerSummary(reader As SqlDataReader) As Dealer
            Dim dealer As New Dealer() With {
                .Id = reader.GetInt32(reader.GetOrdinal("ID")),
                .DealerName = ReadString(reader, "DealerName"),
                .DealerCode = ReadString(reader, "DealerID"),
                .ContactPerson = ReadString(reader, "ContactPerson"),
                .CellNumber = ReadString(reader, "CellNumber"),
                .City = ReadString(reader, "City"),
                .StatusActive = True
            }
            Return dealer
        End Function

        Private Shared Function MapDealerDetail(reader As SqlDataReader) As Dealer
            Dim dealer = MapDealerSummary(reader)
            dealer.DealerOldCode = ReadString(reader, "DealerOldID")
            dealer.Cnic = ReadString(reader, "CNIC")
            dealer.CnicExpiry = ReadNullableDate(reader, "CNICExpiry")
            dealer.Email = ReadString(reader, "Email")
            dealer.CustomerEmail = ReadString(reader, "CustomerEmail")
            dealer.Address = ReadString(reader, "Address")
            dealer.State = ReadString(reader, "State")
            dealer.Country = ReadString(reader, "Country")
            dealer.Website = ReadString(reader, "website")
            dealer.Branch = ReadString(reader, "Branch")
            dealer.BranchCode = ReadString(reader, "Br_Code")
            dealer.SalesPersonName = ReadString(reader, "SALES_PERSON")
            dealer.SalesPersonId = ReadNullableInt(reader, "SLSP_ID")
            dealer.LoginName = ReadString(reader, "Login")
            dealer.LoginPassword = ReadString(reader, "Password")
            dealer.TypeCode = ReadString(reader, "TYPED")
            dealer.ParentId = ReadNullableInt(reader, "parentID")
            dealer.StatusActive = ReadFlag(reader, "Status")
            dealer.SmsEnabled = ReadFlag(reader, "SMS_ACT")
            dealer.AppLoginEnabled = ReadFlag(reader, "APP_LOGIN")
            dealer.DealerInvestment = ReadString(reader, "DEALER_INVST")
            dealer.ContactNumberNotes = ReadString(reader, "ContactNumberNotes")
            dealer.EmailNotes = ReadString(reader, "EmailNotes")
            dealer.SalesTerritoryCode = ReadNullableInt(reader, "SalesTerritoryCode")
            dealer.IpAddress = ReadString(reader, "IP_ADDRESS")
            dealer.CreatedOn = ReadNullableDate(reader, "CDATED")
            dealer.AlternateNumbers = ReadString(reader, "AlternateNumbers")
            dealer.Phone1 = ReadString(reader, "Phone1")
            dealer.Phone2 = ReadString(reader, "Phone2")
            dealer.Phone3 = ReadString(reader, "Phone3")
            dealer.Ntn = ReadString(reader, "NTN")
            dealer.Stn = ReadString(reader, "STN")
            dealer.DefaultDiscountPercentage = ReadNullableDecimal(reader, "Default_Discount_Percentage")
            dealer.TradeLicenseDocument = BuildDocument(reader, "LicenseFileName", "LicenseContentType", "LicenseFileData")
            dealer.CnicDocument = BuildDocument(reader, "NameCNIC", "ContentTypeCNIC", "DataCNIC")
            dealer.NtnDocument = BuildDocument(reader, "NameNTN", "ContentTypeNTN", "DataNTN")
            dealer.LogoDocument = BuildDocument(reader, "NameLOGO", "ContentTypeLOGO", "DataLOGO")
            Return dealer
        End Function

        Private Shared Function BuildDocument(reader As SqlDataReader, nameColumn As String, typeColumn As String, dataColumn As String) As DealerDocument
            Dim fileName = ReadString(reader, nameColumn)
            Dim data = ReadBytes(reader, dataColumn)

            If String.IsNullOrWhiteSpace(fileName) AndAlso (data Is Nothing OrElse data.Length = 0) Then
                Return Nothing
            End If

            Dim document As New DealerDocument() With {
                .FileName = fileName,
                .ContentType = ReadString(reader, typeColumn),
                .Content = data
            }
            Return document
        End Function

        Private Shared Sub BindCommonParameters(command As SqlCommand, request As DealerUpsertRequest)
            command.Parameters.AddWithValue("@DealerName", request.DealerName)
            command.Parameters.AddWithValue("@DealerID", ToDbValue(request.DealerCode))
            command.Parameters.AddWithValue("@DealerOldID", ToDbValue(request.DealerOldCode))
            command.Parameters.AddWithValue("@ContactPerson", ToDbValue(request.ContactPerson))
            command.Parameters.AddWithValue("@CNIC", ToDbValue(request.Cnic))
            AddNullableDate(command, "@CNICExpiry", request.CnicExpiry)
            command.Parameters.AddWithValue("@Email", ToDbValue(request.Email))
            command.Parameters.AddWithValue("@CustomerEmail", ToDbValue(request.CustomerEmail))
            command.Parameters.AddWithValue("@Address", ToDbValue(request.Address))
            command.Parameters.AddWithValue("@City", ToDbValue(request.City))
            command.Parameters.AddWithValue("@State", ToDbValue(request.State))
            command.Parameters.AddWithValue("@Country", ToDbValue(request.Country))
            command.Parameters.AddWithValue("@Website", ToDbValue(request.Website))
            command.Parameters.AddWithValue("@Branch", ToDbValue(request.Branch))
            command.Parameters.AddWithValue("@BranchCode", ToDbValue(request.BranchCode))
            command.Parameters.AddWithValue("@SalesPerson", ToDbValue(request.SalesPersonName))
            AddNullableInt(command, "@SalesPersonId", request.SalesPersonId)
            command.Parameters.AddWithValue("@Login", ToDbValue(request.LoginName))
            command.Parameters.AddWithValue("@Password", ToDbValue(request.LoginPassword))
            command.Parameters.AddWithValue("@TypeCode", ToDbValue(request.TypeCode))
            AddNullableInt(command, "@ParentId", request.ParentId)
            command.Parameters.AddWithValue("@DealerInvestment", ToDbValue(request.DealerInvestment))
            command.Parameters.AddWithValue("@ContactNotes", ToDbValue(request.ContactNumberNotes))
            command.Parameters.AddWithValue("@EmailNotes", ToDbValue(request.EmailNotes))
            AddNullableInt(command, "@SalesTerritory", request.SalesTerritoryCode)
            command.Parameters.AddWithValue("@CellNumber", ToDbValue(request.CellNumber))
            command.Parameters.AddWithValue("@AlternateNumbers", ToDbValue(request.AlternateNumbers))
            command.Parameters.AddWithValue("@Phone1", ToDbValue(request.Phone1))
            command.Parameters.AddWithValue("@Phone2", ToDbValue(request.Phone2))
            command.Parameters.AddWithValue("@Phone3", ToDbValue(request.Phone3))
            command.Parameters.AddWithValue("@NTN", ToDbValue(request.Ntn))
            command.Parameters.AddWithValue("@STN", ToDbValue(request.Stn))
            AddNullableDecimal(command, "@DefaultDiscountPercentage", request.DefaultDiscountPercentage)
            AddFlag(command, "@Status", request.StatusActive)
            AddFlag(command, "@SmsAct", request.SmsEnabled)
            AddFlag(command, "@AppLogin", request.AppLoginEnabled)
            command.Parameters.AddWithValue("@IpAddress", ToDbValue(request.IpAddress))
        End Sub

        Private Shared Function BindDocumentParameters(command As SqlCommand, prefix As String, document As DealerDocument, includeWhenEmpty As Boolean) As Boolean
            Dim hasContent = document IsNot Nothing AndAlso document.HasContent

            If Not hasContent AndAlso Not includeWhenEmpty Then
                Return False
            End If

            Dim nameValue As Object = If(hasContent, ToDbValue(document.FileName), CType(DBNull.Value, Object))
            Dim typeValue As Object = If(hasContent, ToDbValue(document.ContentType), CType(DBNull.Value, Object))
            Dim dataValue As Object = If(hasContent, CType(document.Content, Object), CType(DBNull.Value, Object))

            command.Parameters.AddWithValue($"@{prefix}Name", nameValue)
            command.Parameters.AddWithValue($"@{prefix}ContentType", typeValue)
            Dim dataParameter = command.Parameters.Add($"@{prefix}Data", SqlDbType.VarBinary)
            dataParameter.Size = -1
            dataParameter.Value = dataValue

            Return hasContent
        End Function

        Private Shared Sub AddNullableInt(command As SqlCommand, name As String, value As Integer?)
            Dim parameter = command.Parameters.Add(name, SqlDbType.Int)
            If value.HasValue Then
                parameter.Value = value.Value
            Else
                parameter.Value = DBNull.Value
            End If
        End Sub

        Private Shared Sub AddNullableDate(command As SqlCommand, name As String, value As Date?)
            Dim parameter = command.Parameters.Add(name, SqlDbType.DateTime)
            If value.HasValue Then
                parameter.Value = value.Value
            Else
                parameter.Value = DBNull.Value
            End If
        End Sub

        Private Shared Sub AddNullableDecimal(command As SqlCommand, name As String, value As Decimal?)
            Dim parameter = command.Parameters.Add(name, SqlDbType.Decimal)
            parameter.Precision = 18
            parameter.Scale = 4
            If value.HasValue Then
                parameter.Value = value.Value
            Else
                parameter.Value = DBNull.Value
            End If
        End Sub

        Private Shared Sub AddFlag(command As SqlCommand, name As String, flag As Boolean)
            command.Parameters.AddWithValue(name, ToFlagValue(flag))
        End Sub

        Private Shared Function CreateWalkInDealer() As Dealer
            Return New Dealer() With {
                .Id = 0,
                .DealerName = "Walk-in Customer",
                .DealerCode = "WALKIN",
                .StatusActive = True
            }
        End Function

        Private Shared Sub ValidateRequest(request As DealerUpsertRequest, requireId As Boolean)
            If request Is Nothing Then
                Throw New ArgumentNullException(NameOf(request))
            End If

            request.DealerName = SafeTrim(request.DealerName)
            request.DealerCode = SafeTrim(request.DealerCode)

            If String.IsNullOrWhiteSpace(request.DealerName) Then
                Throw New ArgumentException("Dealer name is required.", NameOf(request.DealerName))
            End If

            If String.IsNullOrWhiteSpace(request.DealerCode) Then
                Throw New ArgumentException("Dealer code is required.", NameOf(request.DealerCode))
            End If

            If requireId AndAlso request.DealerId <= 0 Then
                Throw New ArgumentException("A valid dealer identifier is required for updates.", NameOf(request.DealerId))
            End If
        End Sub

        Private Shared Function SafeTrim(value As String) As String
            If String.IsNullOrWhiteSpace(value) Then
                Return String.Empty
            End If
            Return value.Trim()
        End Function

        Private Shared Function ToDbValue(value As String) As Object
            If String.IsNullOrWhiteSpace(value) Then
                Return DBNull.Value
            End If
            Return value.Trim()
        End Function

        Private Shared Function ToFlagValue(value As Boolean) As Integer
            Return If(value, 1, 0)
        End Function

        Private Shared Function ReadString(reader As SqlDataReader, column As String) As String
            Dim ordinal = reader.GetOrdinal(column)
            If reader.IsDBNull(ordinal) Then
                Return Nothing
            End If
            Return reader.GetString(ordinal)
        End Function

        Private Shared Function ReadNullableInt(reader As SqlDataReader, column As String) As Integer?
            Dim ordinal = reader.GetOrdinal(column)
            If reader.IsDBNull(ordinal) Then
                Return Nothing
            End If
            Return Convert.ToInt32(reader.GetValue(ordinal), CultureInfo.InvariantCulture)
        End Function

        Private Shared Function ReadNullableDate(reader As SqlDataReader, column As String) As Date?
            Dim ordinal = reader.GetOrdinal(column)
            If reader.IsDBNull(ordinal) Then
                Return Nothing
            End If
            Return reader.GetDateTime(ordinal)
        End Function

        Private Shared Function ReadNullableDecimal(reader As SqlDataReader, column As String) As Decimal?
            Dim ordinal = reader.GetOrdinal(column)
            If reader.IsDBNull(ordinal) Then
                Return Nothing
            End If
            Return Convert.ToDecimal(reader.GetValue(ordinal), CultureInfo.InvariantCulture)
        End Function

        Private Shared Function ReadFlag(reader As SqlDataReader, column As String) As Boolean
            Dim value = ReadNullableInt(reader, column)
            Return value.GetValueOrDefault() <> 0
        End Function

        Private Shared Function ReadBytes(reader As SqlDataReader, column As String) As Byte()
            Dim ordinal = reader.GetOrdinal(column)
            If reader.IsDBNull(ordinal) Then
                Return Nothing
            End If

            Dim length = reader.GetBytes(ordinal, 0, Nothing, 0, 0)
            If length = 0 Then
                Return Array.Empty(Of Byte)()
            End If

            Dim buffer = New Byte(length - 1) {}
            reader.GetBytes(ordinal, 0, buffer, 0, buffer.Length)
            Return buffer
        End Function
    End Class
End Namespace
