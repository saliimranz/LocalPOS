Imports System.Configuration
Imports System.Globalization
Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.UI.WebControls
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
            Return 0.05D
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

    Private Property DiscountValue As Decimal
        Get
            Return If(ViewState("DiscountValue") IsNot Nothing, CType(ViewState("DiscountValue"), Decimal), 0D)
        End Get
        Set(value As Decimal)
            ViewState("DiscountValue") = value
        End Set
    End Property

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        litTaxRate.Text = String.Format(CultureInfo.CurrentCulture, "{0:P0}", TaxRate)
        hfCurrencySymbol.Value = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol

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
            litCashChange.Text = (0D).ToString("C", CultureInfo.CurrentCulture)
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
        DiscountValue = discountAmount

        litSubtotal.Text = subtotal.ToString("C", CultureInfo.CurrentCulture)
        litTax.Text = taxAmount.ToString("C", CultureInfo.CurrentCulture)
        litTotal.Text = total.ToString("C", CultureInfo.CurrentCulture)
        hfBaseAmountDue.Value = total.ToString(CultureInfo.InvariantCulture)
        hfAmountDue.Value = hfBaseAmountDue.Value
        hfTaxableAmount.Value = taxable.ToString(CultureInfo.InvariantCulture)
        hfDefaultTaxPercent.Value = (TaxRate * 100D).ToString(CultureInfo.InvariantCulture)
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

    Private Function GetModalTaxPercent() As Decimal
        Dim defaultPercent = Math.Max(0D, Math.Min(100D, TaxRate * 100D))
        If txtModalTaxPercent Is Nothing Then
            Return defaultPercent
        End If

        Dim percent As Decimal
        If Decimal.TryParse(txtModalTaxPercent.Text, NumberStyles.Float, CultureInfo.InvariantCulture, percent) Then
            Return Math.Min(Math.Max(percent, 0D), 100D)
        End If

        If Not String.IsNullOrWhiteSpace(hfDefaultTaxPercent.Value) Then
            If Decimal.TryParse(hfDefaultTaxPercent.Value, NumberStyles.Float, CultureInfo.InvariantCulture, percent) Then
                Return Math.Min(Math.Max(percent, 0D), 100D)
            End If
        End If

        Return defaultPercent
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

    Protected Sub txtSearch_TextChanged(sender As Object, e As EventArgs)
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
        Dim isCorporateCustomer = IsCorporateCustomerSelected()
        hfIsCorporateCustomer.Value = If(isCorporateCustomer, "true", "false")
        pnlCorporatePayment.CssClass = If(isCorporateCustomer, "corporate-payment-options", "corporate-payment-options d-none")

        If Not isCorporateCustomer Then
            rblCorporatePaymentType.SelectedValue = "Full"
            txtCorporatePartialAmount.Text = String.Empty
        End If

        If String.IsNullOrWhiteSpace(rblPaymentMethod.SelectedValue) Then
            rblPaymentMethod.SelectedValue = "Cash"
        End If
    End Sub

    Private Function IsCorporateCustomerSelected() As Boolean
        Dim selectedValue = If(ddlCustomers.SelectedItem Is Not Nothing, ddlCustomers.SelectedValue, String.Empty)
        Return Not String.IsNullOrWhiteSpace(selectedValue) AndAlso Not selectedValue.Equals("0", StringComparison.OrdinalIgnoreCase)
    End Function

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
        ResetPaymentFormState()
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

        Dim subtotalText = SubtotalValue.ToString("C", CultureInfo.CurrentCulture)
        Dim discountText = DiscountValue.ToString("C", CultureInfo.CurrentCulture)
        Dim taxText = TaxValue.ToString("C", CultureInfo.CurrentCulture)
        Dim amountDueText = TotalValue.ToString("C", CultureInfo.CurrentCulture)

        litModalSubtotal.Text = subtotalText
        litModalDiscount.Text = discountText
        litModalTax.Text = taxText
        litModalTotal.Text = amountDueText
        litAmountDueHeader.Text = amountDueText
        litCashAmountDue.Text = amountDueText
        litCardAmountDue.Text = amountDueText
        ResetPaymentFormState()
        Dim defaultTaxPercentValue = (TaxRate * 100D).ToString(CultureInfo.InvariantCulture)
        hfDefaultTaxPercent.Value = defaultTaxPercentValue
        txtModalTaxPercent.Text = defaultTaxPercentValue
        Dim taxableBase = Math.Max(0D, SubtotalValue - DiscountValue)
        hfTaxableAmount.Value = taxableBase.ToString(CultureInfo.InvariantCulture)

        lblCheckoutError.Text = String.Empty
        hfBaseAmountDue.Value = TotalValue.ToString(CultureInfo.InvariantCulture)
        hfAmountDue.Value = hfBaseAmountDue.Value
        rblPaymentMethod.SelectedValue = "Cash"
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
            If String.IsNullOrWhiteSpace(method) Then
                lblCheckoutError.Text = "Select a payment method."
                Return
            End If

            Dim dealerId = If(String.IsNullOrWhiteSpace(ddlCustomers.SelectedValue), 0, Convert.ToInt32(ddlCustomers.SelectedValue, CultureInfo.InvariantCulture))
            Dim dealerName = If(ddlCustomers.SelectedItem IsNot Nothing, ddlCustomers.SelectedItem.Text, "Walk-in Customer")
            Dim isCorporateCustomer = IsCorporateCustomerSelected()
            Dim corporatePaymentType = If(isCorporateCustomer, rblCorporatePaymentType.SelectedValue, "Full")
            Dim taxableBase = Math.Max(0D, SubtotalValue - DiscountValue)
            Dim modalTaxPercent = GetModalTaxPercent()
            Dim recalculatedTax = Decimal.Round(taxableBase * (modalTaxPercent / 100D), 2, MidpointRounding.AwayFromZero)
            Dim recalculatedTotal = taxableBase + recalculatedTax
            TaxValue = recalculatedTax
            TotalValue = recalculatedTotal

            Dim partialAmount As Decimal? = Nothing
            If isCorporateCustomer AndAlso String.Equals(corporatePaymentType, "Partial", StringComparison.OrdinalIgnoreCase) Then
                Dim parsed As Decimal
                If Not Decimal.TryParse(txtCorporatePartialAmount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) OrElse parsed <= 0D Then
                    lblCheckoutError.Text = "Enter a valid partial amount."
                    Return
                End If
                If parsed > recalculatedTotal Then
                    lblCheckoutError.Text = "Partial amount cannot exceed total due."
                    Return
                End If
                partialAmount = parsed
            Else
                corporatePaymentType = "Full"
            End If

            Dim paymentAmount = If(partialAmount.HasValue, partialAmount.Value, recalculatedTotal)
            If paymentAmount <= 0D Then
                lblCheckoutError.Text = "Payment amount must be greater than zero."
                Return
            End If

            Dim request As New CheckoutRequest() With {
                .DealerId = dealerId,
                .DealerName = dealerName,
                .PaymentMethod = method,
                .PaymentAmount = paymentAmount,
                .PartialAmount = partialAmount,
                .CorporatePaymentType = corporatePaymentType,
                .TaxPercent = modalTaxPercent,
                .TaxAmount = recalculatedTax,
                .TotalDue = recalculatedTotal,
                .DiscountPercent = GetDiscountPercent(),
                .Subtotal = SubtotalValue,
                .CartItems = cart.Select(Function(ci) ci.Clone()).ToList(),
                .CreatedBy = lblCashierName.Text
            }

            Select Case method
                Case "Cash"
                    Dim cashReceived As Decimal
                    If Not Decimal.TryParse(txtCashReceived.Text, NumberStyles.Float, CultureInfo.InvariantCulture, cashReceived) OrElse cashReceived <= 0D Then
                        lblCheckoutError.Text = "Enter the cash amount received."
                        Return
                    End If
                    If cashReceived < paymentAmount Then
                        lblCheckoutError.Text = "Cash received cannot be less than the amount due."
                        Return
                    End If
                    request.CashReceived = cashReceived
                    request.CashChange = cashReceived - paymentAmount
                Case "Card"
                    Dim rrn = txtCardRrn.Text.Trim()
                    If String.IsNullOrWhiteSpace(rrn) Then
                        lblCheckoutError.Text = "Bank POS receipt number (RRN) is required."
                        Return
                    End If
                    Dim status = ddlCardStatus.SelectedValue
                    If Not status.Equals("Approved", StringComparison.OrdinalIgnoreCase) Then
                        lblCheckoutError.Text = "Only approved card transactions can be completed."
                        Return
                    End If
                    request.CardRrn = rrn
                    request.CardAuthCode = txtCardAuthCode.Text.Trim()
                    request.CardStatus = status
                Case Else
                    lblCheckoutError.Text = "Unsupported payment method selected."
                    Return
            End Select

            Dim result = _posService.CompleteCheckout(request)

            GetCart().Clear()
            ResetPaymentFormState()
            BindCart()
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HidePaymentModal", "PosUI.hidePaymentModal();", True)
            ShowCartMessage($"Order {result.OrderNumber} completed. Receipt {result.ReceiptNumber}.", True)
        Catch ex As Exception
            lblCheckoutError.Text = $"Checkout failed: {ex.Message}"
        End Try
    End Sub

    Private Sub ResetPaymentFormState()
        txtCashReceived.Text = String.Empty
        txtCardRrn.Text = String.Empty
        txtCardAuthCode.Text = String.Empty

        Dim approvedItem = ddlCardStatus.Items.FindByValue("Approved")
        If approvedItem IsNot Nothing Then
            ddlCardStatus.SelectedValue = "Approved"
        ElseIf ddlCardStatus.Items.Count > 0 Then
            ddlCardStatus.SelectedIndex = 0
        End If

        txtCorporatePartialAmount.Text = String.Empty
        rblCorporatePaymentType.SelectedValue = "Full"
        litCashChange.Text = (0D).ToString("C", CultureInfo.CurrentCulture)
        hfBaseAmountDue.Value = "0"
        hfAmountDue.Value = "0"
        Dim defaultTaxPercent = If(String.IsNullOrWhiteSpace(hfDefaultTaxPercent.Value), (TaxRate * 100D).ToString(CultureInfo.InvariantCulture), hfDefaultTaxPercent.Value)
        If txtModalTaxPercent IsNot Nothing Then
            txtModalTaxPercent.Text = defaultTaxPercent
        End If
    End Sub

    Private Sub ShowCartMessage(message As String, success As Boolean)
        lblCartMessage.Text = message
        lblCartMessage.CssClass = If(success, "text-success", "text-danger")
    End Sub
End Class