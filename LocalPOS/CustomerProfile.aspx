<%@ Page Title="Customer Profile" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="CustomerProfile.aspx.vb" Inherits="LocalPOS.CustomerProfile" %>

<asp:Content ID="Content1" ContentPlaceHolderID="MainContent" runat="server">
    <div class="customer-profile-shell">
        <div class="pos-header customer-profile-header">
            <div>
                <asp:HyperLink runat="server" ID="lnkBackToPos" CssClass="btn btn-light btn-back-to-pos" NavigateUrl="~/Default.aspx">
                    <span class="back-icon" aria-hidden="true">&larr;</span>
                    Back to POS
                </asp:HyperLink>
                <div class="terminal-name">
                    <asp:Literal runat="server" ID="litCustomerName"></asp:Literal>
                </div>
                <div class="location-info text-muted">
                    Customer code: <asp:Literal runat="server" ID="litCustomerCode"></asp:Literal>
                </div>
            </div>
            <div class="datetime text-end text-white">
                <div class="customer-outstanding-label small fw-semibold">Outstanding</div>
                <div class="display-6 fw-bold customer-outstanding-amount">
                    <asp:Literal runat="server" ID="litOutstandingHeader"></asp:Literal>
                </div>
            </div>
            <div class="user-menu-wrapper" data-user-menu-wrapper>
                <button type="button" class="user-pill user-menu-toggle" data-user-menu-toggle aria-haspopup="true" aria-expanded="false">
                    <div class="user-avatar">
                        <asp:Literal runat="server" ID="litCashierInitials"></asp:Literal>
                    </div>
                    <div>
                        <div><asp:Label runat="server" ID="lblCashierName"></asp:Label></div>
                        <small>Cashier</small>
                    </div>
                    <span class="user-menu-caret" aria-hidden="true"></span>
                </button>
                <div class="user-menu-dropdown" data-user-menu-panel>
                    <asp:LinkButton runat="server" ID="btnLogout" CssClass="user-menu-item" OnClick="btnLogout_Click" CausesValidation="false">Log out</asp:LinkButton>
                </div>
            </div>
        </div>

        <asp:Label runat="server" ID="lblPageMessage" CssClass="d-block mb-3"></asp:Label>

        <div class="customer-profile-grid">
            <div class="profile-card">
                <h5>Customer details</h5>
                <dl class="profile-dl">
                    <dt>Contact person</dt>
                    <dd><asp:Literal runat="server" ID="litCustomerContact"></asp:Literal></dd>
                    <dt>Phone</dt>
                    <dd><asp:Literal runat="server" ID="litCustomerPhone"></asp:Literal></dd>
                    <dt>City</dt>
                    <dd><asp:Literal runat="server" ID="litCustomerCity"></asp:Literal></dd>
                </dl>
            </div>
            <div class="profile-card">
                <h5>Balance summary</h5>
                <div class="balance-row">
                    <span>Total paid to date</span>
                    <strong><asp:Literal runat="server" ID="litTotalPaid"></asp:Literal></strong>
                </div>
                <div class="balance-row">
                    <span>Outstanding</span>
                    <strong><asp:Literal runat="server" ID="litOutstandingTotal"></asp:Literal></strong>
                </div>
            </div>
        </div>

        <asp:UpdatePanel runat="server" ID="upOrders" UpdateMode="Conditional">
            <ContentTemplate>
                <div class="order-history-header">
                    <h4>Order history</h4>
                    <p class="text-muted mb-0">Review every order along with its payment progress. Pending balances can be settled from here.</p>
                </div>
                <asp:Repeater runat="server" ID="rptOrders" OnItemCommand="rptOrders_ItemCommand">
                    <ItemTemplate>
                        <div class="order-card">
                            <div class="order-card-header">
                                <div>
                                    <div class="order-number">#<%# Eval("OrderNumber") %></div>
                                    <small class="text-muted"><%# Eval("CreatedOn", "{0:MMMM dd, yyyy HH:mm}") %></small>
                                </div>
                                <span class='<%# GetStatusCss(Eval("Outstanding"), Eval("Status")) %>'><%# GetStatusText(Eval("Outstanding"), Eval("Status")) %></span>
                            </div>
                            <div class="order-card-body">
                                <div>
                                    <span class="label">Order total</span>
                                    <strong><%# FormatCurrency(Eval("TotalAmount")) %></strong>
                                </div>
                                <div>
                                    <span class="label">Paid so far</span>
                                    <strong><%# FormatCurrency(Eval("TotalPaid")) %></strong>
                                </div>
                                <div>
                                    <span class="label">Outstanding</span>
                                    <strong class='<%# If(Convert.ToDecimal(Eval("Outstanding")) > 0D, "text-danger", "text-muted") %>'><%# FormatCurrency(Eval("Outstanding")) %></strong>
                                </div>
                                <div class="order-card-actions">
                                    <asp:LinkButton runat="server"
                                        CssClass="btn btn-sm btn-primary"
                                        CommandName="Settle"
                                        CommandArgument='<%# Eval("OrderId") %>'
                                        Visible='<%# HasOutstanding(Eval("Outstanding")) %>'>Collect payment</asp:LinkButton>
                                </div>
                            </div>
                        </div>
                    </ItemTemplate>
                </asp:Repeater>
                <asp:Panel runat="server" ID="pnlNoOrders" CssClass="alert alert-light" Visible="false">
                    No orders found for this customer yet.
                </asp:Panel>

                <div class="modal fade modal-pos" id="paymentModal" tabindex="-1" aria-hidden="true">
                    <div class="modal-dialog modal-dialog-centered modal-lg">
                        <div class="modal-content payment-modal">
                            <div class="modal-header border-0 align-items-start">
                                <div>
                                    <p class="text-muted mb-1">Outstanding balance</p>
                                    <div class="display-6 fw-bold text-dark amount-highlight" data-amount-due-display="true">
                                        <asp:Literal runat="server" ID="litAmountDueHeader" ClientIDMode="Static"></asp:Literal>
                                    </div>
                                    <small class="text-muted d-block" id="settlementSummary">
                                        <asp:Literal runat="server" ID="litSettlementSummary"></asp:Literal>
                                    </small>
                                </div>
                                <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
                            </div>
                            <div class="modal-body">
                                <div class="payment-summary-grid">
                                    <div>
                                        <small class="text-muted">Order total</small>
                                        <div class="fw-bold">
                                            <asp:Literal runat="server" ID="litModalSubtotal" ClientIDMode="Static"></asp:Literal>
                                        </div>
                                    </div>
                                    <div>
                                        <small class="text-muted">Paid previously</small>
                                        <div class="fw-bold text-success">
                                            <asp:Literal runat="server" ID="litModalDiscount" ClientIDMode="Static"></asp:Literal>
                                        </div>
                                    </div>
                                    <div>
                                        <small class="text-muted">VAT (locked)</small>
                                        <div class="fw-bold">
                                            <span id="modalVatDisplay" data-payment-summary="vat"><asp:Literal runat="server" ID="litModalTax" ClientIDMode="Static"></asp:Literal></span>
                                        </div>
                                    </div>
                                    <div>
                                        <small class="text-muted">Outstanding now</small>
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
                                        ReadOnly="true"></asp:TextBox>
                                    <small class="text-muted">VAT is locked to the original sale value.</small>
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
                                    <p class="text-muted">Process the amount on the bank terminal and capture the reference.</p>

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
                                <asp:Button runat="server" ID="btnCompletePayment" CssClass="btn btn-success" Text="Mark as paid" OnClick="btnCompletePayment_Click" />
                            </div>
                        </div>
                    </div>
                </div>

                <asp:HiddenField runat="server" ID="hfSettlementOrderId" />
                <asp:HiddenField runat="server" ID="hfAmountDue" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfBaseAmountDue" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfTaxableAmount" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfDefaultTaxPercent" ClientIDMode="Static" />
                <asp:HiddenField runat="server" ID="hfIsCorporateCustomer" ClientIDMode="Static" Value="false" />
                <asp:HiddenField runat="server" ID="hfCurrencySymbol" ClientIDMode="Static" />
            </ContentTemplate>
        </asp:UpdatePanel>
    </div>
</asp:Content>
