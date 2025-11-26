Imports LocalPOS.LocalPOS.Data
Imports LocalPOS.LocalPOS.Models

Namespace LocalPOS.Services
    Public Class PosService
        Private ReadOnly _productRepository As ProductRepository
        Private ReadOnly _dealerRepository As DealerRepository
        Private ReadOnly _orderRepository As OrderRepository

        Public Sub New()
            _productRepository = New ProductRepository()
            _dealerRepository = New DealerRepository()
            _orderRepository = New OrderRepository()
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
    End Class
End Namespace
