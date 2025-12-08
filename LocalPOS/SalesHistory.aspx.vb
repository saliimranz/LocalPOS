Imports System
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Globalization
Imports System.Linq
Imports System.Web.UI
Imports System.Web.UI.WebControls
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Class SalesHistory
    Inherits Page

    Private ReadOnly _posService As New PosService()

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        If Not IsPostBack Then
            lblCashierName.Text = ConfigurationManager.AppSettings("PosDefaultCashier")
            ApplyQuickRange("Today")
            BindSales()
        End If
    End Sub

    Protected Sub ddlDateRange_SelectedIndexChanged(sender As Object, e As EventArgs)
        ApplyQuickRange(ddlDateRange.SelectedValue)
        BindSales()
    End Sub

    Protected Sub btnApplyFilters_Click(sender As Object, e As EventArgs)
        BindSales()
    End Sub

    Private Sub ApplyQuickRange(mode As String)
        Dim today = DateTime.Today
        Select Case mode
            Case "Yesterday"
                Dim value = today.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                txtFromDate.Text = value
                txtToDate.Text = value
            Case "Custom"
                If String.IsNullOrWhiteSpace(txtFromDate.Text) Then
                    txtFromDate.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                End If
                If String.IsNullOrWhiteSpace(txtToDate.Text) Then
                    txtToDate.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                End If
            Case Else
                txtFromDate.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                txtToDate.Text = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
        End Select
    End Sub

    Private Function BuildFilter() As SalesHistoryFilter
        Dim filter As New SalesHistoryFilter()
        If Not String.Equals(ddlDateRange.SelectedValue, "Custom", StringComparison.OrdinalIgnoreCase) Then
            ApplyQuickRange(ddlDateRange.SelectedValue)
        End If
        filter.FromDate = ParseDate(txtFromDate.Text)
        filter.ToDate = ParseDate(txtToDate.Text)

        Dim orderSearch = txtOrderSearch.Text
        If Not String.IsNullOrWhiteSpace(orderSearch) Then
            filter.OrderNumber = orderSearch.Trim()
        Else
            filter.OrderNumber = String.Empty
        End If
        Return filter
    End Function

    Private Shared Function ParseDate(value As String) As DateTime?
        If String.IsNullOrWhiteSpace(value) Then
            Return Nothing
        End If

        Dim parsed As DateTime
        If DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, parsed) Then
            Return parsed.Date
        End If
        If DateTime.TryParse(value, parsed) Then
            Return parsed.Date
        End If
        Return Nothing
    End Function

    Private Sub BindSales()
        Dim filter = BuildFilter()
        Dim orders = _posService.GetSalesHistory(filter)
        If orders Is Nothing Then
            orders = New List(Of SalesHistoryOrder)()
        End If

        rptSales.DataSource = orders
        rptSales.DataBind()

        pnlNoResults.Visible = orders.Count = 0
        litOrderCount.Text = orders.Count.ToString(CultureInfo.InvariantCulture)
        litGrossTotal.Text = FormatCurrency(orders.Sum(Function(o) o.TotalAmount))
        litOutstandingTotal.Text = FormatCurrency(orders.Sum(Function(o) o.OutstandingAmount))
    End Sub

    Protected Sub rptSales_ItemDataBound(sender As Object, e As RepeaterItemEventArgs)
        If e.Item.ItemType <> ListItemType.Item AndAlso e.Item.ItemType <> ListItemType.AlternatingItem Then
            Return
        End If

        Dim order = TryCast(e.Item.DataItem, SalesHistoryOrder)
        If order Is Nothing Then
            Return
        End If

        Dim rptLineItems = TryCast(e.Item.FindControl("rptLineItems"), Repeater)
        If rptLineItems IsNot Nothing Then
            rptLineItems.DataSource = order.LineItems
            rptLineItems.DataBind()
        End If
    End Sub

    Public Function FormatCurrency(value As Object) As String
        Dim amount = ConvertToDecimal(value)
        Return amount.ToString("C", CultureInfo.CurrentCulture)
    End Function

    Public Function GetStatusCss(outstandingObj As Object) As String
        Dim outstanding = ConvertToDecimal(outstandingObj)
        Return If(outstanding > 0D, "status-pill status-pending", "status-pill status-complete")
    End Function

    Public Function GetStatusText(outstandingObj As Object) As String
        Dim outstanding = ConvertToDecimal(outstandingObj)
        Return If(outstanding > 0D, "Pending payment", "Completed")
    End Function

    Public Function GetReceiptUrl(orderIdObj As Object) As String
        Dim orderId As Integer
        If orderIdObj Is Nothing OrElse Not Integer.TryParse(orderIdObj.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, orderId) OrElse orderId <= 0 Then
            Return "#"
        End If

        Return ResolveClientUrl($"~/ReceiptDownload.ashx?mode=sale&orderId={orderId}")
    End Function

    Private Shared Function ConvertToDecimal(value As Object) As Decimal
        If value Is Nothing OrElse value Is DBNull.Value Then
            Return 0D
        End If

        Dim parsed As Decimal
        If Decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, parsed) Then
            Return parsed
        End If
        Return 0D
    End Function
End Class
