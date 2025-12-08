<%@ Page Title="Home Page" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Default.aspx.vb" Inherits="LocalPOS._Default" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="pos-shell">
        <div class="pos-header">
            <div>
                <div class="terminal-name">POS Terminal</div>
                <div class="location-info">Main Branch Register #01</div>
            </div>
            <div class="datetime text-end">
                <div id="currentDate"><asp:Literal ID="litDate" runat="server"></asp:Literal></div>
                <div id="currentTime"><asp:Literal ID="litTime" runat="server"></asp:Literal></div>
            </div>
            <div class="dropdown pos-user-menu">
                <button type="button"
                    class="user-pill dropdown-toggle"
                    id="posUserMenuToggle"
                    data-bs-toggle="dropdown"
                    aria-expanded="false">
                    <div class="user-avatar">
                        <asp:Literal runat="server" ID="litCashierInitials"></asp:Literal>
                    </div>
                    <div>
                        <div><asp:Label runat="server" ID="lblCashierName"></asp:Label></div>
                        <small>Cashier</small>
                    </div>
                    <span class="user-menu-caret" aria-hidden="true"></span>
                </button>
                <div class="dropdown-menu dropdown-menu-end pos-user-dropdown" aria-labelledby="posUserMenuToggle">
                    <asp:LinkButton runat="server" ID="btnLogout" CssClass="dropdown-item pos-user-item" OnClick="btnLogout_Click" CausesValidation="false">
                        <span class="logout-icon" aria-hidden="true"></span>
                        Log out
                    </asp:LinkButton>
                </div>
            </div>
        </div>

        <asp:UpdatePanel runat="server" ID="upPos" UpdateMode="Conditional">
            <ContentTemplate>
                <asp:HiddenField runat="server" ID="hfSelectedCategory" />
                <div class="pos-content">
                    <div>
                        <div class="pos-search-bar">
                            <div class="search-input">
                                <asp:TextBox runat="server"
                                    ID="txtSearch"
                                    CssClass="form-control border-0 bg-transparent"
                                    placeholder="Search products or scan barcode..."
                                    TextMode="Search"
                                    AutoPostBack="true"
                                    OnTextChanged="txtSearch_TextChanged"
                                    aria-label="Search products"></asp:TextBox>
                            </div>
                            <asp:Button runat="server" ID="btnSearch" Text="Search" CssClass="btn btn-outline-secondary search-btn" OnClick="btnSearch_Click" />
                            <button type="button" class="scan-btn">Scan</button>
                        </div>

                        <div class="pos-toolbar">
                            <asp:HyperLink runat="server" ID="lnkManageProducts" CssClass="btn btn-purple btn-add-product" NavigateUrl="~/ManageProducts.aspx">
                                <span class="add-product-icon" aria-hidden="true">+</span>
                                Add product
                            </asp:HyperLink>
                            <asp:Button runat="server"
                                ID="btnHeldBills"
                                CssClass="btn btn-purple btn-held-bills"
                                Text="Held bills"
                                OnClick="btnHeldBills_Click"
                                CausesValidation="false" />
                            <asp:HyperLink runat="server"
                                ID="lnkSalesHistory"
                                CssClass="btn btn-outline-secondary btn-sales-history"
                                NavigateUrl="~/SalesHistory.aspx">
                                Sales
                            </asp:HyperLink>
                        </div>

                        <div class="category-pills">
                            <asp:LinkButton runat="server" ID="btnAllCategories" CssClass="category-pill" CommandName="FilterCategory" OnClick="btnAllCategories_Click" Text="All"></asp:LinkButton>
                            <asp:Repeater runat="server" ID="rptCategories">
                                <ItemTemplate>
                                    <asp:LinkButton runat="server"
                                        CssClass='<%# GetCategoryCss(Container.DataItem.ToString()) %>'
                                        Text='<%# Container.DataItem %>'
                                        CommandName="FilterCategory"
                                        CommandArgument='<%# Container.DataItem %>'
                                        OnCommand="Category_Command" />
                                </ItemTemplate>
                            </asp:Repeater>
                        </div>

                        <asp:Label runat="server" ID="lblCatalogEmpty" CssClass="text-muted" Visible="False">No products found for the selected filters.</asp:Label>
                        <div class="product-grid">
                            <asp:Repeater runat="server" ID="rptProducts">
                                <ItemTemplate>
                                    <div class="product-card">
                                        <asp:HyperLink runat="server"
                                            CssClass="product-card-link"
                                            NavigateUrl='<%# GetProductDetailsUrl(Eval("Id")) %>'>
                                            <div class="product-thumb">
                                                <asp:Image runat="server" CssClass="product-thumb-img" ImageUrl='<%# GetProductImage(Eval("ImageUrl")) %>' AlternateText='<%# Eval("DisplayName") %>' />
                                                <span class="product-price-chip"><%# Eval("UnitPrice", "{0:C}") %></span>
                                            </div>
                                            <div class="product-info">
                                            <div class="product-name"><%# Eval("DisplayName") %></div>
                                            <div class="product-meta">
                                                <small><%# Eval("Category") %></small>
                                                <small>SKU: <%# Eval("SkuCode") %></small>
                                            </div>
                                            </div>
                                            <asp:Panel runat="server"
                                                CssClass="low-stock-banner"
                                                Visible='<%# IsLowStock(Eval("StockQuantity"), Eval("MinStockThreshold")) %>'>
                                                <span class="low-stock-dot"></span>
                                                <span><%# GetLowStockText(Eval("StockQuantity")) %></span>
                                            </asp:Panel>
                                        </asp:HyperLink>
                                        <asp:Button runat="server"
                                            CssClass='<%# GetAddToCartCss(Eval("StockQuantity")) %>'
                                            CommandName="AddToCart"
                                            CommandArgument='<%# Eval("Id") %>'
                                            Text='<%# GetAddToCartText(Eval("StockQuantity")) %>'
                                            Enabled='<%# Not IsOutOfStock(Eval("StockQuantity")) %>'
                                            OnCommand="Product_Command" />
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>
                        </div>
                    </div>

                    <div class="cart-panel">
                        <div class="customer-select">
                            <label class="form-label fw-semibold">Customer</label>
                            <asp:DropDownList runat="server" ID="ddlCustomers" AutoPostBack="true" CssClass="form-select" OnSelectedIndexChanged="ddlCustomers_SelectedIndexChanged"></asp:DropDownList>
                            <div class="customer-actions">
                                <asp:Button runat="server" ID="btnViewCustomerProfile" CssClass="btn btn-purple w-100 btn-view-profile" Text="View profile" CausesValidation="false" OnClick="btnViewCustomerProfile_Click" />
                            </div>
                        </div>

                        <div class="cart-header">
                            <span>Cart Items (<asp:Literal runat="server" ID="litCartCount"></asp:Literal>)</span>
                            <asp:LinkButton runat="server" ID="btnClearCart" CssClass="btn btn-danger btn-sm btn-clear-cart" OnClick="btnClearCart_Click" CausesValidation="false">Clear</asp:LinkButton>
                        </div>

                        <div class="cart-items">
                            <asp:Repeater runat="server" ID="rptCart" OnItemCommand="rptCart_ItemCommand">
                                <ItemTemplate>
                                    <div class="cart-item">
                                        <img src='<%# GetProductImage(Eval("Thumbnail")) %>' alt="item" />
                                        <div class="meta">
                                            <strong><%# Eval("Name") %></strong><br />
                                            <small>SKU <%# Eval("SkuCode") %></small>
                                        </div>
                                        <div class="text-end me-3">
                                            <div class="fw-bold"><%# Eval("LineTotal", "{0:C}") %></div>
                                            <small class="text-muted"><%# Eval("UnitPrice", "{0:C}") %> each</small>
                                        </div>
                                        <div class="qty-controls">
                                            <asp:LinkButton runat="server" CssClass="btn btn-qty btn-qty-decrease" Text="-" CommandName="Decrease" CommandArgument='<%# Eval("ProductId") %>' CausesValidation="false"></asp:LinkButton>
                                            <span><%# Eval("Quantity") %></span>
                                            <asp:LinkButton runat="server" CssClass="btn btn-qty btn-qty-increase" Text="+" CommandName="Increase" CommandArgument='<%# Eval("ProductId") %>' CausesValidation="false"></asp:LinkButton>
                                        </div>
                                        <asp:LinkButton runat="server" CssClass="btn btn-icon btn-remove-item" CommandName="Remove" CommandArgument='<%# Eval("ProductId") %>' CausesValidation="false">
                                            <svg class="icon-trash" viewBox="0 0 24 24" role="img" aria-hidden="true" focusable="false">
                                                <path d="M9 3h6l1 2h4v2H4V5h4l1-2zm1 6v8h2V9h-2zm4 0v8h2V9h-2z" fill="currentColor"/>
                                            </svg>
                                            <span class="visually-hidden">Remove</span>
                                        </asp:LinkButton>
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>
                        </div>
                        <asp:Panel runat="server" ID="pnlEmptyCart" CssClass="empty-cart" Visible="false">
                            <p>Cart is empty. Add products to start.</p>
                        </asp:Panel>

                        <div class="discount-control">
                            <div class="discount-header">
                                <label class="form-label fw-semibold mb-0">Discount</label>
                                <asp:RadioButtonList runat="server"
                                    ID="rblDiscountMode"
                                    CssClass="discount-mode-pill"
                                    RepeatDirection="Horizontal"
                                    RepeatLayout="Flow"
                                    AutoPostBack="true"
                                    OnSelectedIndexChanged="rblDiscountMode_SelectedIndexChanged">
                                    <asp:ListItem Text="Percent" Value="Percentage" Selected="True"></asp:ListItem>
                                    <asp:ListItem Text="Value" Value="Amount"></asp:ListItem>
                                </asp:RadioButtonList>
                            </div>
                            <asp:TextBox runat="server"
                                ID="txtDiscount"
                                CssClass="discount-input pos-discount-input"
                                TextMode="Number"
                                AutoPostBack="true"
                                Min="0"
                                step="0.01"
                                OnTextChanged="txtDiscount_TextChanged"></asp:TextBox>
                        </div>

                        <div class="summary-card">
                            <div class="summary-row">
                                <span>Subtotal</span>
                                <strong><asp:Literal runat="server" ID="litSubtotal"></asp:Literal></strong>
                            </div>
                            <div class="summary-row">
                                <span>Tax (<asp:Literal runat="server" ID="litTaxRate"></asp:Literal>)</span>
                                <strong><asp:Literal runat="server" ID="litTax"></asp:Literal></strong>
                            </div>
                            <div class="summary-row total">
                                <span>Total</span>
                                <asp:Literal runat="server" ID="litTotal"></asp:Literal>
                            </div>
                        </div>

                        <asp:Label runat="server" ID="lblCartMessage" CssClass="text-danger"></asp:Label>

                        <div class="action-buttons">
                            <asp:Button runat="server" ID="btnHold" CssClass="btn btn-outline" Text="Hold" OnClick="btnHold_Click" CausesValidation="false" />
                            <asp:Button runat="server" ID="btnNewSale" CssClass="btn btn-outline" Text="New" OnClick="btnNewSale_Click" CausesValidation="false" />
                            <asp:Button runat="server" ID="btnCheckout" CssClass="btn btn-primary btn-action-checkout" Text="Checkout" OnClick="btnCheckout_Click" CausesValidation="false" />
                        </div>
                    </div>
                </div>

                <div class="modal fade modal-pos" id="paymentModal" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered modal-lg">
                        <div class="modal-content payment-modal">
                            <div class="modal-header border-0 align-items-start">
                                <div>
                                    <p class="text-muted mb-1">Amount Due</p>
                                    <div class="display-6 fw-bold text-dark amount-highlight" data-amount-due-display="true">
                                        <asp:Literal runat="server" ID="litAmountDueHeader" ClientIDMode="Static"></asp:Literal>
                                    </div>
                                </div>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <div class="payment-summary-grid">
                                    <div>
                                        <small class="text-muted">Subtotal</small>
                                        <div class="fw-bold">
                                            <asp:Literal runat="server" ID="litModalSubtotal" ClientIDMode="Static"></asp:Literal>
                                        </div>
                                    </div>
                                    <div>
                                        <small class="text-muted">Discount</small>
                                        <div class="fw-bold text-success">
                                            <asp:Literal runat="server" ID="litModalDiscount" ClientIDMode="Static"></asp:Literal>
                                        </div>
                                    </div>
                                    <div>
                                        <small class="text-muted">VAT</small>
                                        <div class="fw-bold">
                                            <span id="modalVatDisplay" data-payment-summary="vat"><asp:Literal runat="server" ID="litModalTax" ClientIDMode="Static"></asp:Literal></span>
                                        </div>
                                    </div>
                                    <div>
                                        <small class="text-muted">Total Due</small>
                                        <div class="fw-bold text-dark">
                                            <span id="modalTotalDisplay" data-payment-summary="total"><asp:Literal runat="server" ID="litModalTotal" ClientIDMode="Static"></asp:Literal></span>
                                        </div>
                                    </div>
                                </div>

                                <div class="tax-adjuster">
                                    <label for="txtModalTaxPercent" class="form-label fw-semibold">VAT %</label>
                                    <asp:TextBox runat="server"
                                        ID="txtModalTaxPercent"
                                        ClientIDMode="Static"
                                        CssClass="form-control tax-percent-input"
                                        TextMode="Number"
                                        step="0.1"
                                        min="0"
                                        max="100"></asp:TextBox>
                                    <small class="text-muted">Default 5%. Update only if VAT changes.</small>
                                </div>

                                <div class="payment-methods">
                                    <label class="form-label fw-semibold">Choose payment method</label>
                                    <asp:RadioButtonList runat="server"
                                        ID="rblPaymentMethod"
                                        ClientIDMode="Static"
                                        CssClass="payment-method-buttons js-payment-methods"
                                        RepeatDirection="Horizontal"
                                        RepeatLayout="Flow">
                                        <asp:ListItem Value="Cash" Selected="True">Cash</asp:ListItem>
                                        <asp:ListItem Value="Card">Card</asp:ListItem>
                                    </asp:RadioButtonList>
                                </div>

                                <asp:Panel runat="server" ID="pnlCorporatePayment" ClientIDMode="Static" CssClass="corporate-payment-options d-none">
                                    <label class="form-label fw-semibold">Corporate payment type</label>
                                    <asp:RadioButtonList runat="server"
                                        ID="rblCorporatePaymentType"
                                        ClientIDMode="Static"
                                        CssClass="corporate-payment-type js-corporate-type"
                                        RepeatDirection="Horizontal"
                                        RepeatLayout="Flow">
                                        <asp:ListItem Value="Full" Selected="True">Full</asp:ListItem>
                                        <asp:ListItem Value="Partial">Partial</asp:ListItem>
                                    </asp:RadioButtonList>
                                    <div id="corporatePartialWrapper" class="partial-input d-none">
                                        <label class="form-label">Partial amount</label>
                                        <asp:TextBox runat="server" ID="txtCorporatePartialAmount" ClientIDMode="Static" CssClass="form-control" TextMode="Number" step="0.01" min="0"></asp:TextBox>
                                    </div>
                                </asp:Panel>

                                <div id="cashPaymentPanel" class="payment-panel" data-payment-panel="Cash">
                                    <div class="panel-header">
                                        <div>
                                            <div class="text-muted small">Amount Due</div>
                                            <div class="fs-4 fw-bold" data-amount-due-display="true">
                                                <asp:Literal runat="server" ID="litCashAmountDue" ClientIDMode="Static"></asp:Literal>
                                            </div>
                                        </div>
                                        <div>
                                            <div class="text-muted small">Change to return</div>
                                            <div class="fs-4 fw-bold text-success" id="cashChangeDisplay">
                                                <asp:Literal runat="server" ID="litCashChange" ClientIDMode="Static"></asp:Literal>
                                            </div>
                                        </div>
                                    </div>

                                    <div class="cash-input">
                                        <label for="txtCashReceived" class="form-label fw-semibold">Cash received</label>
                                        <asp:TextBox runat="server" ID="txtCashReceived" ClientIDMode="Static" CssClass="form-control form-control-lg" TextMode="Number" step="0.01" min="0" placeholder="Enter amount"></asp:TextBox>
                                    </div>

                                    <div class="quick-buttons">
                                        <span class="text-muted small">Quick keys</span>
                                        <div class="quick-buttons-grid">
                                            <button type="button" class="btn btn-light cash-quick-btn" data-cash-action="add" data-cash-value="10">+10</button>
                                            <button type="button" class="btn btn-light cash-quick-btn" data-cash-action="add" data-cash-value="20">+20</button>
                                            <button type="button" class="btn btn-light cash-quick-btn" data-cash-action="add" data-cash-value="50">+50</button>
                                            <button type="button" class="btn btn-light cash-quick-btn" data-cash-action="add" data-cash-value="100">+100</button>
                                            <button type="button" class="btn btn-outline-primary cash-quick-btn" data-cash-action="exact">Exact</button>
                                        </div>
                                    </div>

                                    <div class="numeric-keypad">
                                        <button type="button" data-key="7">7</button>
                                        <button type="button" data-key="8">8</button>
                                        <button type="button" data-key="9">9</button>
                                        <button type="button" data-key="clear">C</button>
                                        <button type="button" data-key="4">4</button>
                                        <button type="button" data-key="5">5</button>
                                        <button type="button" data-key="6">6</button>
                                        <button type="button" data-key="back">DEL</button>
                                        <button type="button" data-key="1">1</button>
                                        <button type="button" data-key="2">2</button>
                                        <button type="button" data-key="3">3</button>
                                        <button type="button" data-key="." class="dot-key">.</button>
                                        <button type="button" class="span-two" data-key="0">0</button>
                                        <button type="button" data-key="00">00</button>
                                    </div>
                                </div>

                                <div id="cardPaymentPanel" class="payment-panel d-none" data-payment-panel="Card">
                                    <div class="panel-header mb-3">
                                        <div>
                                            <div class="text-muted small">Amount</div>
                                            <div class="fs-4 fw-bold" data-amount-due-display="true">
                                                <asp:Literal runat="server" ID="litCardAmountDue" ClientIDMode="Static"></asp:Literal>
                                            </div>
                                        </div>
                                    </div>
                                    <p class="text-muted">Please process the amount on the bank POS terminal and record the reference below.</p>

                                    <div class="mb-3">
                                        <label for="txtCardRrn" class="form-label fw-semibold">Bank POS receipt no (RRN)</label>
                                        <asp:TextBox runat="server" ID="txtCardRrn" ClientIDMode="Static" CssClass="form-control" placeholder="Enter RRN"></asp:TextBox>
                                    </div>
                                    <div class="mb-3">
                                        <label for="txtCardAuthCode" class="form-label fw-semibold">Auth code (optional)</label>
                                        <asp:TextBox runat="server" ID="txtCardAuthCode" ClientIDMode="Static" CssClass="form-control" placeholder="Enter authorization code"></asp:TextBox>
                                    </div>
                                    <div class="mb-3">
                                        <label for="ddlCardStatus" class="form-label fw-semibold">Payment status</label>
                                        <asp:DropDownList runat="server" ID="ddlCardStatus" ClientIDMode="Static" CssClass="form-select">
                                            <asp:ListItem Text="Approved" Value="Approved" Selected="True"></asp:ListItem>
                                            <asp:ListItem Text="Declined" Value="Declined"></asp:ListItem>
                                        </asp:DropDownList>
                                    </div>
                                </div>

                                <asp:Label runat="server" ID="lblCheckoutError" CssClass="text-danger mt-3 d-block"></asp:Label>
                            </div>
                            <div class="modal-footer border-0">
                                <asp:Button runat="server" ID="btnCancelPayment" CssClass="btn btn-outline-secondary" Text="Cancel" CausesValidation="false" OnClientClick="PosUI.hidePaymentModal(); return false;" />
                                <asp:Button runat="server" ID="btnCompletePayment" CssClass="btn btn-success" Text="Complete Sale" OnClick="btnCompletePayment_Click" />
                            </div>
                        </div>
                    </div>
                </div>
                <asp:HiddenField runat="server" ID="hfAmountDue" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfBaseAmountDue" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfCurrencySymbol" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfIsCorporateCustomer" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfTaxableAmount" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfDefaultTaxPercent" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfActiveHeldSaleId" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfReceiptDownloadUrl" ClientIDMode="Static" />

                <div class="modal fade modal-pos" id="holdConfirmModal" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content hold-modal">
                            <div class="modal-header border-0">
                                <div>
                                    <h5 class="modal-title mb-1">Hold this bill?</h5>
                                    <p class="text-muted mb-0">Cart will be saved for later. Screen will reset.</p>
                                </div>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close" onclick="PosUI.hideHoldConfirm();"></button>
                            </div>
                            <div class="modal-body">
                                <div class="hold-summary">
                                    <div>
                                        <small class="text-muted d-block">Items</small>
                                        <strong><asp:Literal runat="server" ID="litHoldSummaryItems"></asp:Literal></strong>
                                    </div>
                                    <div>
                                        <small class="text-muted d-block">Cart total</small>
                                        <strong><asp:Literal runat="server" ID="litHoldSummaryTotal"></asp:Literal></strong>
                                    </div>
                                </div>
                                <p class="mb-0 text-muted">Held bills do not affect stock or accounting until you resume and checkout.</p>
                            </div>
                            <div class="modal-footer border-0">
                                <button type="button" class="btn btn-outline-secondary" onclick="PosUI.hideHoldConfirm();">Cancel</button>
                                <asp:Button runat="server" ID="btnConfirmHold" CssClass="btn btn-primary" Text="Yes, hold bill" OnClick="btnConfirmHold_Click" CausesValidation="false" />
                            </div>
                        </div>
                    </div>
                </div>

                <div class="modal fade modal-pos" id="heldBillsModal" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered modal-lg">
                        <div class="modal-content held-bills-modal">
                            <div class="modal-header border-0">
                                <div>
                                    <h5 class="modal-title mb-0">Held bills</h5>
                                    <small class="text-muted">Resume or delete parked carts.</small>
                                </div>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close" onclick="PosUI.hideHeldBills();"></button>
                            </div>
                            <div class="modal-body">
                                <asp:Panel runat="server" ID="pnlHeldBillsEmpty" CssClass="empty-held-bills" Visible="false">
                                    <p class="mb-0 text-muted">No held bills right now.</p>
                                </asp:Panel>
                                <asp:Repeater runat="server" ID="rptHeldBills" OnItemCommand="rptHeldBills_ItemCommand">
                                    <ItemTemplate>
                                        <div class="held-bill-row">
                                            <div class="held-bill-info">
                                                <div class="held-bill-ref"><%# Eval("ReferenceCode") %></div>
                                                <small class="text-muted"><%# Eval("RelativeAge") %></small>
                                            </div>
                                            <div class="held-bill-meta">
                                                <span><%# Eval("ItemsCount") %> items</span>
                                                <strong><%# Eval("TotalAmount", "{0:C}") %></strong>
                                            </div>
                                            <div class="held-bill-actions">
                                                <asp:LinkButton runat="server" CssClass="btn btn-link btn-sm text-primary" CommandName="Resume" CommandArgument='<%# Eval("HeldSaleId") %>' CausesValidation="false">Resume</asp:LinkButton>
                                                <asp:LinkButton runat="server" CssClass="btn btn-icon btn-sm text-danger" CommandName="Delete" CommandArgument='<%# Eval("HeldSaleId") %>' CausesValidation="false">
                                                    <svg class="icon-trash" viewBox="0 0 24 24" role="img" aria-hidden="true" focusable="false">
                                                        <path d="M9 3h6l1 2h4v2H4V5h4l1-2zm1 6v8h2V9h-2zm4 0v8h2V9h-2z" fill="currentColor" />
                                                    </svg>
                                                    <span class="visually-hidden">Delete</span>
                                                </asp:LinkButton>
                                            </div>
                                        </div>
                                    </ItemTemplate>
                                </asp:Repeater>
                            </div>
                        </div>
                    </div>
                </div>
            </ContentTemplate>
        </asp:UpdatePanel>
    </div>
</asp:Content>
