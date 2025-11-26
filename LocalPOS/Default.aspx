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
            <div class="user-pill">
                <div class="user-avatar">JS</div>
                <div>
                    <div><asp:Label runat="server" ID="lblCashierName"></asp:Label></div>
                    <small>Cashier</small>
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
                                <asp:TextBox runat="server" ID="txtSearch" CssClass="form-control border-0 bg-transparent" placeholder="Search products or scan barcode..." AutoPostBack="false"></asp:TextBox>
                            </div>
                            <asp:Button runat="server" ID="btnSearch" Text="Search" CssClass="btn btn-outline-secondary" OnClick="btnSearch_Click" />
                            <button type="button" class="scan-btn">Scan</button>
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
                        <asp:Repeater runat="server" ID="rptProducts">
                            <ItemTemplate>
                                <div class="product-card">
                                    <span class="price-tag"><%# Eval("UnitPrice", "{0:C}") %></span>
                                    <asp:Image runat="server" ImageUrl='<%# GetProductImage(Eval("ImageUrl")) %>' AlternateText='<%# Eval("DisplayName") %>' />
                                    <div class="card-body">
                                        <strong><%# Eval("DisplayName") %></strong>
                                        <small class="text-muted"><%# Eval("Category") %></small>
                                        <small class="text-muted">SKU: <%# Eval("SkuCode") %></small>
                                    </div>
                                    <div class="card-footer">
                                        <asp:Button runat="server"
                                            CssClass="btn btn-light w-100"
                                            CommandName="AddToCart"
                                            CommandArgument='<%# Eval("Id") %>'
                                            Text="Add to cart"
                                            OnCommand="Product_Command" />
                                    </div>
                                </div>
                            </ItemTemplate>
                        </asp:Repeater>
                    </div>

                    <div class="cart-panel">
                        <div class="customer-select">
                            <label class="form-label fw-semibold">Customer</label>
                            <asp:DropDownList runat="server" ID="ddlCustomers" AutoPostBack="true" CssClass="form-select" OnSelectedIndexChanged="ddlCustomers_SelectedIndexChanged"></asp:DropDownList>
                        </div>

                        <div class="cart-header">
                            <span>Cart Items (<asp:Literal runat="server" ID="litCartCount"></asp:Literal>)</span>
                            <asp:LinkButton runat="server" ID="btnClearCart" CssClass="text-danger" OnClick="btnClearCart_Click">Clear</asp:LinkButton>
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
                                            <asp:LinkButton runat="server" CssClass="qty-btn" Text="-" CommandName="Decrease" CommandArgument='<%# Eval("ProductId") %>'></asp:LinkButton>
                                            <span><%# Eval("Quantity") %></span>
                                            <asp:LinkButton runat="server" CssClass="qty-btn" Text="+" CommandName="Increase" CommandArgument='<%# Eval("ProductId") %>'></asp:LinkButton>
                                        </div>
                                        <asp:LinkButton runat="server" CssClass="btn btn-link text-danger" Text="x" CommandName="Remove" CommandArgument='<%# Eval("ProductId") %>'></asp:LinkButton>
                                    </div>
                                </ItemTemplate>
                            </asp:Repeater>
                        </div>
                        <asp:Panel runat="server" ID="pnlEmptyCart" CssClass="empty-cart" Visible="false">
                            <p>Cart is empty. Add products to start.</p>
                        </asp:Panel>

                        <div>
                            <label class="form-label fw-semibold">Discount %</label>
                            <asp:TextBox runat="server" ID="txtDiscount" CssClass="discount-input" TextMode="Number" AutoPostBack="true" Min="0" Max="100" step="0.5" OnTextChanged="txtDiscount_TextChanged"></asp:TextBox>
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
                            <asp:Button runat="server" ID="btnCheckout" CssClass="btn btn-primary" Text="Checkout" OnClick="btnCheckout_Click" CausesValidation="false" />
                        </div>
                    </div>
                </div>

                <div class="modal fade modal-pos" id="paymentModal" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered">
                        <div class="modal-content">
                            <div class="modal-header border-0">
                                <div>
                                    <h5 class="modal-title fw-bold">Complete Payment</h5>
                                    <small>Total Amount</small>
                                    <div class="fs-4 fw-bold text-dark">
                                        <asp:Literal runat="server" ID="litModalTotal"></asp:Literal>
                                    </div>
                                </div>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <div id="paymentOptions">
                                    <asp:RadioButtonList runat="server" ID="rblPaymentMethod" CssClass="payment-options" RepeatDirection="Horizontal" RepeatLayout="Flow">
                                        <asp:ListItem Value="Cash" Selected="True">Cash</asp:ListItem>
                                        <asp:ListItem Value="Card">Card</asp:ListItem>
                                        <asp:ListItem Value="Credit">Credit Account</asp:ListItem>
                                        <asp:ListItem Value="Partial">Partial Payment</asp:ListItem>
                                    </asp:RadioButtonList>
                                </div>
                                <div id="partialAmountWrapper" class="partial-input d-none">
                                    <label class="form-label">Partial Amount</label>
                                    <asp:TextBox runat="server" ID="txtPartialAmount" CssClass="form-control" TextMode="Number" step="0.01"></asp:TextBox>
                                </div>
                                <asp:Label runat="server" ID="lblCheckoutError" CssClass="text-danger mt-3 d-block"></asp:Label>
                            </div>
                            <div class="modal-footer border-0">
                                <asp:Button runat="server" ID="btnCancelPayment" CssClass="btn btn-outline-secondary" Text="Cancel" CausesValidation="false" OnClientClick="PosUI.hidePaymentModal(); return false;" />
                                <asp:Button runat="server" ID="btnCompletePayment" CssClass="btn btn-success" Text="Complete &amp; Print" OnClick="btnCompletePayment_Click" />
                            </div>
                        </div>
                    </div>
                </div>
            </ContentTemplate>
        </asp:UpdatePanel>
    </div>
</asp:Content>
