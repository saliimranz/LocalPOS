Imports System
Imports System.Collections.Generic
Imports System.Configuration
Imports System.Globalization
Imports System.Linq
Imports LocalPOS.LocalPOS.Models
Imports LocalPOS.LocalPOS.Services

Public Partial Class CustomerProfile
    Inherits Page

    Private ReadOnly _posService As New PosService()

    Private ReadOnly Property CustomerId As Integer
        Get
            Dim raw = Request.QueryString("customerId")
            Dim parsed As Integer
            If Integer.TryParse(raw, parsed) AndAlso parsed >= 0 Then
                Return parsed
            End If
            Return 0
        End Get
    End Property

    Protected Sub Page_Load(ByVal sender As Object, ByVal e As EventArgs) Handles Me.Load
        If Not IsPostBack Then
            lblCashierName.Text = ConfigurationManager.AppSettings("PosDefaultCashier")
            hfCurrencySymbol.Value = CultureInfo.CurrentCulture.NumberFormat.CurrencySymbol
            hfIsCorporateCustomer.Value = "false"
            LoadCustomer()
        End If
    End Sub

    Private Sub LoadCustomer()
        Dim dealer = _posService.GetDealer(CustomerId)
        If dealer Is Nothing Then
            lblPageMessage.CssClass = "d-block mb-3 text-danger"
            lblPageMessage.Text = "Customer not found."
            upOrders.Visible = False
            Return
        End If

        litCustomerName.Text = dealer.DealerName
        litCustomerCode.Text = dealer.DealerCode
        litCustomerContact.Text = If(String.IsNullOrWhiteSpace(dealer.ContactPerson), "-", dealer.ContactPerson)
        litCustomerPhone.Text = If(String.IsNullOrWhiteSpace(dealer.CellNumber), "-", dealer.CellNumber)
        litCustomerCity.Text = If(String.IsNullOrWhiteSpace(dealer.City), "-", dealer.City)

        BindOrders()
    End Sub

        Private Sub BindOrders()
            Dim orders = _posService.GetCustomerOrders(CustomerId)
            If orders Is Nothing Then
                orders = New List(Of CustomerOrderSummary)()
            End If
            rptOrders.DataSource = orders
            rptOrders.DataBind()

            Dim hasOrders = orders IsNot Nothing AndAlso orders.Count > 0
            pnlNoOrders.Visible = Not hasOrders

            Dim totalOutstanding = If(hasOrders, orders.Sum(Function(o) o.Outstanding), 0D)
            Dim totalPaid = If(hasOrders, orders.Sum(Function(o) o.TotalPaid), 0D)

            Dim outstandingFormatted = FormatCurrency(totalOutstanding)
            litOutstandingHeader.Text = outstandingFormatted
            litOutstandingTotal.Text = outstandingFormatted
            litTotalPaid.Text = FormatCurrency(totalPaid)
        End Sub

    Public Function FormatCurrency(value As Object) As String
            Dim amount = ConvertToDecimal(value)
            Return amount.ToString("C", CultureInfo.CurrentCulture)
        End Function

    Public Function GetStatusCss(outstandingObj As Object, statusObj As Object) As String
            Dim outstanding = ConvertToDecimal(outstandingObj)
            If outstanding > 0D Then
                Return "status-pill status-pending"
            End If

            Dim status = If(statusObj IsNot Nothing, statusObj.ToString(), String.Empty)
            If status.IndexOf("pending", StringComparison.OrdinalIgnoreCase) >= 0 Then
                Return "status-pill status-pending"
            End If
            Return "status-pill status-complete"
        End Function

    Public Function GetStatusText(outstandingObj As Object, statusObj As Object) As String
            Dim outstanding = ConvertToDecimal(outstandingObj)
            If outstanding > 0D Then
                Return "Pending payment"
            End If

            Dim status = If(statusObj IsNot Nothing, statusObj.ToString(), String.Empty)
            If String.IsNullOrWhiteSpace(status) Then
                Return "Completed"
            End If
            Return status
        End Function

    Public Function HasOutstanding(value As Object) As Boolean
            Return ConvertToDecimal(value) > 0D
        End Function

        Private Shared Function ConvertToDecimal(value As Object) As Decimal
            If value Is Nothing OrElse value Is DBNull.Value Then
                Return 0D
            End If

            Dim converted As Decimal
            If Decimal.TryParse(value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, converted) Then
                Return converted
            End If
            Return 0D
        End Function

        Protected Sub rptOrders_ItemCommand(source As Object, e As RepeaterCommandEventArgs)
            If e.CommandName.Equals("Settle", StringComparison.OrdinalIgnoreCase) Then
                Dim orderId As Integer
                If Integer.TryParse(Convert.ToString(e.CommandArgument, CultureInfo.InvariantCulture), orderId) Then
                    LoadSettlementModal(orderId)
                End If
            End If
        End Sub

        Private Sub LoadSettlementModal(orderId As Integer)
            lblCheckoutError.Text = String.Empty
            Dim context = _posService.GetPendingPaymentContext(orderId)
            If context Is Nothing Then
                lblPageMessage.CssClass = "d-block mb-3 text-danger"
                lblPageMessage.Text = "This order is already settled or could not be loaded."
                BindOrders()
                Return
            End If

            hfSettlementOrderId.Value = orderId.ToString(CultureInfo.InvariantCulture)
            Dim outstanding = context.OutstandingAmount

            Dim vatPercent = Math.Max(0D, context.VatPercent)
            Dim taxableBase = CalculateTaxableBase(outstanding, vatPercent)

            hfBaseAmountDue.Value = outstanding.ToString(CultureInfo.InvariantCulture)
            hfAmountDue.Value = hfBaseAmountDue.Value
            hfTaxableAmount.Value = taxableBase.ToString(CultureInfo.InvariantCulture)
            hfDefaultTaxPercent.Value = vatPercent.ToString(CultureInfo.InvariantCulture)

            txtModalTaxPercent.Text = vatPercent.ToString("F2", CultureInfo.InvariantCulture)

            litModalSubtotal.Text = FormatCurrency(context.TotalAmount)
            litModalDiscount.Text = FormatCurrency(context.PreviouslyPaid)

            Dim taxAmount = CalculateTaxPortion(context.TotalAmount, vatPercent)
            litModalTax.Text = FormatCurrency(taxAmount)

            Dim outstandingFormatted = FormatCurrency(outstanding)
            litModalTotal.Text = outstandingFormatted
            litAmountDueHeader.Text = outstandingFormatted
            litCashAmountDue.Text = outstandingFormatted
            litCardAmountDue.Text = outstandingFormatted

            litSettlementSummary.Text = $"Paid earlier {FormatCurrency(context.PreviouslyPaid)} &bull; Outstanding {outstandingFormatted}"

            ResetPaymentInputs()
            ScriptManager.RegisterStartupScript(Me, Me.GetType(), "ShowPaymentModal", "PosUI.showPaymentModal();", True)
        End Sub

        Private Shared Function CalculateTaxPortion(totalAmount As Decimal, vatPercent As Decimal) As Decimal
            If vatPercent <= 0D OrElse totalAmount <= 0D Then
                Return 0D
            End If

            Dim divisor = 1D + (vatPercent / 100D)
            If divisor = 0D Then
                Return 0D
            End If

            Dim preTax = totalAmount / divisor
            Return totalAmount - preTax
        End Function

        Private Shared Function CalculateTaxableBase(grossAmount As Decimal, vatPercent As Decimal) As Decimal
            If vatPercent <= 0D Then
                Return grossAmount
            End If

            Dim divisor = 1D + (vatPercent / 100D)
            If divisor = 0D Then
                Return grossAmount
            End If

            Dim baseAmount = grossAmount / divisor
            Return Decimal.Round(baseAmount, 2, MidpointRounding.AwayFromZero)
        End Function

        Protected Sub btnCompletePayment_Click(sender As Object, e As EventArgs)
            Try
                lblCheckoutError.Text = String.Empty
                Dim orderId As Integer
                If Not Integer.TryParse(hfSettlementOrderId.Value, orderId) OrElse orderId <= 0 Then
                    lblCheckoutError.Text = "Select a pending order first."
                    Return
                End If

                Dim method = rblPaymentMethod.SelectedValue
                If String.IsNullOrWhiteSpace(method) Then
                    lblCheckoutError.Text = "Select a payment method."
                    Return
                End If

                Dim context = _posService.GetPendingPaymentContext(orderId)
                If context Is Nothing Then
                    lblCheckoutError.Text = "Order has already been settled."
                    BindOrders()
                    ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HidePaymentModal", "PosUI.hidePaymentModal();", True)
                    Return
                End If

                Dim request As New PendingPaymentRequest() With {
                    .OrderId = orderId,
                    .PaymentMethod = method,
                    .CreatedBy = lblCashierName.Text
                }

                Select Case method
                    Case "Cash"
                        Dim cashReceived As Decimal
                        If Not Decimal.TryParse(txtCashReceived.Text, NumberStyles.Float, CultureInfo.InvariantCulture, cashReceived) OrElse cashReceived <= 0D Then
                            lblCheckoutError.Text = "Enter the cash amount received."
                            Return
                        End If
                        If cashReceived < context.OutstandingAmount Then
                            lblCheckoutError.Text = "Cash received cannot be less than the outstanding amount."
                            Return
                        End If
                        request.CashReceived = cashReceived
                        request.CashChange = cashReceived - context.OutstandingAmount
                    Case "Card"
                        Dim rrn = txtCardRrn.Text.Trim()
                        If String.IsNullOrWhiteSpace(rrn) Then
                            lblCheckoutError.Text = "Bank POS receipt number (RRN) is required."
                            Return
                        End If
                        Dim status = ddlCardStatus.SelectedValue
                        If Not status.Equals("Approved", StringComparison.OrdinalIgnoreCase) Then
                            lblCheckoutError.Text = "Only approved card transactions can be captured."
                            Return
                        End If
                        request.CardRrn = rrn
                        request.CardAuthCode = txtCardAuthCode.Text.Trim()
                        request.CardStatus = status
                    Case Else
                        lblCheckoutError.Text = "Unsupported payment method."
                        Return
                End Select

                request.PaymentAmount = context.OutstandingAmount
                Dim result = _posService.CompletePendingPayment(request)
                Dim receiptPath = SaveSettlementReceipt(result)
                If Not String.IsNullOrWhiteSpace(receiptPath) Then
                    result.ReceiptFilePath = receiptPath
                End If

                BindOrders()
                ResetPaymentInputs()
                hfSettlementOrderId.Value = String.Empty
                ScriptManager.RegisterStartupScript(Me, Me.GetType(), "HidePaymentModal", "PosUI.hidePaymentModal();", True)

                lblPageMessage.CssClass = "d-block mb-3 text-success"
                Dim confirmation = $"Order {result.OrderNumber} marked complete. Receipt {result.ReceiptNumber}."
                If Not String.IsNullOrWhiteSpace(result.ReceiptFilePath) Then
                    confirmation &= " Receipt PDF saved to recipts folder."
                End If
                lblPageMessage.Text = confirmation
            Catch ex As Exception
                lblCheckoutError.Text = $"Unable to complete payment: {ex.Message}"
            End Try
        End Sub

        Private Sub ResetPaymentInputs()
            rblPaymentMethod.SelectedValue = "Cash"
            txtCashReceived.Text = String.Empty
            txtCardRrn.Text = String.Empty
            txtCardAuthCode.Text = String.Empty

            Dim approvedItem = ddlCardStatus.Items.FindByValue("Approved")
            If approvedItem IsNot Nothing Then
                ddlCardStatus.SelectedValue = "Approved"
            ElseIf ddlCardStatus.Items.Count > 0 Then
                ddlCardStatus.SelectedIndex = 0
            End If

            litCashChange.Text = (0D).ToString("C", CultureInfo.CurrentCulture)
        End Sub

    Private Function SaveSettlementReceipt(result As PendingPaymentResult) As String
        Try
            Dim generator = New ReceiptGenerator(Server.MapPath("~"))
            Return generator.GenerateSettlementReceipt(result)
        Catch
            Return String.Empty
        End Try
    End Function
End Class
