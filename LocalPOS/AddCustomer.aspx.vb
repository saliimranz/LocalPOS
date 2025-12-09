Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class AddCustomer
    Inherits Page

    Private Const CustomerMessageSessionKey As String = "CustomerSuccessMessage"
    Private ReadOnly _posService As New PosService()

    Private ReadOnly Property DealerId As Integer
        Get
            Dim raw = Request.QueryString("id")
            Dim parsed As Integer
            If Integer.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) AndAlso parsed > 0 Then
                Return parsed
            End If
            Return 0
        End Get
    End Property

    Private Property ReturnUrl As String
        Get
            Return TryCast(ViewState("ReturnUrl"), String)
        End Get
        Set(value As String)
            ViewState("ReturnUrl") = value
        End Set
    End Property

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        If Not IsPostBack Then
            ReturnUrl = SanitizeReturnUrl(Request.QueryString("returnUrl"))
            hfDealerId.Value = DealerId.ToString(CultureInfo.InvariantCulture)
            hfReturnUrl.Value = ReturnUrl
            SetupNavigation()
            If DealerId > 0 Then
                LoadDealer()
            Else
                SetDocumentStatusPlaceholders()
            End If
        End If
    End Sub

    Private Sub SetupNavigation()
        Dim isEdit = DealerId > 0
        litFormTitle.Text = If(isEdit, "Edit customer", "Add customer")
        litFormSubtitle.Text = If(isEdit, "Update account details, compliance info, and customer-facing credentials.", "Capture every detail required to serve and bill the customer from POS.")
        btnSaveCustomer.Text = If(isEdit, "Save changes", "Create customer")

        Dim backUrl = GetRedirectUrl(If(DealerId > 0, DealerId, 0))
        lnkBackToCustomers.NavigateUrl = ResolveClientUrl(backUrl)
        lnkBackToCustomers.Text = If(String.IsNullOrWhiteSpace(ReturnUrl), "Back to profile", "Back")
        lnkCancel.NavigateUrl = lnkBackToCustomers.NavigateUrl
    End Sub

    Private Function SanitizeReturnUrl(raw As String) As String
        If String.IsNullOrWhiteSpace(raw) Then
            Return Nothing
        End If
        Dim decoded = HttpUtility.UrlDecode(raw)
        If String.IsNullOrWhiteSpace(decoded) Then
            Return Nothing
        End If
        If decoded.StartsWith("~/", StringComparison.OrdinalIgnoreCase) OrElse decoded.StartsWith("/", StringComparison.OrdinalIgnoreCase) Then
            Return decoded
        End If
        Return Nothing
    End Function

    Private Sub LoadDealer()
        Dim dealer = _posService.GetDealer(DealerId)
        If dealer Is Nothing Then
            ShowErrors(New List(Of String) From {"Customer was not found."})
            Return
        End If

        txtDealerName.Text = dealer.DealerName
        txtDealerCode.Text = dealer.DealerCode
        txtDealerOldCode.Text = dealer.DealerOldCode
        txtContactPerson.Text = dealer.ContactPerson
        txtSalesPerson.Text = dealer.SalesPersonName
        txtSalesPersonId.Text = dealer.SalesPersonId?.ToString(CultureInfo.InvariantCulture)
        txtTypeCode.Text = dealer.TypeCode
        txtParentId.Text = dealer.ParentId?.ToString(CultureInfo.InvariantCulture)
        txtBranch.Text = dealer.Branch
        txtBranchCode.Text = dealer.BranchCode
        txtDealerInvestment.Text = dealer.DealerInvestment
        txtSalesTerritory.Text = dealer.SalesTerritoryCode?.ToString(CultureInfo.InvariantCulture)
        chkActive.Checked = dealer.StatusActive
        chkSmsAlerts.Checked = dealer.SmsEnabled
        chkAppLogin.Checked = dealer.AppLoginEnabled

        txtAddress.Text = dealer.Address
        txtCity.Text = dealer.City
        txtState.Text = dealer.State
        txtCountry.Text = dealer.Country
        txtWebsite.Text = dealer.Website
        txtEmail.Text = dealer.Email
        txtEmailCustomer.Text = dealer.CustomerEmail
        txtPhonePrimary.Text = dealer.CellNumber
        txtPhoneAlt1.Text = dealer.Phone1
        txtPhoneAlt2.Text = dealer.Phone2
        txtPhoneAlt3.Text = dealer.Phone3
        txtAltNumbers.Text = dealer.AlternateNumbers
        txtContactNotes.Text = dealer.ContactNumberNotes
        txtEmailNotes.Text = dealer.EmailNotes

        txtCnic.Text = dealer.Cnic
        txtCnicExpiry.Text = If(dealer.CnicExpiry.HasValue, dealer.CnicExpiry.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), String.Empty)
        txtNtn.Text = dealer.Ntn
        txtStn.Text = dealer.Stn
        txtLogin.Text = dealer.LoginName
        txtPassword.Text = dealer.LoginPassword

        SetDocumentStatus(litTradeLicenseStatus, dealer.TradeLicenseDocument)
        SetDocumentStatus(litCnicStatus, dealer.CnicDocument)
        SetDocumentStatus(litNtnStatus, dealer.NtnDocument)
        SetDocumentStatus(litLogoStatus, dealer.LogoDocument)
    End Sub

    Private Sub SetDocumentStatusPlaceholders()
        SetDocumentStatus(litTradeLicenseStatus, Nothing)
        SetDocumentStatus(litCnicStatus, Nothing)
        SetDocumentStatus(litNtnStatus, Nothing)
        SetDocumentStatus(litLogoStatus, Nothing)
    End Sub

    Private Sub SetDocumentStatus(target As Literal, document As DealerDocument)
        If target Is Nothing Then
            Return
        End If

        If document Is Nothing OrElse Not document.HasContent Then
            target.Text = "No file uploaded yet."
        Else
            target.Text = $"Current file: {HttpUtility.HtmlEncode(document.FileName)}"
        End If
    End Sub

    Protected Sub btnSaveCustomer_Click(sender As Object, e As EventArgs) Handles btnSaveCustomer.Click
        Dim errors = ValidateForm()
        If errors.Count > 0 Then
            ShowErrors(errors)
            Return
        End If

        Dim request = BuildRequest()
        Dim isEdit = DealerId > 0

        Try
            Dim savedId = DealerId
            If isEdit Then
                request.DealerId = savedId
                _posService.UpdateDealer(request)
            Else
                savedId = _posService.CreateDealer(request)
            End If

            Dim successMessage = If(isEdit, "Customer details updated successfully.", "Customer created successfully.")
            ShowStatus(successMessage, True)
            QueueSuccessMessage(successMessage, savedId)

            Dim targetUrl = GetRedirectUrl(savedId)
            Dim script = $"setTimeout(function() {{ window.location = '{ResolveClientUrl(targetUrl)}'; }}, 1500);"
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "RedirectAfterCustomerSave", script, True)
        Catch ex As Exception
            ShowStatus($"Unable to save customer: {ex.Message}", False)
        End Try
    End Sub

    Private Function ValidateForm() As IList(Of String)
        Dim errors As New List(Of String)()

        If String.IsNullOrWhiteSpace(txtDealerName.Text) Then
            errors.Add("Customer name is required.")
        End If
        If String.IsNullOrWhiteSpace(txtDealerCode.Text) Then
            errors.Add("Customer code is required.")
        End If

        ValidateIntegerField(txtParentId.Text, "Parent account ID", errors)
        ValidateIntegerField(txtSalesPersonId.Text, "Sales person ID", errors)
        ValidateIntegerField(txtSalesTerritory.Text, "Sales territory code", errors)

        Return errors
    End Function

    Private Sub ValidateIntegerField(input As String, fieldName As String, errors As IList(Of String))
        If String.IsNullOrWhiteSpace(input) Then
            Return
        End If
        Dim parsed As Integer
        If Not Integer.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then
            errors.Add($"{fieldName} must be numeric.")
        End If
    End Sub

    Private Function BuildRequest() As DealerUpsertRequest
        Dim request As New DealerUpsertRequest() With {
            .DealerId = DealerId,
            .DealerName = txtDealerName.Text,
            .DealerCode = txtDealerCode.Text,
            .DealerOldCode = txtDealerOldCode.Text,
            .ContactPerson = txtContactPerson.Text,
            .SalesPersonName = txtSalesPerson.Text,
            .SalesPersonId = ParseNullableInt(txtSalesPersonId.Text),
            .TypeCode = txtTypeCode.Text,
            .ParentId = ParseNullableInt(txtParentId.Text),
            .Branch = txtBranch.Text,
            .BranchCode = txtBranchCode.Text,
            .DealerInvestment = txtDealerInvestment.Text,
            .SalesTerritoryCode = ParseNullableInt(txtSalesTerritory.Text),
            .StatusActive = chkActive.Checked,
            .SmsEnabled = chkSmsAlerts.Checked,
            .AppLoginEnabled = chkAppLogin.Checked,
            .Address = txtAddress.Text,
            .City = txtCity.Text,
            .State = txtState.Text,
            .Country = txtCountry.Text,
            .Website = txtWebsite.Text,
            .Email = txtEmail.Text,
            .CustomerEmail = txtEmailCustomer.Text,
            .CellNumber = txtPhonePrimary.Text,
            .Phone1 = txtPhoneAlt1.Text,
            .Phone2 = txtPhoneAlt2.Text,
            .Phone3 = txtPhoneAlt3.Text,
            .AlternateNumbers = txtAltNumbers.Text,
            .ContactNumberNotes = txtContactNotes.Text,
            .EmailNotes = txtEmailNotes.Text,
            .Cnic = txtCnic.Text,
            .CnicExpiry = ParseNullableDate(txtCnicExpiry.Text),
            .Ntn = txtNtn.Text,
            .Stn = txtStn.Text,
            .LoginName = txtLogin.Text,
            .LoginPassword = txtPassword.Text,
            .TradeLicenseDocument = BuildDocument(fuTradeLicense),
            .CnicDocument = BuildDocument(fuCnic),
            .NtnDocument = BuildDocument(fuNtn),
            .LogoDocument = BuildDocument(fuLogo),
            .IpAddress = GetClientIp()
        }
        Return request
    End Function

    Private Function ParseNullableInt(input As String) As Integer?
        If String.IsNullOrWhiteSpace(input) Then
            Return Nothing
        End If
        Dim parsed As Integer
        If Integer.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then
            Return parsed
        End If
        Return Nothing
    End Function

    Private Function ParseNullableDate(input As String) As Date?
        If String.IsNullOrWhiteSpace(input) Then
            Return Nothing
        End If
        Dim parsed As Date
        If Date.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, parsed) Then
            Return parsed
        End If
        Return Nothing
    End Function

    Private Function BuildDocument(uploader As FileUpload) As DealerDocument
        If uploader Is Nothing OrElse Not uploader.HasFile Then
            Return Nothing
        End If

        Return New DealerDocument() With {
            .FileName = uploader.FileName,
            .ContentType = uploader.PostedFile.ContentType,
            .Content = uploader.FileBytes
        }
    End Function

    Private Function GetClientIp() As String
        Dim ip = Request.ServerVariables("HTTP_X_FORWARDED_FOR")
        If Not String.IsNullOrWhiteSpace(ip) Then
            Dim segments = ip.Split({","c}, StringSplitOptions.RemoveEmptyEntries)
            If segments.Length > 0 Then
                Return segments(0).Trim()
            End If
        End If
        Return Request.UserHostAddress
    End Function

    Private Sub QueueSuccessMessage(message As String, dealerId As Integer)
        If String.IsNullOrWhiteSpace(message) OrElse dealerId <= 0 Then
            Return
        End If

        Dim redirect = GetRedirectUrl(dealerId)
        If redirect.IndexOf("CustomerProfile.aspx", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Session(CustomerMessageSessionKey) = message
        End If
    End Sub

    Private Function GetRedirectUrl(savedDealerId As Integer) As String
        Dim target = ReturnUrl
        If String.IsNullOrWhiteSpace(target) Then
            If savedDealerId > 0 Then
                Return $"~/CustomerProfile.aspx?customerId={savedDealerId}"
            End If
            Return "~/CustomerProfile.aspx"
        End If

        If savedDealerId > 0 AndAlso target.IndexOf("customerId=", StringComparison.OrdinalIgnoreCase) < 0 AndAlso target.IndexOf("CustomerProfile.aspx", StringComparison.OrdinalIgnoreCase) >= 0 Then
            Dim separator = If(target.Contains("?"), "&", "?")
            Return $"{target}{separator}customerId={savedDealerId}"
        End If

        Return target
    End Function

    Private Sub ShowStatus(message As String, success As Boolean)
        pnlStatus.Visible = True
        pnlStatus.CssClass = If(success, "alert alert-success", "alert alert-danger")
        litStatus.Text = HttpUtility.HtmlEncode(message)
        HideErrors()
    End Sub

    Private Sub ShowErrors(errors As IList(Of String))
        If errors Is Nothing OrElse errors.Count = 0 Then
            HideErrors()
            Return
        End If

        pnlErrors.Visible = True
        Dim encoded = New List(Of String)()
        For Each item In errors
            encoded.Add(HttpUtility.HtmlEncode(item))
        Next
        litErrors.Text = "<ul><li>" & String.Join("</li><li>", encoded) & "</li></ul>"
        pnlStatus.Visible = False
        litStatus.Text = String.Empty
    End Sub

    Private Sub HideErrors()
        pnlErrors.Visible = False
        litErrors.Text = String.Empty
    End Sub
End Class
