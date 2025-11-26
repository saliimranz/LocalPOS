Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Web
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class ManageProducts
    Inherits Page

    Private Const MediaFolderVirtual As String = "~/product media"
    Private Shared ReadOnly AllowedExtensions As String() = {".png", ".jpg", ".jpeg", ".gif", ".webp"}
    Private ReadOnly _posService As New PosService()

    <Serializable>
    Private Class SkuDraft
        Public Property SkuCode As String
        Public Property DisplayName As String
        Public Property Description As String
        Public Property RetailPrice As String
        Public Property TaxRate As String
        Public Property StockQuantity As String
        Public Property MinStockThreshold As String
        Public Property ImageUrl As String
    End Class

    Private Property SkuDrafts As List(Of SkuDraft)
        Get
            Dim drafts = TryCast(ViewState("SkuDrafts"), List(Of SkuDraft))
            If drafts Is Nothing OrElse drafts.Count = 0 Then
                drafts = New List(Of SkuDraft)() From {New SkuDraft()}
                ViewState("SkuDrafts") = drafts
            End If
            Return drafts
        End Get
        Set(value As List(Of SkuDraft))
            ViewState("SkuDrafts") = value
        End Set
    End Property

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        If Not IsPostBack Then
            BindSkuRepeater()
        End If
    End Sub

    Private Sub BindSkuRepeater()
        rptSkus.DataSource = SkuDrafts
        rptSkus.DataBind()
    End Sub

    Protected Sub btnAddSkuRow_Click(sender As Object, e As EventArgs) Handles btnAddSkuRow.Click
        Dim captureErrors = CaptureSkuDrafts()
        If captureErrors.Any() Then
            ShowErrors(captureErrors)
        Else
            HideErrors()
        End If

        Dim drafts = SkuDrafts
        drafts.Add(New SkuDraft())
        SkuDrafts = drafts
        BindSkuRepeater()
    End Sub

    Protected Sub btnSaveProduct_Click(sender As Object, e As EventArgs) Handles btnSaveProduct.Click
        Dim errors = CaptureSkuDrafts().ToList()
        Dim skuErrors As New List(Of String)()
        Dim skuRequests = BuildSkuRequests(skuErrors)

        errors.AddRange(skuErrors)

        If String.IsNullOrWhiteSpace(txtSparePartCode.Text) Then
            errors.Add("Spare part code is required.")
        End If

        If skuRequests.Count = 0 Then
            errors.Add("Add at least one SKU row with code, price, tax rate, and stock information.")
        End If

        If errors.Any() Then
            ShowErrors(errors)
            Return
        End If

        Dim request As New ProductCreateRequest() With {
            .SparePartCode = txtSparePartCode.Text.Trim(),
            .DisplayName = txtDisplayName.Text.Trim(),
            .Description = txtProductDescription.Text.Trim(),
            .Category = txtCategory.Text.Trim(),
            .Brand = txtBrand.Text.Trim(),
            .Skus = skuRequests
        }

        Try
            _posService.CreateProduct(request)
            ResetForm()
            ShowStatus("Product and SKUs saved successfully.", True)
        Catch ex As Exception
            ShowErrors(New List(Of String) From {$"Unable to save product: {ex.Message}"})
        End Try
    End Sub

    Private Function CaptureSkuDrafts() As IList(Of String)
        Dim errors As New List(Of String)()
        Dim drafts As New List(Of SkuDraft)()

        For Each item As RepeaterItem In rptSkus.Items
            If item.ItemType <> ListItemType.Item AndAlso item.ItemType <> ListItemType.AlternatingItem Then
                Continue For
            End If

            Dim draft As New SkuDraft() With {
                .SkuCode = GetText(item, "txtSkuCode"),
                .DisplayName = GetText(item, "txtSkuDisplayName"),
                .RetailPrice = GetText(item, "txtSkuPrice"),
                .TaxRate = GetText(item, "txtSkuTax"),
                .StockQuantity = GetText(item, "txtSkuStock"),
                .MinStockThreshold = GetText(item, "txtSkuThreshold"),
                .Description = GetText(item, "txtSkuDescription")
            }

            Dim uploader = TryCast(item.FindControl("fuSkuImage"), FileUpload)
            Dim storedValue = GetHiddenValue(item, "hfStoredImage")

            If uploader IsNot Nothing AndAlso uploader.HasFile Then
                Try
                    draft.ImageUrl = SaveImage(uploader)
                Catch ex As Exception
                    errors.Add(ex.Message)
                    draft.ImageUrl = storedValue
                End Try
            Else
                draft.ImageUrl = storedValue
            End If

            drafts.Add(draft)
        Next

        If drafts.Count = 0 Then
            drafts.Add(New SkuDraft())
        End If

        SkuDrafts = drafts
        Return errors
    End Function

    Private Function BuildSkuRequests(rowErrors As IList(Of String)) As List(Of ProductSkuRequest)
        Dim result As New List(Of ProductSkuRequest)()
        Dim index = 1

        For Each draft In SkuDrafts
            If IsDraftEmpty(draft) Then
                index += 1
                Continue For
            End If

            Dim hasRowErrors = False
            Dim skuCode = SafeTrim(draft.SkuCode)
            If String.IsNullOrWhiteSpace(skuCode) Then
                rowErrors.Add($"SKU row #{index}: SKU code is required.")
                hasRowErrors = True
            End If

            Dim retailPrice As Decimal
            If String.IsNullOrWhiteSpace(draft.RetailPrice) OrElse Not TryParseDecimal(draft.RetailPrice, retailPrice) Then
                rowErrors.Add($"SKU row #{index}: Enter a valid retail price.")
                hasRowErrors = True
            End If

            Dim taxPercent As Decimal
            If String.IsNullOrWhiteSpace(draft.TaxRate) OrElse Not TryParseDecimal(draft.TaxRate, taxPercent) Then
                rowErrors.Add($"SKU row #{index}: Enter a valid tax rate percentage.")
                hasRowErrors = True
            End If

            Dim stockQuantity As Integer
            If Not TryParseInteger(draft.StockQuantity, stockQuantity) Then
                rowErrors.Add($"SKU row #{index}: Stock quantity must be numeric.")
                hasRowErrors = True
            End If

            Dim threshold As Integer
            If Not TryParseInteger(draft.MinStockThreshold, threshold) Then
                rowErrors.Add($"SKU row #{index}: Min threshold must be numeric.")
                hasRowErrors = True
            End If

            If Not hasRowErrors Then
                Dim request As New ProductSkuRequest() With {
                    .SkuCode = skuCode,
                    .DisplayName = SafeTrim(draft.DisplayName),
                    .Description = SafeTrim(draft.Description),
                    .RetailPrice = Math.Max(0D, retailPrice),
                    .TaxRate = Math.Max(0D, taxPercent / 100D),
                    .StockQuantity = Math.Max(0, stockQuantity),
                    .MinStockThreshold = Math.Max(0, threshold),
                    .ImageUrl = SafeTrim(draft.ImageUrl)
                }
                result.Add(request)
            End If

            index += 1
        Next

        Return result
    End Function

    Private Shared Function TryParseDecimal(input As String, ByRef value As Decimal) As Boolean
        If Decimal.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, value) Then
            Return True
        End If
        Return Decimal.TryParse(input, NumberStyles.Float, CultureInfo.CurrentCulture, value)
    End Function

    Private Shared Function TryParseInteger(input As String, ByRef value As Integer) As Boolean
        If String.IsNullOrWhiteSpace(input) Then
            value = 0
            Return True
        End If
        Return Integer.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, value) _
            OrElse Integer.TryParse(input, NumberStyles.Integer, CultureInfo.CurrentCulture, value)
    End Function

    Private Shared Function SafeTrim(value As String) As String
        Return If(value, String.Empty).Trim()
    End Function

    Private Shared Function IsDraftEmpty(draft As SkuDraft) As Boolean
        If draft Is Nothing Then
            Return True
        End If

        Return String.IsNullOrWhiteSpace(draft.SkuCode) _
            AndAlso String.IsNullOrWhiteSpace(draft.DisplayName) _
            AndAlso String.IsNullOrWhiteSpace(draft.RetailPrice) _
            AndAlso String.IsNullOrWhiteSpace(draft.TaxRate) _
            AndAlso String.IsNullOrWhiteSpace(draft.StockQuantity) _
            AndAlso String.IsNullOrWhiteSpace(draft.MinStockThreshold) _
            AndAlso String.IsNullOrWhiteSpace(draft.Description)
    End Function

    Private Function SaveImage(uploader As FileUpload) As String
        Dim extension = Path.GetExtension(uploader.FileName)
        If String.IsNullOrWhiteSpace(extension) Then
            Throw New InvalidOperationException("Uploaded images must include a file extension.")
        End If

        extension = extension.ToLowerInvariant()
        If Not AllowedExtensions.Contains(extension) Then
            Throw New InvalidOperationException($"Images must be one of the following types: {String.Join(", ", AllowedExtensions)}.")
        End If

        EnsureMediaFolderExists()

        Dim baseName = Path.GetFileNameWithoutExtension(uploader.FileName)
        Dim sanitized = New String(baseName.Select(Function(ch) If(Char.IsLetterOrDigit(ch), ch, "-"c)).ToArray()).Trim("-"c)
        If String.IsNullOrWhiteSpace(sanitized) Then
            sanitized = "product"
        End If

        Dim uniqueName = $"{sanitized}_{DateTime.UtcNow:yyyyMMddHHmmssfff}{extension}"
        Dim physicalPath = Server.MapPath($"{MediaFolderVirtual}/{uniqueName}")

        uploader.SaveAs(physicalPath)
        Return $"{MediaFolderVirtual}/{uniqueName}"
    End Function

    Private Sub EnsureMediaFolderExists()
        Dim physicalPath = Server.MapPath(MediaFolderVirtual)
        If Not Directory.Exists(physicalPath) Then
            Directory.CreateDirectory(physicalPath)
        End If
    End Sub

    Protected Sub rptSkus_ItemDataBound(sender As Object, e As RepeaterItemEventArgs) Handles rptSkus.ItemDataBound
        If e.Item.ItemType <> ListItemType.Item AndAlso e.Item.ItemType <> ListItemType.AlternatingItem Then
            Return
        End If

        Dim draft = TryCast(e.Item.DataItem, SkuDraft)
        If draft Is Nothing Then
            Return
        End If

        SetText(e.Item, "txtSkuCode", draft.SkuCode)
        SetText(e.Item, "txtSkuDisplayName", draft.DisplayName)
        SetText(e.Item, "txtSkuPrice", draft.RetailPrice)
        SetText(e.Item, "txtSkuTax", draft.TaxRate)
        SetText(e.Item, "txtSkuStock", draft.StockQuantity)
        SetText(e.Item, "txtSkuThreshold", draft.MinStockThreshold)
        SetText(e.Item, "txtSkuDescription", draft.Description)

        Dim lit = TryCast(e.Item.FindControl("litImageName"), Literal)
        If lit IsNot Nothing Then
            If String.IsNullOrWhiteSpace(draft.ImageUrl) Then
                lit.Text = "No image selected yet."
            Else
                lit.Text = $"Current: {HttpUtility.HtmlEncode(Path.GetFileName(draft.ImageUrl))}"
            End If
        End If

        Dim hidden = TryCast(e.Item.FindControl("hfStoredImage"), HiddenField)
        If hidden IsNot Nothing Then
            hidden.Value = draft.ImageUrl
        End If
    End Sub

    Private Shared Function GetText(item As RepeaterItem, controlId As String) As String
        Dim control = TryCast(item.FindControl(controlId), TextBox)
        Return If(control Is Nothing, String.Empty, control.Text.Trim())
    End Function

    Private Shared Function GetHiddenValue(item As RepeaterItem, controlId As String) As String
        Dim control = TryCast(item.FindControl(controlId), HiddenField)
        Return If(control Is Nothing, String.Empty, control.Value)
    End Function

    Private Shared Sub SetText(item As RepeaterItem, controlId As String, value As String)
        Dim control = TryCast(item.FindControl(controlId), TextBox)
        If control IsNot Nothing Then
            control.Text = value
        End If
    End Sub

    Private Sub ResetForm()
        txtSparePartCode.Text = String.Empty
        txtDisplayName.Text = String.Empty
        txtCategory.Text = String.Empty
        txtBrand.Text = String.Empty
        txtProductDescription.Text = String.Empty

        SkuDrafts = New List(Of SkuDraft)() From {New SkuDraft()}
        BindSkuRepeater()
        HideErrors()
    End Sub

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
        pnlErrors.CssClass = "alert alert-danger"
        litErrors.Text = "<ul><li>" & String.Join("</li><li>", errors.Select(Function(err) HttpUtility.HtmlEncode(err))) & "</li></ul>"
        pnlStatus.Visible = False
        litStatus.Text = String.Empty
    End Sub

    Private Sub HideErrors()
        pnlErrors.Visible = False
        litErrors.Text = String.Empty
    End Sub
End Class
