Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.IO
Imports System.Linq
Imports System.Web
Imports System.Web.UI
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class ProductDetails
    Inherits Page

    Private Const MediaFolderVirtual As String = "~/product media"
    Private Shared ReadOnly AllowedExtensions As String() = {".png", ".jpg", ".jpeg", ".gif", ".webp"}
    Private ReadOnly _posService As New PosService()

    Protected Sub Page_Load(sender As Object, e As EventArgs) Handles Me.Load
        If Not IsPostBack Then
            LoadProduct()
        End If
    End Sub

    Private Sub LoadProduct()
        Dim productId = GetProductIdFromRequest()
        If productId <= 0 Then
            ShowNotFound("A valid product id is required.")
            Return
        End If

        Dim product = _posService.GetProduct(productId)
        If product Is Nothing Then
            ShowNotFound("We couldn't find that product. It may have been deleted.")
            Return
        End If

        pnlNotFound.Visible = False
        pnlDetails.Visible = True

        hfProductId.Value = product.Id.ToString(CultureInfo.InvariantCulture)
        litProductName.Text = HttpUtility.HtmlEncode(product.DisplayName)
        litSkuCode.Text = HttpUtility.HtmlEncode(product.SkuCode)
        litCategory.Text = HttpUtility.HtmlEncode(product.Category)
        litBrand.Text = HttpUtility.HtmlEncode(product.Brand)
        litStock.Text = product.StockQuantity.ToString(CultureInfo.InvariantCulture)
        litThreshold.Text = product.MinStockThreshold.ToString(CultureInfo.InvariantCulture)
        litUnitPrice.Text = product.UnitPrice.ToString("C", CultureInfo.CurrentCulture)
        litTax.Text = product.TaxRate.ToString("P2", CultureInfo.CurrentCulture)
        litProductDescription.Text = HttpUtility.HtmlEncode(product.Description)

        txtEditDisplayName.Text = product.DisplayName
        txtEditPrice.Text = product.UnitPrice.ToString(CultureInfo.InvariantCulture)
        txtEditTaxRate.Text = (product.TaxRate * 100D).ToString("0.##", CultureInfo.InvariantCulture)
        txtEditStock.Text = product.StockQuantity.ToString(CultureInfo.InvariantCulture)
        txtEditThreshold.Text = product.MinStockThreshold.ToString(CultureInfo.InvariantCulture)
        txtEditDescription.Text = product.Description

        imgProduct.ImageUrl = GetProductImage(product.ImageUrl)
        imgProduct.AlternateText = product.DisplayName
        litExistingImage.Text = If(String.IsNullOrWhiteSpace(product.ImageUrl),
                                   "Current image: placeholder",
                                   $"Current image: {HttpUtility.HtmlEncode(Path.GetFileName(product.ImageUrl))}")
    End Sub

    Protected Sub btnSaveChanges_Click(sender As Object, e As EventArgs) Handles btnSaveChanges.Click
        Dim productId = GetProductIdFromHidden()
        If productId <= 0 Then
            ShowStatus("Product id is missing. Reload the page and try again.", False)
            Return
        End If

        Dim errors As New List(Of String)()
        Dim displayName = txtEditDisplayName.Text.Trim()
        If String.IsNullOrWhiteSpace(displayName) Then
            errors.Add("Display name is required.")
        End If

        Dim price As Decimal
        If Not TryParseDecimal(txtEditPrice.Text, price) Then
            errors.Add("Enter a valid unit price.")
        End If

        Dim taxPercent As Decimal
        If Not TryParseDecimal(txtEditTaxRate.Text, taxPercent) Then
            errors.Add("Enter a valid tax rate percentage.")
        End If

        Dim stockQuantity As Integer
        If Not TryParseInteger(txtEditStock.Text, stockQuantity) Then
            errors.Add("Stock quantity must be numeric.")
        End If

        Dim threshold As Integer
        If Not TryParseInteger(txtEditThreshold.Text, threshold) Then
            errors.Add("Min threshold must be numeric.")
        End If

        Dim imageUrl As String = Nothing
        If fuEditImage.HasFile Then
            Try
                imageUrl = SaveImage(fuEditImage)
            Catch ex As Exception
                errors.Add(ex.Message)
            End Try
        End If

        If errors.Any() Then
            ShowErrorList(errors)
            Return
        End If

        Dim request As New ProductUpdateRequest() With {
            .ProductId = productId,
            .DisplayName = displayName,
            .Description = txtEditDescription.Text.Trim(),
            .RetailPrice = Math.Max(0D, price),
            .TaxRate = Math.Max(0D, taxPercent / 100D),
            .StockQuantity = Math.Max(0, stockQuantity),
            .MinStockThreshold = Math.Max(0, threshold),
            .ImageUrl = imageUrl
        }

        Try
            _posService.UpdateProduct(request)
            ShowStatus("Product details were updated.", True)
            LoadProduct()
        Catch ex As Exception
            ShowStatus($"Unable to update product: {ex.Message}", False)
        End Try
    End Sub

    Private Sub ShowNotFound(message As String)
        pnlDetails.Visible = False
        pnlNotFound.Visible = True
        pnlDetailStatus.Visible = True
        pnlDetailStatus.CssClass = "alert alert-danger"
        litDetailStatus.Text = HttpUtility.HtmlEncode(message)
    End Sub

    Private Sub ShowStatus(message As String, success As Boolean)
        pnlDetailStatus.Visible = True
        pnlDetailStatus.CssClass = If(success, "alert alert-success", "alert alert-danger")
        litDetailStatus.Text = HttpUtility.HtmlEncode(message)
    End Sub

    Private Sub ShowErrorList(errors As IEnumerable(Of String))
        pnlDetailStatus.Visible = True
        pnlDetailStatus.CssClass = "alert alert-danger"
        litDetailStatus.Text = "<ul><li>" & String.Join("</li><li>", errors.Select(Function(err) HttpUtility.HtmlEncode(err))) & "</li></ul>"
    End Sub

    Private Function GetProductIdFromRequest() As Integer
        Dim raw = Request.QueryString("id")
        Dim parsed As Integer
        If Integer.TryParse(raw, parsed) AndAlso parsed > 0 Then
            Return parsed
        End If
        Return GetProductIdFromHidden()
    End Function

    Private Function GetProductIdFromHidden() As Integer
        Dim parsed As Integer
        If Integer.TryParse(hfProductId.Value, parsed) AndAlso parsed > 0 Then
            Return parsed
        End If
        Return 0
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

    Private Function GetProductImage(path As String) As String
        If String.IsNullOrWhiteSpace(path) Then
            Return "https://via.placeholder.com/320x200.png?text=No+Image"
        End If
        If path.StartsWith("http", StringComparison.OrdinalIgnoreCase) Then
            Return path
        End If
        Return ResolveUrl(path)
    End Function
End Class
