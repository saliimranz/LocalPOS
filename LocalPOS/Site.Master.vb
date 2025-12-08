Imports System
Imports System.Web
Imports LocalPOS.LocalPOS.Services

Public Class SiteMaster
    Inherits MasterPage

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        EnforceAuthentication()
    End Sub

    Private Sub EnforceAuthentication()
        Dim currentPath = Page.AppRelativeVirtualPath
        If currentPath Is Nothing Then
            Return
        End If

        Dim isLoginPage = currentPath.Equals("~/Login.aspx", StringComparison.OrdinalIgnoreCase)
        If isLoginPage Then
            Return
        End If

        If Not AuthManager.IsAuthenticated(Context) Then
            Dim target = HttpUtility.UrlEncode(Request.RawUrl)
            Response.Redirect($"~/Login.aspx?returnUrl={target}", False)
            Context.ApplicationInstance.CompleteRequest()
        End If
    End Sub
End Class