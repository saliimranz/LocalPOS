Option Strict On
Option Explicit On

Partial Public Class CustomerProfile

    Protected WithEvents lnkBackToPos As Global.System.Web.UI.WebControls.HyperLink

    Protected WithEvents litCustomerName As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litCustomerCode As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litOutstandingHeader As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents lblCashierName As Global.System.Web.UI.WebControls.Label

    Protected WithEvents lblPageMessage As Global.System.Web.UI.WebControls.Label

    Protected WithEvents litCustomerContact As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litCustomerPhone As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litCustomerCity As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litTotalPaid As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litOutstandingTotal As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents lnkDownloadLedger As Global.System.Web.UI.WebControls.HyperLink

    Protected WithEvents upOrders As Global.System.Web.UI.UpdatePanel

    Protected WithEvents rptOrders As Global.System.Web.UI.WebControls.Repeater

    Protected WithEvents pnlNoOrders As Global.System.Web.UI.WebControls.Panel

    Protected WithEvents litAmountDueHeader As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litSettlementSummary As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litModalSubtotal As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litModalDiscount As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litModalTax As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litModalTotal As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents txtModalTaxPercent As Global.System.Web.UI.WebControls.TextBox

    Protected WithEvents rblPaymentMethod As Global.System.Web.UI.WebControls.RadioButtonList

    Protected WithEvents litCashAmountDue As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents litCashChange As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents txtCashReceived As Global.System.Web.UI.WebControls.TextBox

    Protected WithEvents litCardAmountDue As Global.System.Web.UI.WebControls.Literal

    Protected WithEvents txtCardRrn As Global.System.Web.UI.WebControls.TextBox

    Protected WithEvents txtCardAuthCode As Global.System.Web.UI.WebControls.TextBox

    Protected WithEvents ddlCardStatus As Global.System.Web.UI.WebControls.DropDownList

    Protected WithEvents lblCheckoutError As Global.System.Web.UI.WebControls.Label

    Protected WithEvents btnCancelPayment As Global.System.Web.UI.WebControls.Button

    Protected WithEvents btnCompletePayment As Global.System.Web.UI.WebControls.Button

    Protected WithEvents hfSettlementOrderId As Global.System.Web.UI.WebControls.HiddenField

    Protected WithEvents hfAmountDue As Global.System.Web.UI.WebControls.HiddenField

    Protected WithEvents hfBaseAmountDue As Global.System.Web.UI.WebControls.HiddenField

    Protected WithEvents hfTaxableAmount As Global.System.Web.UI.WebControls.HiddenField

    Protected WithEvents hfDefaultTaxPercent As Global.System.Web.UI.WebControls.HiddenField

    Protected WithEvents hfIsCorporateCustomer As Global.System.Web.UI.WebControls.HiddenField

    Protected WithEvents hfCurrencySymbol As Global.System.Web.UI.WebControls.HiddenField
End Class
