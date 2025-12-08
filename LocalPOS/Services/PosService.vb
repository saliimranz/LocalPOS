Imports LocalPOS.LocalPOS.Data
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Services
    Public Class PosService
        Private ReadOnly _productRepository As ProductRepository
        Private ReadOnly _dealerRepository As DealerRepository
        Private ReadOnly _orderRepository As OrderRepository
        Private ReadOnly _heldSaleRepository As HeldSaleRepository

        Public Sub New()
            _productRepository = New ProductRepository()
            _dealerRepository = New DealerRepository()
            _orderRepository = New OrderRepository()
            _heldSaleRepository = New HeldSaleRepository()
        End Sub

        Public Function GetCategories() As IList(Of String)
            Return _productRepository.GetCategories()
        End Function

        Public Function SearchProducts(Optional searchTerm As String = Nothing, Optional category As String = Nothing) As IList(Of Product)
            Return _productRepository.SearchProducts(searchTerm, category)
        End Function

        Public Function GetDealers() As IList(Of Dealer)
            Return _dealerRepository.GetDealers()
        End Function

        Public Function GetDealer(dealerId As Integer) As Dealer
            Return _dealerRepository.GetDealer(dealerId)
        End Function

        Public Function GetProduct(productId As Integer) As Product
            Return _productRepository.GetProduct(productId)
        End Function

        Public Function CreateProduct(request As ProductCreateRequest) As Integer
            Return _productRepository.CreateProduct(request)
        End Function

        Public Sub UpdateProduct(request As ProductUpdateRequest)
            _productRepository.UpdateProduct(request)
        End Sub

        Public Function CompleteCheckout(request As CheckoutRequest) As CheckoutResult
            Return _orderRepository.CreateOrder(request)
        End Function

        Public Function GetCustomerOrders(dealerId As Integer) As IList(Of CustomerOrderSummary)
            Return _orderRepository.GetCustomerOrders(dealerId)
        End Function

        Public Function GetCustomerLedger(dealerId As Integer) As CustomerLedgerReport
            Dim dealer = _dealerRepository.GetDealer(dealerId)
            If dealer Is Nothing Then
                Return Nothing
            End If

            Dim report = _orderRepository.GetCustomerLedger(dealerId)
            If report Is Nothing Then
                report = New CustomerLedgerReport()
            End If

            report.DealerId = dealer.Id
            report.DealerCode = dealer.DealerCode
            report.DealerName = dealer.DealerName

            Return report
        End Function

        Public Function GetSalesHistory(filter As SalesHistoryFilter) As IList(Of SalesHistoryOrder)
            Return _orderRepository.GetSalesHistory(filter)
        End Function

        Public Function GetReceivables() As IList(Of ReceivableReportRow)
            Return _orderRepository.GetReceivables()
        End Function

        Public Function GetOrderReceipt(orderId As Integer) As OrderReceiptData
            Return _orderRepository.GetOrderReceipt(orderId)
        End Function

        Public Function GetSettlementReceipt(orderId As Integer, receiptNumber As String) As PendingPaymentResult
            Return _orderRepository.GetSettlementReceipt(orderId, receiptNumber)
        End Function

        Public Function GetOrderPayments(orderId As Integer) As IList(Of OrderPaymentRecord)
            Return _orderRepository.GetOrderPayments(orderId)
        End Function

        Public Function GetPendingPaymentContext(orderId As Integer) As PendingPaymentContext
            Return _orderRepository.GetPendingPaymentContext(orderId)
        End Function

        Public Function CompletePendingPayment(request As PendingPaymentRequest) As PendingPaymentResult
            If request Is Nothing Then Throw New ArgumentNullException(NameOf(request))
            Dim context = _orderRepository.GetPendingPaymentContext(request.OrderId)
            If context Is Nothing Then
                Throw New InvalidOperationException("Order is already settled or was not found.")
            End If

            request.PaymentAmount = context.OutstandingAmount
            Return _orderRepository.CompletePendingPayment(request, context)
        End Function

        Public Function HoldSale(request As HoldSaleRequest) As Integer
            Return _heldSaleRepository.SaveHeldSale(request)
        End Function

        Public Function GetHeldSales() As IList(Of HeldSaleSummary)
            Return _heldSaleRepository.GetHeldSales()
        End Function

        Public Function GetHeldSale(heldSaleId As Integer) As HeldSaleDetail
            Return _heldSaleRepository.GetHeldSale(heldSaleId)
        End Function

        Public Sub DeleteHeldSale(heldSaleId As Integer)
            _heldSaleRepository.DeleteHeldSale(heldSaleId)
        End Sub
    End Class
End Namespace
