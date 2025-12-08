Imports System
Imports System.Web

Namespace LocalPOS.Services
    Public NotInheritable Class AuthManager
        Private Const AuthSessionKey As String = "POS_AUTHENTICATED"
        Private Const UserSessionKey As String = "POS_USERNAME"

        Private Sub New()
        End Sub

        Public Shared Sub SignIn(context As HttpContext, username As String)
            If context Is Nothing OrElse context.Session Is Nothing Then
                Return
            End If

            context.Session(AuthSessionKey) = True
            context.Session(UserSessionKey) = username
        End Sub

        Public Shared Sub SignOut(context As HttpContext)
            If context Is Nothing OrElse context.Session Is Nothing Then
                Return
            End If

            context.Session.Remove(AuthSessionKey)
            context.Session.Remove(UserSessionKey)
        End Sub

        Public Shared Function IsAuthenticated(context As HttpContext) As Boolean
            If context Is Nothing OrElse context.Session Is Nothing Then
                Return False
            End If

            Dim flag = context.Session(AuthSessionKey)
            If flag Is Nothing Then
                Return False
            End If
            Dim isAuthenticated As Boolean
            If Boolean.TryParse(flag.ToString(), isAuthenticated) Then
                Return isAuthenticated
            End If

            If TypeOf flag Is Boolean Then
                Return CBool(flag)
            End If

            Return False
        End Function

        Public Shared Function GetCurrentUsername(context As HttpContext) As String
            If context Is Nothing OrElse context.Session Is Nothing Then
                Return String.Empty
            End If

            Dim value = context.Session(UserSessionKey)
            Return If(value IsNot Nothing, value.ToString(), String.Empty)
        End Function
    End Class
End Namespace
