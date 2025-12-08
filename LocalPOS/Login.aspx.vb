Imports System
Imports System.Web
Imports LocalPOS.LocalPOS.Services
Imports System.Web.UI.HtmlControls

Public Partial Class Login
    Inherits Page

    Private Const AllowedUsername As String = "Alain_lml"
    Private Const AllowedPassword As String = "Lml_786@110@234"

    Protected Overrides Sub OnInit(e As EventArgs)
        MyBase.OnInit(e)
        RegisterLoginStyles()
    End Sub

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        If Not IsPostBack Then
            If AuthManager.IsAuthenticated(Context) Then
                RedirectToHome()
            End If
        End If
    End Sub

    Protected Sub btnLogin_Click(sender As Object, e As EventArgs)
        lblError.CssClass = "alert alert-danger d-none"
        lblError.Text = String.Empty

        Dim username = txtUsername.Text.Trim()
        Dim password = txtPassword.Text

        If ValidateCredentials(username, password) Then
            AuthManager.SignIn(Context, username)
            RedirectToHome()
            Return
        End If

        lblError.Text = "Incorrect username or password."
        lblError.CssClass = "alert alert-danger"
    End Sub

    Private Shared Function ValidateCredentials(username As String, password As String) As Boolean
        Return username.Equals(AllowedUsername, StringComparison.OrdinalIgnoreCase) _
            AndAlso password = AllowedPassword
    End Function

    Private Sub RedirectToHome()
        Dim target = Request.QueryString("returnUrl")
        If String.IsNullOrWhiteSpace(target) Then
            target = "~/Default.aspx"
        End If

        Response.Redirect(target, False)
        Context.ApplicationInstance.CompleteRequest()
    End Sub

    Private Sub RegisterLoginStyles()
        If Header Is Nothing Then
            Return
        End If

        If Header.FindControl("LoginStyles") IsNot Nothing Then
            Return
        End If

        Dim link = New HtmlLink()
        link.ID = "LoginStyles"
        link.Href = ResolveClientUrl("~/Content/login.css")
        link.Attributes("rel") = "stylesheet"
        link.Attributes("type") = "text/css"
        Header.Controls.Add(link)
    End Sub
End Class
