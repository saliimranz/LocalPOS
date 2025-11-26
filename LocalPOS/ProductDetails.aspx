<%@ Page Title="Product Details" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="false" CodeBehind="ProductDetails.aspx.vb" Inherits="LocalPOS.ProductDetails" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="product-details-shell">
        <div class="product-admin-header">
            <div>
                <h1><asp:Literal runat="server" ID="litProductName"></asp:Literal></h1>
                <p class="text-muted mb-0">SKU <asp:Literal runat="server" ID="litSkuCode"></asp:Literal></p>
            </div>
            <div class="product-admin-actions">
                <asp:HyperLink runat="server" ID="lnkBackToCatalog" NavigateUrl="~/ManageProducts.aspx" CssClass="btn btn-outline-secondary">Manage catalog</asp:HyperLink>
                <asp:HyperLink runat="server" ID="lnkBackToPos" NavigateUrl="~/Default.aspx" CssClass="btn btn-outline-secondary">Back to POS</asp:HyperLink>
            </div>
        </div>

        <asp:Panel runat="server" ID="pnlDetailStatus" CssClass="alert" Visible="false">
            <asp:Literal runat="server" ID="litDetailStatus"></asp:Literal>
        </asp:Panel>

        <asp:Panel runat="server" ID="pnlNotFound" CssClass="alert alert-danger" Visible="false">
            The requested product could not be found. Please return to the catalog and try again.
        </asp:Panel>

        <asp:Panel runat="server" ID="pnlDetails">
            <div class="product-details-grid">
                <div class="product-details-card">
                    <asp:Image runat="server" ID="imgProduct" AlternateText="Product image" />
                    <div class="product-summary">
                        <p><strong>Category:</strong> <asp:Literal runat="server" ID="litCategory"></asp:Literal></p>
                        <p><strong>Brand:</strong> <asp:Literal runat="server" ID="litBrand"></asp:Literal></p>
                        <p><strong>Current stock:</strong> <asp:Literal runat="server" ID="litStock"></asp:Literal></p>
                        <p><strong>Threshold:</strong> <asp:Literal runat="server" ID="litThreshold"></asp:Literal></p>
                        <p><strong>Unit price:</strong> <asp:Literal runat="server" ID="litUnitPrice"></asp:Literal></p>
                        <p><strong>Tax rate:</strong> <asp:Literal runat="server" ID="litTax"></asp:Literal></p>
                    </div>
                    <p class="mt-3">
                        <asp:Literal runat="server" ID="litProductDescription"></asp:Literal>
                    </p>
                </div>

                <div class="product-details-card">
                    <h2>Edit SKU</h2>
                    <asp:HiddenField runat="server" ID="hfProductId" />
                    <div class="form-grid two-column">
                        <div class="form-group">
                            <label for="txtEditDisplayName">Display name</label>
                            <asp:TextBox runat="server" ID="txtEditDisplayName" CssClass="form-control" />
                        </div>
                        <div class="form-group">
                            <label for="txtEditPrice">Unit price</label>
                            <asp:TextBox runat="server" ID="txtEditPrice" CssClass="form-control" TextMode="Number" />
                        </div>
                        <div class="form-group">
                            <label for="txtEditTaxRate">Tax rate (%)</label>
                            <asp:TextBox runat="server" ID="txtEditTaxRate" CssClass="form-control" TextMode="Number" />
                        </div>
                        <div class="form-group">
                            <label for="txtEditStock">Stock quantity</label>
                            <asp:TextBox runat="server" ID="txtEditStock" CssClass="form-control" TextMode="Number" />
                        </div>
                        <div class="form-group">
                            <label for="txtEditThreshold">Min threshold</label>
                            <asp:TextBox runat="server" ID="txtEditThreshold" CssClass="form-control" TextMode="Number" />
                        </div>
                    </div>
                    <div class="form-group">
                        <label for="txtEditDescription">Description</label>
                        <asp:TextBox runat="server" ID="txtEditDescription" CssClass="form-control" TextMode="MultiLine" Rows="4" />
                    </div>
                    <div class="form-group">
                        <label for="fuEditImage">Replace image</label>
                        <asp:FileUpload runat="server" ID="fuEditImage" CssClass="form-control" />
                        <div class="product-media-note">
                            <asp:Literal runat="server" ID="litExistingImage"></asp:Literal>
                        </div>
                    </div>
                    <div class="product-admin-actions">
                        <asp:Button runat="server" ID="btnSaveChanges" CssClass="btn btn-primary" Text="Save changes" OnClick="btnSaveChanges_Click" />
                    </div>
                </div>
            </div>
        </asp:Panel>
    </div>
</asp:Content>
