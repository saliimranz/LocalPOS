Imports System.Configuration
Imports System.Globalization
Imports System.Collections.Generic
Imports System.Linq
Imports System.Web.UI.WebControls
Imports System.Web
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services


Public Class _Default
    Inherits Page

    Private Const CartSessionKey As String = "POS_CART"
    Private Const DiscountModePercent As String = "Percent"
    Private Const DiscountModeAmount As String = "Amount"
    Private Const DiscountScopeItem As String = "ITEM"
    Private Const DiscountScopeSubtotal As String = "SUBTOTAL"
    Private Const DiscountValueTypePercent As String = "PERCENT"
    Private Const DiscountValueTypeAmount As String = "AMOUNT"
    Private Const PosCustomersRefreshKey As String = "PosCustomersNeedsRefresh"
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

    Private Property DiscountPercentValue As Decimal
        Get
            Return If(ViewState("DiscountPercentValue") IsNot Nothing, CType(ViewState("DiscountPercentValue"), Decimal), 0D)
        End Get
        Set(value As Decimal)
            ViewState("DiscountPercentValue") = value
        End Set
    End Property

    Private Property ActiveHeldSaleId As Integer?
        Get
            If hfActiveHeldSaleId Is Nothing Then
                Return Nothing
            End If

            Dim raw = hfActiveHeldSaleId.Value
            If String.IsNullOrWhiteSpace(raw) Then
                Return Nothing
            End If

            Dim parsed As Integer
            If Integer.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) Then
                Return parsed
            End If
            Return Nothing
        End Get
        Set(value As Integer?)
            If hfActiveHeldSaleId Is Nothing Then
                Return
            End If
            hfActiveHeldSaleId.Value = If(value.HasValue, value.Value.ToString(CultureInfo.InvariantCulture), String.Empty)
        End Set
    End Property

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        litTaxRate.Text = String.Format(CultureInfo.CurrentCulture, "{0:P0}", TaxRate)
        hfCurrencySymbol.Value = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol
        Dim requestedCustomerId = ConsumeNewCustomerId(Not IsPostBack)
        Dim shouldRefreshCustomers = requestedCustomerId > 0

        If Not IsPostBack Then
            lblCashierName.Text = ConfigurationManager.AppSettings("PosDefaultCashier")
            litDate.Text = DateTime.Now.ToString("MMMM dd, yyyy")
            litTime.Text = DateTime.Now.ToString("HH:mm:ss")
            txtDiscount.Text = "0"
            If ddlDiscountMode IsNot Nothing Then
                ddlDiscountMode.SelectedValue = DiscountModePercent
            End If

            BindCustomers()
            BindCategories()
            BindProducts()
            BindCart()
            UpdatePaymentAvailability()
            litCashChange.Text = (0D).ToString("C", CultureInfo.CurrentCulture)
        ElseIf shouldRefreshCustomers Then
            BindCustomers()
        End If

        If requestedCustomerId > 0 Then
            SetSelectedCustomer(requestedCustomerId)
            ' Customer default discount is a base price adjustment (Approach 2).
            ' Ensure prices in the cart are re-derived from ListUnitPrice (no compounding).
            ApplyCustomerDefaultPricingToCart()
            BindProducts()
            BindCart()
        End If

        UpdateCustomerProfileButtonState()
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
        ' Backward compatibility for existing sessions / legacy held sales:
        ' If ListUnitPrice isn't set yet, treat the current UnitPrice as the baseline to avoid compounding.
        Dim cappedMessageShown As Boolean = False
        For Each ci In cart
            If ci Is Nothing Then Continue For
            If ci.ListUnitPrice <= 0D Then
                ci.ListUnitPrice = Math.Max(0D, ci.UnitPrice)
            End If
            ' Keep manual AMOUNT item discounts within the current base.
            If Not cappedMessageShown AndAlso CapAmountItemDiscountIfNeeded(ci, Nothing) Then
                ShowCartMessage($"Item discount for {ci.Name} was capped to {ci.ItemDiscountValue.ToString("C", CultureInfo.CurrentCulture)} because it exceeded the item price.", False)
                cappedMessageShown = True
            Else
                CapAmountItemDiscountIfNeeded(ci, Nothing)
            End If
        Next
        rptCart.DataSource = cart
        rptCart.DataBind()
        pnlEmptyCart.Visible = cart.Count = 0
        litCartCount.Text = cart.Count.ToString(CultureInfo.InvariantCulture)

        Dim discountIntents = BuildAllDiscountIntents(cart)
        Dim pricing = PricingEngine.Calculate(cart, TaxRate * 100D, discountIntents)

        Dim subtotal = pricing.SubtotalGross
        Dim totalDiscount = pricing.TotalDiscount
        Dim taxable = pricing.TaxableSubtotal
        Dim taxAmount = pricing.VatTotal
        Dim total = pricing.TotalDue

        SubtotalValue = subtotal
        TaxValue = taxAmount
        TotalValue = total
        DiscountValue = totalDiscount
        DiscountPercentValue = pricing.EffectiveDiscountPercent

        If litSubtotalBeforeDiscount IsNot Nothing Then
            litSubtotalBeforeDiscount.Text = subtotal.ToString("C", CultureInfo.CurrentCulture)
        End If
        If litDiscountAmount IsNot Nothing Then
            Dim discountDisplay = If(totalDiscount <= 0D, (0D).ToString("C", CultureInfo.CurrentCulture), totalDiscount.ToString("C", CultureInfo.CurrentCulture))
            litDiscountAmount.Text = If(totalDiscount <= 0D, discountDisplay, "-" & discountDisplay)
        End If
        litSubtotal.Text = taxable.ToString("C", CultureInfo.CurrentCulture)
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

    Private Sub BindHeldBillsList()
        Dim heldBills = _posService.GetHeldSales()
        rptHeldBills.DataSource = heldBills
        rptHeldBills.DataBind()
        pnlHeldBillsEmpty.Visible = heldBills Is Nothing OrElse heldBills.Count = 0
    End Sub

    Private Function GetDiscountPercent() As Decimal
        Return DiscountPercentValue
    End Function

    Private Function GetSelectedDiscountMode() As String
        If ddlDiscountMode Is Nothing OrElse String.IsNullOrWhiteSpace(ddlDiscountMode.SelectedValue) Then
            Return DiscountModePercent
        End If

        Dim selected = ddlDiscountMode.SelectedValue
        If selected.Equals(DiscountModeAmount, StringComparison.OrdinalIgnoreCase) Then
            Return DiscountModeAmount
        End If

        Return DiscountModePercent
    End Function

    Private Function GetDiscountInputValue() As Decimal
        If txtDiscount Is Nothing Then
            Return 0D
        End If
        Dim value As Decimal
        If Decimal.TryParse(txtDiscount.Text, NumberStyles.Float, CultureInfo.InvariantCulture, value) Then
            Return Math.Max(0D, value)
        End If
        Return 0D
    End Function

    Private Function BuildSubtotalDiscountIntents(subtotal As Decimal) As IList(Of CheckoutDiscount)
        Dim discounts As New List(Of CheckoutDiscount)()
        Dim inputValue = GetDiscountInputValue()
        If inputValue <= 0D OrElse subtotal <= 0D Then
            Return discounts
        End If

        Dim mode = GetSelectedDiscountMode()
        Dim intent As New CheckoutDiscount() With {
            .Scope = DiscountScopeSubtotal,
            .Source = "Manual",
            .Reference = String.Empty,
            .Description = "Cart discount",
            .Priority = 0,
            .IsStackable = True
        }

        If mode.Equals(DiscountModeAmount, StringComparison.OrdinalIgnoreCase) Then
            intent.ValueType = DiscountValueTypeAmount
            intent.Value = Math.Min(inputValue, subtotal)
        Else
            intent.ValueType = DiscountValueTypePercent
            intent.Value = Math.Min(inputValue, 100D)
        End If

        If intent.Value > 0D Then
            discounts.Add(intent)
        End If
        Return discounts
    End Function

    Private Function BuildItemDiscountIntents(cart As IList(Of CartItem)) As IList(Of CheckoutDiscount)
        Dim discounts As New List(Of CheckoutDiscount)()
        If cart Is Nothing OrElse cart.Count = 0 Then
            Return discounts
        End If

        For Each item In cart
            If item Is Nothing OrElse item.ProductId <= 0 Then
                Continue For
            End If
            Dim rawValue = Math.Max(0D, item.ItemDiscountValue)
            If rawValue <= 0D Then
                Continue For
            End If

            Dim mode = If(String.IsNullOrWhiteSpace(item.ItemDiscountMode), DiscountModePercent, item.ItemDiscountMode)
            Dim intent As New CheckoutDiscount() With {
                .Scope = DiscountScopeItem,
                .TargetProductId = item.ProductId,
                .Source = "Manual",
                .Reference = String.Empty,
                .Description = $"Item discount ({item.Name})",
                .Priority = 0,
                .IsStackable = True
            }

            If mode.Equals(DiscountModeAmount, StringComparison.OrdinalIgnoreCase) Then
                intent.ValueType = DiscountValueTypeAmount
                intent.Value = rawValue
            Else
                intent.ValueType = DiscountValueTypePercent
                intent.Value = Math.Min(rawValue, 100D)
            End If

            discounts.Add(intent)
        Next

        Return discounts
    End Function

    Private Function BuildAllDiscountIntents(cart As IList(Of CartItem)) As IList(Of CheckoutDiscount)
        Dim discounts As New List(Of CheckoutDiscount)()
        If cart IsNot Nothing AndAlso cart.Count > 0 Then
            discounts.AddRange(BuildItemDiscountIntents(cart))
        End If

        ' NOTE: subtotal discount applies after item discounts; engine handles ordering.
        Dim subtotalBaseGuess = If(cart IsNot Nothing, cart.Sum(Function(i) i.LineTotal), 0D)
        discounts.AddRange(BuildSubtotalDiscountIntents(subtotalBaseGuess))
        Return discounts
    End Function

    Private Function CapAmountItemDiscountIfNeeded(item As CartItem, Optional input As TextBox = Nothing) As Boolean
        If item Is Nothing Then
            Return False
        End If
        If String.IsNullOrWhiteSpace(item.ItemDiscountMode) OrElse Not item.ItemDiscountMode.Equals(DiscountModeAmount, StringComparison.OrdinalIgnoreCase) Then
            Return False
        End If

        Dim maxAllowed = Decimal.Round(Math.Max(0D, item.LineTotal), 2, MidpointRounding.AwayFromZero)
        If item.ItemDiscountValue <= maxAllowed Then
            Return False
        End If

        item.ItemDiscountValue = maxAllowed
        If input IsNot Nothing Then
            input.Text = maxAllowed.ToString(CultureInfo.InvariantCulture)
        End If
        Return True
    End Function

    Protected Sub ddlItemDiscountMode_SelectedIndexChanged(sender As Object, e As EventArgs)
        Dim dropDown = TryCast(sender, DropDownList)
        If dropDown Is Nothing Then
            Return
        End If
        Dim container = TryCast(dropDown.NamingContainer, RepeaterItem)
        If container Is Nothing Then
            Return
        End If
        Dim productField = TryCast(container.FindControl("hfCartProductId"), HiddenField)
        If productField Is Nothing Then
            Return
        End If

        Dim productId As Integer
        If Not Integer.TryParse(productField.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, productId) Then
            Return
        End If

        Dim cart = GetCart()
        Dim item = cart.FirstOrDefault(Function(ci) ci.ProductId = productId)
        If item Is Nothing Then
            Return
        End If

        item.ItemDiscountMode = If(String.IsNullOrWhiteSpace(dropDown.SelectedValue), DiscountModePercent, dropDown.SelectedValue)
        Dim valueBox = TryCast(container.FindControl("txtItemDiscountValue"), TextBox)
        If CapAmountItemDiscountIfNeeded(item, valueBox) Then
            ShowCartMessage($"Item discount for {item.Name} was capped to {item.ItemDiscountValue.ToString("C", CultureInfo.CurrentCulture)} because it exceeded the item price.", False)
        End If
        BindCart()
    End Sub

    Protected Sub txtItemDiscountValue_TextChanged(sender As Object, e As EventArgs)
        Dim input = TryCast(sender, TextBox)
        If input Is Nothing Then
            Return
        End If
        Dim container = TryCast(input.NamingContainer, RepeaterItem)
        If container Is Nothing Then
            Return
        End If
        Dim productField = TryCast(container.FindControl("hfCartProductId"), HiddenField)
        If productField Is Nothing Then
            Return
        End If

        Dim productId As Integer
        If Not Integer.TryParse(productField.Value, NumberStyles.Integer, CultureInfo.InvariantCulture, productId) Then
            Return
        End If

        Dim parsed As Decimal
        If Not Decimal.TryParse(input.Text, NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
            parsed = 0D
        End If
        parsed = Math.Max(0D, parsed)

        Dim cart = GetCart()
        Dim item = cart.FirstOrDefault(Function(ci) ci.ProductId = productId)
        If item Is Nothing Then
            Return
        End If

        item.ItemDiscountValue = parsed
        If CapAmountItemDiscountIfNeeded(item, input) Then
            ShowCartMessage($"Item discount for {item.Name} was capped to {item.ItemDiscountValue.ToString("C", CultureInfo.CurrentCulture)} because it exceeded the item price.", False)
        End If
        BindCart()
    End Sub

    Private Function CalculateDiscountAmount(subtotal As Decimal, ByRef effectivePercent As Decimal) As Decimal
        If txtDiscount Is Nothing Then
            effectivePercent = 0D
            Return 0D
        End If
        Dim inputValue = GetDiscountInputValue()
        Dim mode = GetSelectedDiscountMode()
        Dim discountAmount As Decimal

        If mode.Equals(DiscountModeAmount, StringComparison.OrdinalIgnoreCase) Then
            discountAmount = Math.Min(inputValue, subtotal)
            If subtotal > 0D AndAlso discountAmount > 0D Then
                effectivePercent = Decimal.Round((discountAmount / subtotal) * 100D, 4, MidpointRounding.AwayFromZero)
            Else
                effectivePercent = 0D
            End If
            txtDiscount.Text = discountAmount.ToString(CultureInfo.InvariantCulture)
        Else
            effectivePercent = Math.Min(inputValue, 100D)
            discountAmount = subtotal * (effectivePercent / 100D)
            txtDiscount.Text = effectivePercent.ToString(CultureInfo.InvariantCulture)
        End If

        Return discountAmount
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

        Dim path = imagePath.ToString()
        If path.StartsWith("http", StringComparison.OrdinalIgnoreCase) Then
            Return path
        End If

        Return ResolveUrl(path)
    End Function

    Protected Function GetProductDetailsUrl(productId As Object) As String
        If productId Is Nothing Then
            Return ResolveUrl("~/ProductDetails.aspx")
        End If

        Dim parsed As Integer
        If Integer.TryParse(productId.ToString(), parsed) AndAlso parsed > 0 Then
            Return ResolveUrl($"~/ProductDetails.aspx?id={parsed}")
        End If

        Return ResolveUrl("~/ProductDetails.aspx")
    End Function

    Protected Function IsLowStock(stockObj As Object, thresholdObj As Object) As Boolean
        Dim stock = ConvertToInt(stockObj)
        Dim threshold = ConvertToInt(thresholdObj)
        If threshold <= 0 Then
            Return False
        End If
        Return stock <= threshold
    End Function

    Protected Function GetLowStockText(stockObj As Object) As String
        Dim stock = ConvertToInt(stockObj)
        stock = Math.Max(0, stock)
        Return $"Low stock - {stock} left"
    End Function

    Protected Function IsOutOfStock(stockObj As Object) As Boolean
        Return ConvertToInt(stockObj) <= 0
    End Function

    Protected Function GetAddToCartText(stockObj As Object) As String
        Return If(IsOutOfStock(stockObj), "Out of stock", "Add to cart")
    End Function

    Protected Function GetAddToCartCss(stockObj As Object) As String
        Const baseClass = "btn btn-light btn-add-to-cart"
        Return If(IsOutOfStock(stockObj), $"{baseClass} disabled", baseClass)
    End Function

    Private Shared Function ConvertToInt(value As Object) As Integer
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return 0
        End If

        Dim parsed As Integer
        If Integer.TryParse(value.ToString(), parsed) Then
            Return parsed
        End If
        Return 0
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

        If product.StockQuantity <= 0 Then
            ShowCartMessage($"{product.DisplayName} is out of stock.", False)
            Return
        End If

        Dim cart = GetCart()
        Dim existing = cart.FirstOrDefault(Function(item) item.ProductId = product.Id)
        Dim customerPct = GetSelectedCustomerDefaultDiscountPercent()
        Dim listUnit = Decimal.Round(Math.Max(0D, product.UnitPrice), 2, MidpointRounding.AwayFromZero)
        Dim effectiveUnit = ApplyPercentDiscount(listUnit, customerPct)

        If existing Is Nothing Then
            cart.Add(New CartItem() With {
                .ProductId = product.Id,
                .SkuCode = product.SkuCode,
                .Name = product.DisplayName,
                .ListUnitPrice = listUnit,
                .CustomerDefaultDiscountPercentApplied = customerPct,
                .UnitPrice = effectiveUnit,
                .Quantity = 1,
                .TaxRate = product.TaxRate,
                .Thumbnail = product.ImageUrl
            })
        Else
            If existing.Quantity >= product.StockQuantity Then
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

        If delta > 0 Then
            Dim product = _posService.GetProduct(productId)
            If product Is Nothing Then
                ShowCartMessage("Product was not found.", False)
                Return
            End If

            If product.StockQuantity <= 0 Then
                ShowCartMessage($"{item.Name} is out of stock.", False)
                Return
            End If

            Dim desiredQuantity = item.Quantity + delta
            If desiredQuantity > product.StockQuantity Then
                ShowCartMessage("Cannot exceed available stock.", False)
                Return
            End If
        End If

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

    Protected Sub ddlDiscountMode_SelectedIndexChanged(sender As Object, e As EventArgs)
        BindCart()
    End Sub

    Protected Sub ddlCustomers_SelectedIndexChanged(sender As Object, e As EventArgs)
        UpdatePaymentAvailability()
        UpdateCustomerProfileButtonState()
        ' Requirement: selecting a customer reloads catalog and re-prices cart using customer default discount.
        ApplyCustomerDefaultPricingToCart()
        BindProducts()
        BindCart()
    End Sub

    Private Sub ApplyCustomerDefaultPricingToCart()
        Dim cart = GetCart()
        If cart Is Nothing OrElse cart.Count = 0 Then
            Return
        End If

        Dim pct = GetSelectedCustomerDefaultDiscountPercent()
        For Each item In cart
            If item Is Nothing Then
                Continue For
            End If

            ' Preserve the list/original price baseline so customer switching doesn't compound discounts.
            If item.ListUnitPrice <= 0D Then
                item.ListUnitPrice = Math.Max(0D, item.UnitPrice)
            End If

            item.CustomerDefaultDiscountPercentApplied = pct
            item.UnitPrice = ApplyPercentDiscount(item.ListUnitPrice, pct)
        Next
    End Sub

    Private Function GetSelectedCustomerDefaultDiscountPercent() As Decimal
        If ddlCustomers Is Nothing OrElse String.IsNullOrWhiteSpace(ddlCustomers.SelectedValue) Then
            Return 0D
        End If

        Dim dealerId As Integer
        If Not Integer.TryParse(ddlCustomers.SelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, dealerId) OrElse dealerId <= 0 Then
            Return 0D
        End If

        Dim dealer = _posService.GetDealer(dealerId)
        If dealer Is Nothing OrElse Not dealer.DefaultDiscountPercentage.HasValue Then
            Return 0D
        End If

        Return ClampPercent(dealer.DefaultDiscountPercentage.Value)
    End Function

    Private Shared Function ClampPercent(value As Decimal) As Decimal
        If value < 0D Then Return 0D
        If value > 100D Then Return 100D
        Return value
    End Function

    Private Shared Function ApplyPercentDiscount(amount As Decimal, percent As Decimal) As Decimal
        Dim safe = Decimal.Round(Math.Max(0D, amount), 2, MidpointRounding.AwayFromZero)
        Dim pct = ClampPercent(percent)
        Dim discounted = safe * (1D - (pct / 100D))
        Return Decimal.Round(Math.Max(0D, discounted), 2, MidpointRounding.AwayFromZero)
    End Function

    Protected Sub btnViewCustomerProfile_Click(sender As Object, e As EventArgs)
        Dim selectedId As Integer
        If Not Integer.TryParse(ddlCustomers.SelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, selectedId) OrElse selectedId <= 0 Then
            ShowCartMessage("Select a saved customer to open their profile.", False)
            Return
        End If

        Response.Redirect($"~/CustomerProfile.aspx?customerId={selectedId}", False)
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

    Private Sub UpdateCustomerProfileButtonState()
        If btnViewCustomerProfile Is Nothing Then
            Return
        End If
        btnViewCustomerProfile.Visible = Not IsWalkInCustomerSelected()
    End Sub

    Private Function IsWalkInCustomerSelected() As Boolean
        If ddlCustomers.SelectedItem Is Nothing Then
            Return True
        End If

        Dim selectedId As Integer
        If Integer.TryParse(ddlCustomers.SelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, selectedId) Then
            Return selectedId = 0
        End If

        Return True
    End Function

    Private Function IsCorporateCustomerSelected() As Boolean
        If ddlCustomers.SelectedItem Is Nothing Then
            Return False
        End If

        Dim selectedValue As String = ddlCustomers.SelectedValue.ToString()

        Return Not String.IsNullOrWhiteSpace(selectedValue) _
        AndAlso Not selectedValue.Equals("0", StringComparison.OrdinalIgnoreCase)
    End Function

    Private Sub SetSelectedCustomer(dealerId As Integer)
        If ddlCustomers Is Nothing OrElse ddlCustomers.Items.Count = 0 Then
            Return
        End If

        Dim targetValue = dealerId.ToString(CultureInfo.InvariantCulture)
        Dim targetItem = ddlCustomers.Items.FindByValue(targetValue)

        If targetItem IsNot Nothing Then
            ddlCustomers.ClearSelection()
            targetItem.Selected = True
        Else
            Dim walkIn = ddlCustomers.Items.FindByValue("0")
            If walkIn IsNot Nothing Then
                ddlCustomers.ClearSelection()
                walkIn.Selected = True
            Else
                ddlCustomers.SelectedIndex = 0
            End If
        End If

        UpdatePaymentAvailability()
        UpdateCustomerProfileButtonState()
    End Sub

    Private Function ConsumeNewCustomerId(allowQueryParameter As Boolean) As Integer
        Dim requestedId As Integer
        If allowQueryParameter Then
            Dim rawQuery = Request.QueryString("customerId")
            If Integer.TryParse(rawQuery, NumberStyles.Integer, CultureInfo.InvariantCulture, requestedId) AndAlso requestedId >= 0 Then
                Session(PosCustomersRefreshKey) = Nothing
                Return requestedId
            End If
        End If

        Dim sessionValue = Session(PosCustomersRefreshKey)
        If sessionValue IsNot Nothing Then
            Try
                Dim parsed As Integer
                If Integer.TryParse(sessionValue.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, parsed) AndAlso parsed >= 0 Then
                    Return parsed
                End If
            Finally
                Session(PosCustomersRefreshKey) = Nothing
            End Try
        End If

        Return 0
    End Function


    Protected Sub btnClearCart_Click(sender As Object, e As EventArgs)
        GetCart().Clear()
        ActiveHeldSaleId = Nothing
        BindCart()
    End Sub

    Protected Sub btnHold_Click(sender As Object, e As EventArgs)
        Dim cart = GetCart()
        If cart.Count = 0 Then
            ShowCartMessage("Cannot place an empty cart on hold.", False)
            Return
        End If

        BindCart()
        Dim itemCount = cart.Sum(Function(ci) ci.Quantity)
        litHoldSummaryItems.Text = itemCount.ToString(CultureInfo.InvariantCulture)
        litHoldSummaryTotal.Text = TotalValue.ToString("C", CultureInfo.CurrentCulture)
        ScriptManager.RegisterStartupScript(Me, Me.GetType(), "ShowHoldConfirmModal", "PosUI.showHoldConfirm();", True)
    End Sub

    Protected Sub btnConfirmHold_Click(sender As Object, e As EventArgs)
        Dim cart = GetCart()
        If cart.Count = 0 Then
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HideHoldConfirmModal", "PosUI.hideHoldConfirm();", True)
            ShowCartMessage("Cart is empty. Nothing to hold.", False)
            Return
        End If

        BindCart()
        Dim dealerId As Integer
        If ddlCustomers.SelectedItem IsNot Nothing Then
            Integer.TryParse(ddlCustomers.SelectedValue, NumberStyles.Integer, CultureInfo.InvariantCulture, dealerId)
        End If
        Dim dealerName = If(ddlCustomers.SelectedItem IsNot Nothing, ddlCustomers.SelectedItem.Text, "Walk-in Customer")

        Dim request As New HoldSaleRequest() With {
            .HeldSaleId = ActiveHeldSaleId,
            .DealerId = Math.Max(0, dealerId),
            .DealerName = dealerName,
            .DiscountPercent = GetDiscountPercent(),
            .DiscountAmount = DiscountValue,
            .TaxPercent = TaxRate * 100D,
            .TaxAmount = TaxValue,
            .Subtotal = SubtotalValue,
            .TotalAmount = TotalValue,
            .Items = cart.Select(Function(ci) ci.Clone()).ToList(),
            .CreatedBy = lblCashierName.Text,
            .Discounts = BuildAllDiscountIntents(cart)
        }

        _posService.HoldSale(request)
        cart.Clear()
        ActiveHeldSaleId = Nothing
        txtDiscount.Text = "0"
        If ddlDiscountMode IsNot Nothing Then
            ddlDiscountMode.SelectedValue = DiscountModePercent
        End If
        ResetPaymentFormState()
        BindCart()
        ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HideHoldConfirmModal", "PosUI.hideHoldConfirm();", True)
        ShowCartMessage("Bill held successfully. You can resume it from Held bills.", True)
    End Sub

    Protected Sub btnNewSale_Click(sender As Object, e As EventArgs)
        txtDiscount.Text = "0"
        If ddlDiscountMode IsNot Nothing Then
            ddlDiscountMode.SelectedValue = DiscountModePercent
        End If
        ResetPaymentFormState()
        GetCart().Clear()
        ActiveHeldSaleId = Nothing
        BindCart()
        ShowCartMessage("New sale started.", True)
    End Sub

    Protected Sub btnHeldBills_Click(sender As Object, e As EventArgs)
        BindHeldBillsList()
        ScriptManager.RegisterStartupScript(Me, Me.GetType(), "ShowHeldBillsModal", "PosUI.showHeldBills();", True)
    End Sub

    Protected Sub btnLogout_Click(sender As Object, e As EventArgs)
        AuthManager.SignOut(Context)
        If Context IsNot Nothing AndAlso Context.Session IsNot Nothing Then
            Context.Session.Clear()
            Context.Session.Abandon()
        End If
        Response.Redirect("~/Login.aspx", False)
        Context.ApplicationInstance.CompleteRequest()
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
                .CreatedBy = lblCashierName.Text,
                .Discounts = BuildAllDiscountIntents(cart)
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
            If ActiveHeldSaleId.HasValue Then
                _posService.DeleteHeldSale(ActiveHeldSaleId.Value)
                ActiveHeldSaleId = Nothing
            End If
            Dim receiptUrl = BuildReceiptUrl(result.OrderId)
            If Not String.IsNullOrWhiteSpace(receiptUrl) Then
                result.ReceiptFilePath = receiptUrl
                TriggerReceiptDownload(result.OrderId)
            End If

            GetCart().Clear()
            ResetPaymentFormState()
            BindCart()
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HidePaymentModal", "PosUI.hidePaymentModal();", True)
            Dim confirmation = $"Order {result.OrderNumber} completed. Receipt {result.ReceiptNumber}."
            ShowCartMessage(confirmation, True)
            Dim salesPageUrl = ResolveClientUrl("~/SalesHistory.aspx")
            If Not String.IsNullOrWhiteSpace(salesPageUrl) Then
                Dim openSalesScript = String.Format(CultureInfo.InvariantCulture, "window.open('{0}', '_blank');", salesPageUrl)
                ScriptManager.RegisterStartupScript(Me, Me.GetType(), "OpenSalesTab", openSalesScript, True)
            End If
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
        If hfReceiptDownloadUrl IsNot Nothing Then
            hfReceiptDownloadUrl.Value = String.Empty
        End If
        Dim defaultTaxPercent = If(String.IsNullOrWhiteSpace(hfDefaultTaxPercent.Value), (TaxRate * 100D).ToString(CultureInfo.InvariantCulture), hfDefaultTaxPercent.Value)
        If txtModalTaxPercent IsNot Nothing Then
            txtModalTaxPercent.Text = defaultTaxPercent
        End If
    End Sub

    Private Sub ShowCartMessage(message As String, success As Boolean)
        lblCartMessage.Text = message
        lblCartMessage.CssClass = If(success, "text-success", "text-danger")
    End Sub

    Protected Sub rptHeldBills_ItemCommand(source As Object, e As RepeaterCommandEventArgs)
        Dim heldSaleId As Integer
        If e.CommandArgument Is Nothing OrElse Not Integer.TryParse(e.CommandArgument.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, heldSaleId) Then
            Return
        End If

        Select Case e.CommandName
            Case "Resume"
                RestoreHeldSale(heldSaleId)
            Case "Delete"
                _posService.DeleteHeldSale(heldSaleId)
                If ActiveHeldSaleId.HasValue AndAlso ActiveHeldSaleId.Value = heldSaleId Then
                    ActiveHeldSaleId = Nothing
                End If
                BindHeldBillsList()
                ShowCartMessage("Held bill deleted.", True)
                ScriptManager.RegisterStartupScript(Me, Me.GetType(), "ShowHeldBillsModal", "PosUI.showHeldBills();", True)
        End Select
    End Sub

    Private Sub RestoreHeldSale(heldSaleId As Integer)
        Dim detail = _posService.GetHeldSale(heldSaleId)
        If detail Is Nothing Then
            BindHeldBillsList()
            ShowCartMessage("Held bill was not found. It may have already been removed.", False)
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "ShowHeldBillsModal", "PosUI.showHeldBills();", True)
            Return
        End If

        Dim cart = GetCart()
        cart.Clear()
        If detail.Items IsNot Nothing Then
            For Each item In detail.Items
                Dim listUnit = If(item.ListUnitPrice.HasValue AndAlso item.ListUnitPrice.Value > 0D, item.ListUnitPrice.Value, item.UnitPrice)
                Dim inferredPct As Decimal = 0D
                If listUnit > 0D AndAlso item.UnitPrice > 0D AndAlso item.UnitPrice < listUnit Then
                    inferredPct = ClampPercent(Decimal.Round((1D - (item.UnitPrice / listUnit)) * 100D, 4, MidpointRounding.AwayFromZero))
                End If
                cart.Add(New CartItem() With {
                    .ProductId = item.ProductId,
                    .SkuCode = item.SkuCode,
                    .Name = item.Name,
                    .ListUnitPrice = Decimal.Round(Math.Max(0D, listUnit), 2, MidpointRounding.AwayFromZero),
                    .CustomerDefaultDiscountPercentApplied = inferredPct,
                    .UnitPrice = item.UnitPrice,
                    .Quantity = item.Quantity,
                    .TaxRate = item.TaxRate,
                    .Thumbnail = item.ThumbnailUrl,
                    .ItemDiscountMode = DiscountModePercent,
                    .ItemDiscountValue = 0D
                })
            Next
        End If

        ' Restore item-level discount UI from held sale discount intents (best-effort: first discount per item).
        If detail.Discounts IsNot Nothing AndAlso detail.Discounts.Count > 0 Then
            For Each discount In detail.Discounts
                If discount Is Nothing OrElse discount.TargetProductId Is Nothing OrElse discount.TargetProductId.Value <= 0 Then
                    Continue For
                End If
                If discount.Scope Is Nothing OrElse Not discount.Scope.Equals("ITEM", StringComparison.OrdinalIgnoreCase) Then
                    Continue For
                End If
                Dim target = cart.FirstOrDefault(Function(ci) ci.ProductId = discount.TargetProductId.Value)
                If target Is Nothing Then
                    Continue For
                End If
                target.ItemDiscountMode = If(discount.ValueType IsNot Nothing AndAlso discount.ValueType.Equals("AMOUNT", StringComparison.OrdinalIgnoreCase), DiscountModeAmount, DiscountModePercent)
                target.ItemDiscountValue = Math.Max(0D, discount.Value)
            Next
        End If

        Dim restoredDiscount = If(detail.Discounts IsNot Nothing, detail.Discounts.FirstOrDefault(Function(d) d IsNot Nothing AndAlso d.Scope IsNot Nothing AndAlso d.Scope.Equals("SUBTOTAL", StringComparison.OrdinalIgnoreCase)), Nothing)
        If restoredDiscount IsNot Nothing AndAlso restoredDiscount.Value > 0D Then
            txtDiscount.Text = restoredDiscount.Value.ToString(CultureInfo.InvariantCulture)
            If ddlDiscountMode IsNot Nothing Then
                ddlDiscountMode.SelectedValue = If(restoredDiscount.ValueType IsNot Nothing AndAlso restoredDiscount.ValueType.Equals("AMOUNT", StringComparison.OrdinalIgnoreCase), DiscountModeAmount, DiscountModePercent)
            End If
        Else
            ' IMPORTANT:
            ' Only restore SUBTOTAL discount when it explicitly exists in the saved discount intents.
            ' The held sale header DISCOUNT_PERCENT may represent an effective/aggregated discount
            ' (e.g. from item-level discounts) and must not be re-applied as a subtotal discount.
            txtDiscount.Text = "0"
            If ddlDiscountMode IsNot Nothing Then
                ddlDiscountMode.SelectedValue = DiscountModePercent
            End If
        End If
        ActiveHeldSaleId = detail.HeldSaleId
        SetSelectedCustomer(detail.DealerId)
        BindCart()
        ShowCartMessage($"Held bill {detail.ReferenceCode} restored into the cart.", True)
        ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HideHeldBillsModal", "PosUI.hideHeldBills();", True)
    End Sub

    Private Function BuildReceiptUrl(orderId As Integer) As String
        If orderId <= 0 Then
            Return String.Empty
        End If
        Return ResolveClientUrl($"~/ReceiptDownload.ashx?mode=sale&orderId={orderId}")
    End Function

    Private Sub TriggerReceiptDownload(orderId As Integer)
        Dim url = BuildReceiptUrl(orderId)
        If String.IsNullOrWhiteSpace(url) Then
            Return
        End If

        QueueReceiptDownload(url)
    End Sub

    Private Sub QueueReceiptDownload(url As String)
        If hfReceiptDownloadUrl IsNot Nothing Then
            hfReceiptDownloadUrl.Value = url
        End If
    End Sub
End Class