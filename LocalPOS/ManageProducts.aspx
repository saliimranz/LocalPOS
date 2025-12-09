<%@ Page Title="Manage Products" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="false" CodeBehind="ManageProducts.aspx.vb" Inherits="LocalPOS.ManageProducts" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="product-admin-shell">
        <div class="page-hero product-hero">
            <div class="hero-content">
                <p class="hero-label">Catalog workspace</p>
                <h1>Product catalog</h1>
                <p class="hero-subtitle">Create spare parts, upload media, and keep SKU data aligned with what appears on the POS.</p>
                <div class="hero-actions">
                    <asp:HyperLink runat="server" ID="lnkBackToPos" NavigateUrl="~/Default.aspx" CssClass="btn btn-light btn-back-link">Back to POS</asp:HyperLink>
                    <a href="mailto:support@localpos.app" class="btn btn-outline-light btn-back-link">Need help?</a>
                </div>
            </div>
            <div class="hero-stats">
                <div class="hero-stat-card">
                    <span class="hero-stat-label">Today's session</span>
                    <strong class="hero-stat-value"><asp:Literal runat="server" ID="litCatalogUpdated"></asp:Literal></strong>
                </div>
                <div class="hero-stat-card">
                    <span class="hero-stat-label">SKU drafts</span>
                    <strong class="hero-stat-value"><asp:Literal runat="server" ID="litSkuDraftCount"></asp:Literal></strong>
                </div>
            </div>
        </div>

        <asp:Panel runat="server" ID="pnlStatus" CssClass="alert" Visible="false">
            <asp:Literal runat="server" ID="litStatus"></asp:Literal>
        </asp:Panel>
        <asp:Panel runat="server" ID="pnlErrors" CssClass="alert alert-danger" Visible="false">
            <asp:Literal runat="server" ID="litErrors"></asp:Literal>
        </asp:Panel>

        <div class="product-admin-card">
            <h2 class="section-title">Spare part info</h2>
            <div class="form-grid two-column">
                <div class="form-group">
                    <label for="txtSparePartCode">Spare part code *</label>
                    <asp:TextBox runat="server" ID="txtSparePartCode" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtDisplayName">Default display name</label>
                    <asp:TextBox runat="server" ID="txtDisplayName" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtCategory">Category</label>
                    <asp:TextBox runat="server" ID="txtCategory" CssClass="form-control" />
                </div>
                <div class="form-group">
                    <label for="txtBrand">Brand</label>
                    <asp:TextBox runat="server" ID="txtBrand" CssClass="form-control" />
                </div>
            </div>
            <div class="form-group">
                <label for="txtProductDescription">Description</label>
                <asp:TextBox runat="server" ID="txtProductDescription" CssClass="form-control" TextMode="MultiLine" Rows="4" />
            </div>
        </div>

        <div class="product-admin-card">
            <div class="section-heading">
                <div>
                    <h2>SKUs &amp; media</h2>
                    <p class="text-muted mb-0">Add each sellable SKU with pricing, VAT, and stock thresholds.</p>
                </div>
                <asp:Button runat="server" ID="btnAddSkuRow" CssClass="btn btn-outline-primary" Text="Add SKU row" OnClick="btnAddSkuRow_Click" CausesValidation="false" />
            </div>
            <asp:Repeater runat="server" ID="rptSkus" OnItemDataBound="rptSkus_ItemDataBound">
                <ItemTemplate>
                    <div class="sku-editor">
                        <h3>SKU #<%# Container.ItemIndex + 1 %></h3>
                        <asp:HiddenField runat="server" ID="hfStoredImage" />
                        <div class="form-grid three-column">
                            <div class="form-group">
                                <label>SKU code *</label>
                                <asp:TextBox runat="server" ID="txtSkuCode" CssClass="form-control" />
                            </div>
                            <div class="form-group">
                                <label>Display name</label>
                                <asp:TextBox runat="server" ID="txtSkuDisplayName" CssClass="form-control" />
                            </div>
                            <div class="form-group">
                                <label>Retail price</label>
                                <asp:TextBox runat="server" ID="txtSkuPrice" CssClass="form-control" TextMode="Number" />
                            </div>
                        </div>
                        <div class="form-grid three-column">
                            <div class="form-group">
                                <label>Tax rate (%)</label>
                                <asp:TextBox runat="server" ID="txtSkuTax" CssClass="form-control" TextMode="Number" />
                            </div>
                            <div class="form-group">
                                <label>Stock quantity</label>
                                <asp:TextBox runat="server" ID="txtSkuStock" CssClass="form-control" TextMode="Number" />
                            </div>
                            <div class="form-group">
                                <label>Min threshold</label>
                                <asp:TextBox runat="server" ID="txtSkuThreshold" CssClass="form-control" TextMode="Number" />
                            </div>
                        </div>
                        <div class="form-group">
                            <label>Description</label>
                            <asp:TextBox runat="server" ID="txtSkuDescription" CssClass="form-control" TextMode="MultiLine" Rows="3" />
                        </div>
                        <div class="form-group">
                            <label>Image</label>
                            <asp:FileUpload runat="server" ID="fuSkuImage" CssClass="form-control" />
                            <div class="product-media-note">
                                <asp:Literal runat="server" ID="litImageName"></asp:Literal>
                            </div>
                        </div>
                    </div>
                </ItemTemplate>
            </asp:Repeater>
            <div class="product-media-note">Images are stored in the <code>product media</code> folder and reused across POS pages.</div>
        </div>

        <div class="product-admin-actions">
            <asp:Button runat="server" ID="btnSaveProduct" CssClass="btn btn-primary btn-lg" Text="Save product" OnClick="btnSaveProduct_Click" />
        </div>
    </div>
</asp:Content>
