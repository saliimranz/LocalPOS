<%@ Page Title="Sales" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="SalesHistory.aspx.vb" Inherits="LocalPOS.SalesHistory" %>

<asp:Content ID="MainContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="sales-history-shell">
        <div class="pos-header sales-history-header">
            <div>
                <asp:HyperLink runat="server" ID="lnkBackToPos" CssClass="btn btn-light btn-back-to-pos" NavigateUrl="~/Default.aspx">
                    <span class="back-icon" aria-hidden="true">&larr;</span>
                    Back to POS
                </asp:HyperLink>
                <div class="terminal-name">Sales</div>
                <div class="location-info text-muted">Review every order, payment, and receipt in one place.</div>
            </div>
            <div class="dropdown pos-user-menu">
                <button type="button"
                    class="user-pill dropdown-toggle"
                    id="salesUserMenuToggle"
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
                <div class="dropdown-menu dropdown-menu-end pos-user-dropdown" aria-labelledby="salesUserMenuToggle">
                    <asp:LinkButton runat="server" ID="btnLogout" CssClass="dropdown-item pos-user-item" OnClick="btnLogout_Click" CausesValidation="false">
                        <span class="logout-icon" aria-hidden="true"></span>
                        Log out
                    </asp:LinkButton>
                </div>
            </div>
        </div>

        <asp:UpdatePanel runat="server" ID="upSalesHistory" UpdateMode="Conditional">
            <ContentTemplate>
                <div class="history-filters card mb-4">
                    <div class="card-body">
                        <div class="row g-3 align-items-end">
                            <div class="col-md-3">
                                <label class="form-label fw-semibold">Quick range</label>
                                <asp:DropDownList runat="server" ID="ddlDateRange" CssClass="form-select" AutoPostBack="true" OnSelectedIndexChanged="ddlDateRange_SelectedIndexChanged">
                                    <asp:ListItem Value="Today" Selected="True">Today</asp:ListItem>
                                    <asp:ListItem Value="Yesterday">Yesterday</asp:ListItem>
                                    <asp:ListItem Value="Custom">Custom range</asp:ListItem>
                                </asp:DropDownList>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label fw-semibold">From</label>
                                <asp:TextBox runat="server" ID="txtFromDate" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div class="col-md-3">
                                <label class="form-label fw-semibold">To</label>
                                <asp:TextBox runat="server" ID="txtToDate" CssClass="form-control" TextMode="Date"></asp:TextBox>
                            </div>
                            <div class="col-md-2">
                                <label class="form-label fw-semibold">Order #</label>
                                <asp:TextBox runat="server" ID="txtOrderSearch" CssClass="form-control" placeholder="eg. POS-2025..."></asp:TextBox>
                            </div>
                            <div class="col-md-1 d-grid">
                                <asp:Button runat="server" ID="btnApplyFilters" CssClass="btn btn-primary" Text="Apply" OnClick="btnApplyFilters_Click" />
                            </div>
                        </div>
                        <asp:Label runat="server" ID="lblFilterMessage" CssClass="text-danger small d-block mt-2"></asp:Label>
                    </div>
                </div>

                <div class="history-summary card mb-4">
                    <div class="card-body summary-grid">
                        <div>
                            <small class="text-muted text-uppercase">Orders</small>
                            <div class="h4 mb-0"><asp:Literal runat="server" ID="litOrderCount"></asp:Literal></div>
                        </div>
                        <div>
                            <small class="text-muted text-uppercase">Gross sales</small>
                            <div class="h4 mb-0"><asp:Literal runat="server" ID="litGrossTotal"></asp:Literal></div>
                        </div>
                        <div>
                            <small class="text-muted text-uppercase">Outstanding</small>
                            <div class="h4 mb-0 text-danger"><asp:Literal runat="server" ID="litOutstandingTotal"></asp:Literal></div>
                        </div>
                    </div>
                </div>

                <asp:Repeater runat="server" ID="rptSales" OnItemDataBound="rptSales_ItemDataBound">
                    <ItemTemplate>
                        <div class="order-card order-card-extended mb-4">
                            <div class="order-card-header">
                                <div>
                                    <div class="order-number">#<%# Eval("OrderNumber") %></div>
                                    <small class="text-muted"><%# Eval("CreatedOn", "{0:MMMM dd, yyyy HH:mm}") %></small>
                                </div>
                                <span class='<%# GetStatusCss(Eval("OutstandingAmount")) %>'><%# GetStatusText(Eval("OutstandingAmount")) %></span>
                            </div>
                            <div class="order-card-body order-card-body-grid">
                                <div>
                                    <span class="label text-muted">Customer</span>
                                    <strong><%# Eval("CustomerName") %></strong>
                                </div>
                                <div>
                                    <span class="label text-muted">Payment method</span>
                                    <strong><%# Eval("PaymentMethod") %></strong>
                                </div>
                                <div>
                                    <span class="label text-muted">Order total</span>
                                    <strong><%# FormatCurrency(Eval("TotalAmount")) %></strong>
                                </div>
                                <div>
                                    <span class="label text-muted">Paid so far</span>
                                    <strong><%# FormatCurrency(Eval("TotalPaid")) %></strong>
                                </div>
                                <div>
                                    <span class="label text-muted">Outstanding</span>
                                    <strong class='<%# If(Convert.ToDecimal(Eval("OutstandingAmount")) > 0D, "text-danger", "text-muted") %>'><%# FormatCurrency(Eval("OutstandingAmount")) %></strong>
                                </div>
                                <div class="order-card-actions text-end">
                                    <asp:HyperLink runat="server" ID="lnkReceipt" CssClass="btn btn-outline-primary btn-sm" Target="_blank" NavigateUrl='<%# GetReceiptUrl(Eval("OrderId")) %>'>
                                        Download receipt
                                    </asp:HyperLink>
                                </div>
                            </div>
                            <div class="order-line-items">
                                <table class="table table-sm align-middle mb-0">
                                    <thead>
                                        <tr>
                                            <th>Item</th>
                                            <th class="text-center">Qty</th>
                                            <th class="text-end">Unit price</th>
                                            <th class="text-end">Line total</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <asp:Repeater runat="server" ID="rptLineItems">
                                            <ItemTemplate>
                                                <tr>
                                                    <td><%# Eval("Name") %></td>
                                                    <td class="text-center"><%# Eval("Quantity") %></td>
                                                    <td class="text-end"><%# Eval("UnitPrice", "{0:C}") %></td>
                                                    <td class="text-end"><%# Eval("LineTotal", "{0:C}") %></td>
                                                </tr>
                                            </ItemTemplate>
                                        </asp:Repeater>
                                    </tbody>
                                </table>
                            </div>
                            <div class="order-totals-grid">
                                <div>
                                    <span class="label text-muted">Subtotal</span>
                                    <strong><%# FormatCurrency(Eval("Subtotal")) %></strong>
                                </div>
                                <div>
                                    <span class="label text-muted">Discount</span>
                                    <strong><%# FormatCurrency(Eval("DiscountAmount")) %></strong>
                                    <small class="text-muted d-block"><%# Eval("DiscountPercent", "{0:N2}") %>%</small>
                                </div>
                                <div>
                                    <span class="label text-muted">Tax (<%# Eval("TaxPercent", "{0:N2}") %>%)</span>
                                    <strong><%# FormatCurrency(Eval("TaxAmount")) %></strong>
                                </div>
                                <div>
                                    <span class="label text-muted">Total</span>
                                    <strong><%# FormatCurrency(Eval("TotalAmount")) %></strong>
                                </div>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
                <asp:Panel runat="server" ID="pnlNoResults" CssClass="alert alert-light" Visible="false">
                    No sales found for the selected filters.
                </asp:Panel>
            </ContentTemplate>
        </asp:UpdatePanel>
    </div>
</asp:Content>
