<%@ Page Title="Add Customer" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="false" CodeBehind="AddCustomer.aspx.vb" Inherits="LocalPOS.AddCustomer" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="product-admin-shell">
        <div class="product-admin-header">
            <div>
                <h1><asp:Literal runat="server" ID="litFormTitle"></asp:Literal></h1>
                <p class="text-muted mb-0"><asp:Literal runat="server" ID="litFormSubtitle"></asp:Literal></p>
            </div>
            <div class="product-admin-actions">
                <asp:HyperLink runat="server" ID="lnkBackToCustomers" CssClass="btn btn-outline-secondary" NavigateUrl="~/Default.aspx">Back to POS</asp:HyperLink>
            </div>
        </div>

        <asp:Panel runat="server" ID="pnlStatus" CssClass="alert" Visible="false">
            <asp:Literal runat="server" ID="litStatus"></asp:Literal>
        </asp:Panel>
        <asp:Panel runat="server" ID="pnlErrors" CssClass="alert alert-danger" Visible="false">
            <asp:Literal runat="server" ID="litErrors"></asp:Literal>
        </asp:Panel>

        <asp:HiddenField runat="server" ID="hfDealerId" />
        <asp:HiddenField runat="server" ID="hfReturnUrl" />

        <div class="product-admin-card">
            <h2 class="section-title">Business details</h2>
            <div class="form-grid two-column">
                <div class="form-group">
                    <label for="txtDealerName">Customer name *</label>
                    <asp:TextBox runat="server" ID="txtDealerName" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtDealerCode">Customer code *</label>
                    <asp:TextBox runat="server" ID="txtDealerCode" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtDealerOldCode">Legacy code</label>
                    <asp:TextBox runat="server" ID="txtDealerOldCode" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtContactPerson">Primary contact person</label>
                    <asp:TextBox runat="server" ID="txtContactPerson" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtSalesPerson">Sales person</label>
                    <asp:TextBox runat="server" ID="txtSalesPerson" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtSalesPersonId">Sales person ID</label>
                    <asp:TextBox runat="server" ID="txtSalesPersonId" CssClass="form-control" TextMode="Number" />
                </div>
                <div class="form-group">
                    <label for="txtTypeCode">Customer type</label>
                    <asp:TextBox runat="server" ID="txtTypeCode" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtParentId">Parent account ID</label>
                    <asp:TextBox runat="server" ID="txtParentId" CssClass="form-control" TextMode="Number" />
                </div>
                <div class="form-group">
                    <label for="txtBranch">Branch</label>
                    <asp:TextBox runat="server" ID="txtBranch" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtBranchCode">Branch code</label>
                    <asp:TextBox runat="server" ID="txtBranchCode" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtDealerInvestment">Investment notes</label>
                    <asp:TextBox runat="server" ID="txtDealerInvestment" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtDefaultDiscountPercentage">Default Discount %</label>
                    <asp:TextBox runat="server" ID="txtDefaultDiscountPercentage" CssClass="form-control" TextMode="Number" />
                    <small class="text-muted">Optional. Applied automatically in POS.</small>
                </div>
                <div class="form-group">
                    <label for="txtSalesTerritory">Sales territory code</label>
                    <asp:TextBox runat="server" ID="txtSalesTerritory" CssClass="form-control" TextMode="Number" />
                </div>
            </div>
            <div class="customer-toggle-row">
                <label class="form-check form-switch">
                    <asp:CheckBox runat="server" ID="chkActive" CssClass="form-check-input" />
                    <span class="form-check-label">Customer is active</span>
                </label>
                <label class="form-check form-switch">
                    <asp:CheckBox runat="server" ID="chkSmsAlerts" CssClass="form-check-input" />
                    <span class="form-check-label">Enable SMS alerts</span>
                </label>
                <label class="form-check form-switch">
                    <asp:CheckBox runat="server" ID="chkAppLogin" CssClass="form-check-input" />
                    <span class="form-check-label">Allow app login</span>
                </label>
            </div>
        </div>

        <div class="product-admin-card">
            <h2 class="section-title">Contact information</h2>
            <div class="form-group">
                <label for="txtAddress">Billing address</label>
                <asp:TextBox runat="server" ID="txtAddress" CssClass="form-control" TextMode="MultiLine" Rows="3" />
            </div>
            <div class="form-grid two-column">
                <div class="form-group">
                    <label for="txtCity">City</label>
                    <asp:TextBox runat="server" ID="txtCity" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtState">State/Province</label>
                    <asp:TextBox runat="server" ID="txtState" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtCountry">Country</label>
                    <asp:TextBox runat="server" ID="txtCountry" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtWebsite">Website</label>
                    <asp:TextBox runat="server" ID="txtWebsite" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtEmail">Primary email</label>
                    <asp:TextBox runat="server" ID="txtEmail" CssClass="form-control" TextMode="Email" />
                </div>
                <div class="form-group">
                    <label for="txtEmailCustomer">Alternate email</label>
                    <asp:TextBox runat="server" ID="txtEmailCustomer" CssClass="form-control" TextMode="Email" />
                </div>
                <div class="form-group">
                    <label for="txtPhonePrimary">Primary phone</label>
                    <asp:TextBox runat="server" ID="txtPhonePrimary" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtPhoneAlt1">Phone 2</label>
                    <asp:TextBox runat="server" ID="txtPhoneAlt1" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtPhoneAlt2">Phone 3</label>
                    <asp:TextBox runat="server" ID="txtPhoneAlt2" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtPhoneAlt3">Phone 4</label>
                    <asp:TextBox runat="server" ID="txtPhoneAlt3" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtAltNumbers">Other numbers</label>
                    <asp:TextBox runat="server" ID="txtAltNumbers" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtContactNotes">Contact notes</label>
                    <asp:TextBox runat="server" ID="txtContactNotes" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtEmailNotes">Email notes</label>
                    <asp:TextBox runat="server" ID="txtEmailNotes" CssClass="form-control" />
                </div>
            </div>
        </div>

        <div class="product-admin-card">
            <h2 class="section-title">Compliance &amp; portal access</h2>
            <div class="form-grid two-column">
                <div class="form-group">
                    <label for="txtCnic">CNIC</label>
                    <asp:TextBox runat="server" ID="txtCnic" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtCnicExpiry">CNIC expiry</label>
                    <asp:TextBox runat="server" ID="txtCnicExpiry" CssClass="form-control" TextMode="Date" />
                </div>
                <div class="form-group">
                    <label for="txtNtn">NTN</label>
                    <asp:TextBox runat="server" ID="txtNtn" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtStn">STN</label>
                    <asp:TextBox runat="server" ID="txtStn" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtLogin">Portal login</label>
                    <asp:TextBox runat="server" ID="txtLogin" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtPassword">Portal password</label>
                    <asp:TextBox runat="server" ID="txtPassword" CssClass="form-control" TextMode="Password" />
                </div>
            </div>
        </div>

        <div class="product-admin-card">
            <h2 class="section-title">Supporting documents</h2>
            <div class="form-grid two-column">
                <div class="form-group">
                    <label for="fuTradeLicense">Trade license / agreement</label>
                    <asp:FileUpload runat="server" ID="fuTradeLicense" CssClass="form-control" />
                    <small class="customer-doc-note"><asp:Literal runat="server" ID="litTradeLicenseStatus"></asp:Literal></small>
                </div>
                <div class="form-group">
                    <label for="fuCnic">CNIC copy</label>
                    <asp:FileUpload runat="server" ID="fuCnic" CssClass="form-control" />
                    <small class="customer-doc-note"><asp:Literal runat="server" ID="litCnicStatus"></asp:Literal></small>
                </div>
                <div class="form-group">
                    <label for="fuNtn">NTN certificate</label>
                    <asp:FileUpload runat="server" ID="fuNtn" CssClass="form-control" />
                    <small class="customer-doc-note"><asp:Literal runat="server" ID="litNtnStatus"></asp:Literal></small>
                </div>
                <div class="form-group">
                    <label for="fuLogo">Company logo</label>
                    <asp:FileUpload runat="server" ID="fuLogo" CssClass="form-control" />
                    <small class="customer-doc-note"><asp:Literal runat="server" ID="litLogoStatus"></asp:Literal></small>
                </div>
            </div>
        </div>

        <div class="product-admin-actions">
            <asp:Button runat="server" ID="btnSaveCustomer" CssClass="btn btn-primary btn-lg" Text="Save customer" OnClick="btnSaveCustomer_Click" />
            <asp:HyperLink runat="server" ID="lnkCancel" CssClass="btn btn-outline-secondary" NavigateUrl="~/Default.aspx">Cancel</asp:HyperLink>
        </div>
    </div>
</asp:Content>
