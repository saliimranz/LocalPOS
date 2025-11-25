Imports LocalPOS.Data
Imports LocalPOS.Models

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

        Public Function CompleteCheckout(request As CheckoutRequest) As CheckoutResult
            Return _orderRepository.CreateOrder(request)
        End Function
    End Class
End Namespace
