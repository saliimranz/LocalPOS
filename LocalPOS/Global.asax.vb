Imports System.Globalization
Imports System.Threading
Imports System.Web.Optimization

Public Class Global_asax
    Inherits HttpApplication

    Private Shared ReadOnly AedCulture As CultureInfo = CreateAedCulture()

    Private Shared Function CreateAedCulture() As CultureInfo
        Dim culture = CType(CultureInfo.CreateSpecificCulture("en-AE").Clone(), CultureInfo)
        culture.NumberFormat.CurrencySymbol = "AED"
        culture.NumberFormat.CurrencyDecimalDigits = 2
        Return culture
    End Function

    Private Shared Sub ApplyPosCulture()
        Dim requestCulture = CType(AedCulture.Clone(), CultureInfo)
        Thread.CurrentThread.CurrentCulture = requestCulture
        Thread.CurrentThread.CurrentUICulture = requestCulture
    End Sub

    Sub Application_Start(sender As Object, e As EventArgs)
        ' Fires when the application is started
        RouteConfig.RegisterRoutes(RouteTable.Routes)
        BundleConfig.RegisterBundles(BundleTable.Bundles)

        CultureInfo.DefaultThreadCurrentCulture = AedCulture
        CultureInfo.DefaultThreadCurrentUICulture = AedCulture
        ApplyPosCulture()
    End Sub

    Sub Application_BeginRequest(sender As Object, e As EventArgs)
        ApplyPosCulture()
    End Sub
End Class