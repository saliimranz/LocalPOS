<%@ Page Title="Sign in" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.vb" Inherits="LocalPOS.Login" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="login-shell">
        <div class="pos-header login-header">
            <div>
                <div class="terminal-name">LocalPOS Console</div>
                <div class="location-info">Secure cashier access</div>
            </div>
        </div>

        <div class="login-card">
            <h4 class="mb-3">Welcome back</h4>
            <p class="text-muted mb-4">Enter your operator credentials to continue.</p>

            <asp:Label runat="server" ID="lblError" CssClass="alert alert-danger d-none"></asp:Label>

            <div class="mb-3">
                <label class="form-label fw-semibold">Username</label>
                <asp:TextBox runat="server" ID="txtUsername" CssClass="form-control form-control-lg" placeholder="Enter username"></asp:TextBox>
            </div>
            <div class="mb-3">
                <label class="form-label fw-semibold">Password</label>
                <asp:TextBox runat="server" ID="txtPassword" CssClass="form-control form-control-lg" TextMode="Password" placeholder="Enter password"></asp:TextBox>
            </div>
            <asp:Button runat="server" ID="btnLogin" CssClass="btn btn-primary btn-login w-100" Text="Sign in" OnClick="btnLogin_Click" />
        </div>
    </div>
</asp:Content>
