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
        lblFilterMessage.Text = String.Empty
        ApplyQuickRange(ddlDateRange.SelectedValue)

        If Not String.Equals(ddlDateRange.SelectedValue, "Custom", StringComparison.OrdinalIgnoreCase) Then
            BindSales()
        Else
            lblFilterMessage.Text = "Select your custom range and click Apply."
        End If
    End Sub

    Protected Sub btnApplyFilters_Click(sender As Object, e As EventArgs)
        BindSales()
    End Sub

    Private Sub ApplyQuickRange(mode As String)
        Dim today = DateTime.Today
        Select Case mode
            Case "Yesterday"
                Dim target = today.AddDays(-1)
                SetDateInputs(target, target)
            Case "Custom"
                If String.IsNullOrWhiteSpace(txtFromDate.Text) AndAlso String.IsNullOrWhiteSpace(txtToDate.Text) Then
                    SetDateInputs(today, today)
                End If
            Case Else
                SetDateInputs(today, today)
        End Select
    End Sub

    Private Function BuildFilter() As SalesHistoryFilter
        lblFilterMessage.Text = String.Empty
        Dim filter As New SalesHistoryFilter()
        Dim today = DateTime.Today

        Select Case ddlDateRange.SelectedValue
            Case "Yesterday"
                Dim target = today.AddDays(-1)
                SetDateInputs(target, target)
                filter.FromDate = target
                filter.ToDate = target
            Case "Custom"
                Dim fromDate = ParseDate(txtFromDate.Text)
                Dim toDate = ParseDate(txtToDate.Text)

                If Not fromDate.HasValue OrElse Not toDate.HasValue Then
                    lblFilterMessage.Text = "Select both From and To dates for a custom range."
                    Return Nothing
                End If

                If fromDate.Value > toDate.Value Then
                    lblFilterMessage.Text = "From date cannot be later than To date."
                    Return Nothing
                End If

                filter.FromDate = fromDate
                filter.ToDate = toDate
            Case Else
                SetDateInputs(today, today)
                filter.FromDate = today
                filter.ToDate = today
        End Select

        filter.OrderNumber = txtOrderSearch.Text.Trim()
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
        If filter Is Nothing Then
            rptSales.DataSource = Nothing
            rptSales.DataBind()
            pnlNoResults.Visible = True
            litOrderCount.Text = "0"
            litGrossTotal.Text = FormatCurrency(0D)
            litOutstandingTotal.Text = FormatCurrency(0D)
            Return
        End If

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

    Private Sub SetDateInputs(fromDate As DateTime?, toDate As DateTime?)
        txtFromDate.Text = If(fromDate.HasValue, fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), String.Empty)
        txtToDate.Text = If(toDate.HasValue, toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), String.Empty)
    End Sub

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
