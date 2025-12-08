Option Strict On
Option Explicit On

Partial Public Class SalesHistory

    '''<summary>
    '''Control lnkBackToPos.
    '''</summary>
    Protected WithEvents lnkBackToPos As Global.System.Web.UI.WebControls.HyperLink

    '''<summary>
    '''Control lblCashierName.
    '''</summary>
    Protected WithEvents lblCashierName As Global.System.Web.UI.WebControls.Label

    '''<summary>
    '''Control upSalesHistory.
    '''</summary>
    Protected WithEvents upSalesHistory As Global.System.Web.UI.UpdatePanel

    '''<summary>
    '''Control ddlDateRange.
    '''</summary>
    Protected WithEvents ddlDateRange As Global.System.Web.UI.WebControls.DropDownList

    '''<summary>
    '''Control txtFromDate.
    '''</summary>
    Protected WithEvents txtFromDate As Global.System.Web.UI.WebControls.TextBox

    '''<summary>
    '''Control txtToDate.
    '''</summary>
    Protected WithEvents txtToDate As Global.System.Web.UI.WebControls.TextBox

    '''<summary>
    '''Control txtOrderSearch.
    '''</summary>
    Protected WithEvents txtOrderSearch As Global.System.Web.UI.WebControls.TextBox

    '''<summary>
    '''Control btnApplyFilters.
    '''</summary>
    Protected WithEvents btnApplyFilters As Global.System.Web.UI.WebControls.Button

    '''<summary>
    '''Control lblFilterMessage.
    '''</summary>
    Protected WithEvents lblFilterMessage As Global.System.Web.UI.WebControls.Label

    '''<summary>
    '''Control litOrderCount.
    '''</summary>
    Protected WithEvents litOrderCount As Global.System.Web.UI.WebControls.Literal

    '''<summary>
    '''Control litGrossTotal.
    '''</summary>
    Protected WithEvents litGrossTotal As Global.System.Web.UI.WebControls.Literal

    '''<summary>
    '''Control litOutstandingTotal.
    '''</summary>
    Protected WithEvents litOutstandingTotal As Global.System.Web.UI.WebControls.Literal

    '''<summary>
    '''Control rptSales.
    '''</summary>
    Protected WithEvents rptSales As Global.System.Web.UI.WebControls.Repeater

    '''<summary>
    '''Control pnlNoResults.
    '''</summary>
    Protected WithEvents pnlNoResults As Global.System.Web.UI.WebControls.Panel

End Class
