<%@ Page Title="Sign in" Language="VB" MasterPageFile="~/Site.Master" AutoEventWireup="true" CodeBehind="Login.aspx.vb" Inherits="LocalPOS.Login" %>

<asp:Content ID="BodyContent" ContentPlaceHolderID="MainContent" runat="server">
    <div class="login-page">
        <div class="login-shell">
            <div class="login-visual">
                <div class="login-visual-content">
                    <span class="visual-chip">Retail POS Suite</span>
                    <h1>Alanin LML</h1>
                    <p class="visual-copy">Lightweight cashier workflows with instant sync, beautiful receipts, and zero guesswork.</p>
                    <ul class="visual-highlights">
                        <li>Realtime drawer totals</li>
                        <li>Tap-to-print receipts</li>
                        <li>24/7 cloud backups</li>
                    </ul>
                </div>
            </div>
            <div class="login-card">
                <div class="login-card-header">
                    <div class="brand-circle">LP</div>
                    <div>
                        <div class="terminal-name">LocalPOS Console</div>
                        <div class="location-info">Secure cashier access</div>
                    </div>
                </div>

                <h2 class="login-title">Sign in</h2>
                <p class="login-subtitle">Use your operator credentials to continue.</p>

                <asp:Label runat="server" ID="lblError" CssClass="alert alert-danger d-none"></asp:Label>

                <div class="mb-3">
                    <label class="form-label fw-semibold">Username</label>
                    <asp:TextBox runat="server" ID="txtUsername" CssClass="form-control form-control-lg" placeholder="Enter username"></asp:TextBox>
                </div>
                <div class="mb-4">
                    <label class="form-label fw-semibold">Password</label>
                    <asp:TextBox runat="server" ID="txtPassword" CssClass="form-control form-control-lg" TextMode="Password" placeholder="Enter password"></asp:TextBox>
                </div>
                <asp:Button runat="server" ID="btnLogin" CssClass="btn btn-primary btn-login w-100" Text="Sign in" OnClick="btnLogin_Click" />
            </div>
        </div>
    </div>
</asp:Content>
