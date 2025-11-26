Imports System.Configuration
Imports System.Globalization
Imports System.Collections.Generic
Imports System.Linq
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services


Public Class _Default
    Inherits Page

    Private Const CartSessionKey As String = "POS_CART"
    Private Const HeldCartSessionKey As String = "POS_HELD_CARTS"
    Private ReadOnly _posService As New PosService()

    Private ReadOnly Property TaxRate As Decimal
        Get
            Dim configured = ConfigurationManager.AppSettings("PosTaxRate")
            Dim parsed As Decimal
            If Decimal.TryParse(configured, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
                Return parsed
            End If
            Return 0.1D
        End Get
    End Property

    Private Property SubtotalValue As Decimal
        Get
            Return If(ViewState("Subtotal") IsNot Nothing, CType(ViewState("Subtotal"), Decimal), 0D)
        End Get
        Set(value As Decimal)
            ViewState("Subtotal") = value
        End Set
    End Property

    Private Property TaxValue As Decimal
        Get
            Return If(ViewState("Tax") IsNot Nothing, CType(ViewState("Tax"), Decimal), 0D)
        End Get
        Set(value As Decimal)
            ViewState("Tax") = value
        End Set
    End Property

    Private Property TotalValue As Decimal
        Get
            Return If(ViewState("Total") IsNot Nothing, CType(ViewState("Total"), Decimal), 0D)
        End Get
        Set(value As Decimal)
            ViewState("Total") = value
        End Set
    End Property

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        litTaxRate.Text = String.Format(CultureInfo.CurrentCulture, "{0:P0}", TaxRate)

        If Not IsPostBack Then
            lblCashierName.Text = ConfigurationManager.AppSettings("PosDefaultCashier")
            litDate.Text = DateTime.Now.ToString("MMMM dd, yyyy")
            litTime.Text = DateTime.Now.ToString("HH:mm:ss")
            txtDiscount.Text = "0"

            BindCustomers()
            BindCategories()
            BindProducts()
            BindCart()
            UpdatePaymentAvailability()
        End If
    End Sub

    Private Sub BindCustomers()
        Dim customers = _posService.GetDealers()
        ddlCustomers.DataSource = customers
        ddlCustomers.DataTextField = "DealerName"
        ddlCustomers.DataValueField = "Id"
        ddlCustomers.DataBind()
    End Sub

    Private Sub BindCategories()
        Dim categories = _posService.GetCategories()
        rptCategories.DataSource = categories
        rptCategories.DataBind()
        UpdateCategoryFilterState()
    End Sub

    Private Sub BindProducts()
        Dim category = hfSelectedCategory.Value
        Dim searchTerm = txtSearch.Text
        Dim products = _posService.SearchProducts(searchTerm, If(String.IsNullOrWhiteSpace(category), Nothing, category))
        lblCatalogEmpty.Visible = products Is Nothing OrElse products.Count = 0
        rptProducts.DataSource = products
        rptProducts.DataBind()
        UpdateCategoryFilterState()
    End Sub

    Private Sub UpdateCategoryFilterState()
        btnAllCategories.CssClass = If(String.IsNullOrWhiteSpace(hfSelectedCategory.Value), "category-pill active", "category-pill")
    End Sub

    Private Sub BindCart()
        Dim cart = GetCart()
        rptCart.DataSource = cart
        rptCart.DataBind()
        pnlEmptyCart.Visible = cart.Count = 0
        litCartCount.Text = cart.Count.ToString(CultureInfo.InvariantCulture)

        Dim subtotal = cart.Sum(Function(item) item.LineTotal)
        Dim discountPercent = GetDiscountPercent()
        Dim discountAmount = subtotal * (discountPercent / 100D)
        Dim taxable = Math.Max(0D, subtotal - discountAmount)
        Dim taxAmount = taxable * TaxRate
        Dim total = taxable + taxAmount

        SubtotalValue = subtotal
        TaxValue = taxAmount
        TotalValue = total

        litSubtotal.Text = subtotal.ToString("C", CultureInfo.CurrentCulture)
        litTax.Text = taxAmount.ToString("C", CultureInfo.CurrentCulture)
        litTotal.Text = total.ToString("C", CultureInfo.CurrentCulture)
    End Sub

    Private Function GetCart() As List(Of CartItem)
        Dim cart = TryCast(Session(CartSessionKey), List(Of CartItem))
        If cart Is Nothing Then
            cart = New List(Of CartItem)()
            Session(CartSessionKey) = cart
        End If
        Return cart
    End Function

    Private Function GetDiscountPercent() As Decimal
        Dim percent As Decimal
        If Decimal.TryParse(txtDiscount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, percent) Then
            percent = Math.Min(Math.Max(percent, 0D), 100D)
        Else
            percent = 0D
        End If
        txtDiscount.Text = percent.ToString(CultureInfo.InvariantCulture)
        Return percent
    End Function

    Protected Function GetCategoryCss(category As String) As String
        Dim selected = hfSelectedCategory.Value
        Dim isActive = Not String.IsNullOrWhiteSpace(category) AndAlso category.Equals(selected, StringComparison.OrdinalIgnoreCase)
        Return If(isActive, "category-pill active", "category-pill")
    End Function

    Protected Function GetProductImage(imagePath As Object) As String
        If imagePath Is Nothing OrElse String.IsNullOrWhiteSpace(imagePath.ToString()) Then
            Return "https://via.placeholder.com/320x200.png?text=No+Image"
        End If
        Return imagePath.ToString()
    End Function

    Protected Sub btnSearch_Click(sender As Object, e As EventArgs)
        BindProducts()
    End Sub

    Protected Sub btnAllCategories_Click(sender As Object, e As EventArgs)
        hfSelectedCategory.Value = String.Empty
        BindProducts()
    End Sub

    Protected Sub Category_Command(sender As Object, e As CommandEventArgs)
        If e.CommandName = "FilterCategory" Then
            hfSelectedCategory.Value = e.CommandArgument.ToString()
            BindProducts()
        End If
    End Sub

    Protected Sub Product_Command(source As Object, e As CommandEventArgs)
        If e.CommandName = "AddToCart" Then
            Dim productId = Convert.ToInt32(e.CommandArgument, CultureInfo.InvariantCulture)
            AddProductToCart(productId)
        End If
    End Sub

    Private Sub AddProductToCart(productId As Integer)
        Dim product = _posService.GetProduct(productId)
        If product Is Nothing Then
            ShowCartMessage("Product was not found.", False)
            Return
        End If

        Dim cart = GetCart()
        Dim existing = cart.FirstOrDefault(Function(item) item.ProductId = product.Id)

        If existing Is Nothing Then
            cart.Add(New CartItem() With {
                .ProductId = product.Id,
                .SkuCode = product.SkuCode,
                .Name = product.DisplayName,
                .UnitPrice = product.UnitPrice,
                .Quantity = 1,
                .TaxRate = product.TaxRate,
                .Thumbnail = product.ImageUrl
            })
        Else
            If product.StockQuantity > 0 AndAlso existing.Quantity >= product.StockQuantity Then
                ShowCartMessage("Cannot add more than available stock.", False)
                Return
            End If
            existing.Quantity += 1
        End If

        ShowCartMessage($"{product.DisplayName} added to cart.", True)
        BindCart()
    End Sub

    Protected Sub rptCart_ItemCommand(source As Object, e As RepeaterCommandEventArgs)
        Dim productId = Convert.ToInt32(e.CommandArgument, CultureInfo.InvariantCulture)
        Select Case e.CommandName
            Case "Increase"
                ChangeQuantity(productId, 1)
            Case "Decrease"
                ChangeQuantity(productId, -1)
            Case "Remove"
                RemoveItem(productId)
        End Select
        BindCart()
    End Sub

    Private Sub ChangeQuantity(productId As Integer, delta As Integer)
        Dim cart = GetCart()
        Dim item = cart.FirstOrDefault(Function(ci) ci.ProductId = productId)
        If item Is Nothing Then Return

        item.Quantity += delta
        If item.Quantity <= 0 Then
            cart.Remove(item)
        End If
    End Sub

    Private Sub RemoveItem(productId As Integer)
        Dim cart = GetCart()
        cart.RemoveAll(Function(ci) ci.ProductId = productId)
    End Sub

    Protected Sub txtDiscount_TextChanged(sender As Object, e As EventArgs)
        BindCart()
    End Sub

    Protected Sub ddlCustomers_SelectedIndexChanged(sender As Object, e As EventArgs)
        UpdatePaymentAvailability()
    End Sub

    Private Sub UpdatePaymentAvailability()
        Dim isDealer = ddlCustomers.SelectedValue IsNot Nothing AndAlso Not ddlCustomers.SelectedValue.Equals("0", StringComparison.OrdinalIgnoreCase)
        Dim credit = rblPaymentMethod.Items.FindByValue("Credit")
        Dim partial_v = rblPaymentMethod.Items.FindByValue("Partial")
        If credit IsNot Nothing Then credit.Enabled = isDealer
        If partial_v IsNot Nothing Then partial_v.Enabled = isDealer

        If Not isDealer AndAlso (rblPaymentMethod.SelectedValue = "Credit" OrElse rblPaymentMethod.SelectedValue = "Partial") Then
            rblPaymentMethod.SelectedValue = "Cash"
        End If
    End Sub

    Protected Sub btnClearCart_Click(sender As Object, e As EventArgs)
        GetCart().Clear()
        BindCart()
    End Sub

    Protected Sub btnHold_Click(sender As Object, e As EventArgs)
        Dim cart = GetCart()
        If cart.Count = 0 Then
            ShowCartMessage("Cannot place an empty cart on hold.", False)
            Return
        End If

        Dim held = TryCast(Session(HeldCartSessionKey), List(Of List(Of CartItem)))
        If held Is Nothing Then
            held = New List(Of List(Of CartItem))()
        End If
        held.Add(cart.Select(Function(ci) ci.Clone()).ToList())
        Session(HeldCartSessionKey) = held

        cart.Clear()
        BindCart()
        ShowCartMessage($"Cart held successfully. Total held carts: {held.Count}.", True)
    End Sub

    Protected Sub btnNewSale_Click(sender As Object, e As EventArgs)
        txtDiscount.Text = "0"
        txtPartialAmount.Text = String.Empty
        GetCart().Clear()
        BindCart()
        ShowCartMessage("New sale started.", True)
    End Sub

    Protected Sub btnCheckout_Click(sender As Object, e As EventArgs)
        Dim cart = GetCart()
        If cart.Count = 0 Then
            ShowCartMessage("Add items to cart before checking out.", False)
            Return
        End If

        litModalTotal.Text = TotalValue.ToString("C", CultureInfo.CurrentCulture)
        lblCheckoutError.Text = String.Empty
        UpdatePaymentAvailability()
        ScriptManager.RegisterStartupScript(Me, Me.GetType(), "ShowPaymentModal", "PosUI.showPaymentModal();", True)
    End Sub

    Protected Sub btnCompletePayment_Click(sender As Object, e As EventArgs)
        Try
            Dim cart = GetCart()
            If cart.Count = 0 Then
                lblCheckoutError.Text = "Cart is empty."
                Return
            End If

            Dim method = rblPaymentMethod.SelectedValue
            Dim dealerId = If(String.IsNullOrWhiteSpace(ddlCustomers.SelectedValue), 0, Convert.ToInt32(ddlCustomers.SelectedValue, CultureInfo.InvariantCulture))
            If (method = "Credit" OrElse method = "Partial") AndAlso dealerId = 0 Then
                lblCheckoutError.Text = "Select a registered customer for credit or partial payments."
                Return
            End If

            Dim partialAmount As Decimal? = Nothing
            If method = "Partial" Then
                Dim parsed As Decimal
                If Not Decimal.TryParse(txtPartialAmount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) OrElse parsed <= 0 Then
                    lblCheckoutError.Text = "Enter a valid partial amount."
                    Return
                End If
                If parsed > TotalValue Then
                    lblCheckoutError.Text = "Partial amount cannot exceed total due."
                    Return
                End If
                partialAmount = parsed
            End If

            Dim dealerName = ddlCustomers.SelectedItem.Text
            Dim request As New CheckoutRequest() With {
                .DealerId = dealerId,
                .DealerName = dealerName,
                .PaymentMethod = method,
                .PartialAmount = partialAmount,
                .DiscountPercent = GetDiscountPercent(),
                .Subtotal = SubtotalValue,
                .TaxAmount = TaxValue,
                .TotalDue = TotalValue,
                .CartItems = GetCart().Select(Function(ci) ci.Clone()).ToList(),
                .CreatedBy = lblCashierName.Text
            }

            Dim result = _posService.CompleteCheckout(request)

            GetCart().Clear()
            txtPartialAmount.Text = String.Empty
            BindCart()
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HidePaymentModal", "PosUI.hidePaymentModal();", True)
            ShowCartMessage($"Order {result.OrderNumber} completed. Receipt {result.ReceiptNumber}.", True)
        Catch ex As Exception
            lblCheckoutError.Text = $"Checkout failed: {ex.Message}"
        End Try
    End Sub

    Private Sub ShowCartMessage(message As String, success As Boolean)
        lblCartMessage.Text = message
        lblCartMessage.CssClass = If(success, "text-success", "text-danger")
    End Sub
End Class