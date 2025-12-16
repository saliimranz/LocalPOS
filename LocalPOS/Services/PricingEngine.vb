Imports System
Imports System.Collections.Generic
Imports System.Globalization
Imports System.Linq
Imports LocalPOS.LocalPOS.Models

''' <summary>
''' Deterministic pricing engine that separates:
''' - discount intent (scope/type/source)
''' - any internal allocation required for tax correctness
'''
''' Phase 1:
''' - ITEM scoped discounts (percent/amount) (multiple)
''' - SUBTOTAL scoped discounts (percent/amount) (multiple) applied after item discounts
''' - VAT (percent) applied after all discounts on the taxable base
''' </summary>
Public NotInheritable Class PricingEngine
        Private Sub New()
        End Sub

        Public Shared Function Calculate(cartItems As IList(Of CartItem),
                                         vatPercent As Decimal,
                                         discounts As IList(Of CheckoutDiscount)) As OrderPricingResult
            Dim result As New OrderPricingResult()
            If cartItems Is Nothing OrElse cartItems.Count = 0 Then
                Return result
            End If

            Dim safeVatPercent = ClampPercent(vatPercent)
            Dim normalizedDiscounts = NormalizeDiscounts(discounts)

            ' Build lines (current UI has one line per productId).
            For Each item In cartItems
                result.Lines.Add(New PricingLineResult() With {
                    .ProductId = item.ProductId,
                    .Name = item.Name,
                    .Quantity = item.Quantity,
                    .UnitPrice = item.UnitPrice,
                    .LineGross = RoundMoney(Math.Max(0D, item.LineTotal))
                })
            Next

            result.SubtotalGross = RoundMoney(result.Lines.Sum(Function(l) l.LineGross))

            ' 1) Apply ITEM discounts per line (do not touch subtotal discounts).
            For Each line In result.Lines
                Dim lineGrossCents = ToCents(line.LineGross)
                Dim remainingBaseCents = lineGrossCents

                Dim itemDiscounts = normalizedDiscounts.
                    Where(Function(d) d.Scope = DiscountScope.Item AndAlso d.TargetProductId.HasValue AndAlso d.TargetProductId.Value = line.ProductId).
                    OrderByDescending(Function(d) d.Priority).
                    ThenBy(Function(d) d.Ordinal).
                    ToList()

                For Each d In itemDiscounts
                    If remainingBaseCents <= 0 Then Exit For

                    Dim appliedBase = FromCents(remainingBaseCents)
                    Dim discountCents = ComputeDiscountCents(appliedBase, remainingBaseCents, d)
                    If discountCents <= 0 Then Continue For

                    remainingBaseCents = Math.Max(0, remainingBaseCents - discountCents)

                    Dim applied = New AppliedDiscountSummary() With {
                        .Scope = "ITEM",
                        .ValueType = d.ValueTypeText,
                        .Value = d.Value,
                        .AppliedBaseAmount = RoundMoney(appliedBase),
                        .AppliedAmount = FromCents(discountCents),
                        .Source = d.Source,
                        .Reference = d.Reference,
                        .Description = d.Description,
                        .Priority = d.Priority,
                        .IsStackable = d.IsStackable,
                        .TargetProductId = line.ProductId
                    }
                    line.AppliedDiscounts.Add(applied)
                    result.AppliedDiscounts.Add(applied)
                Next

                line.ItemDiscountAmount = RoundMoney(FromCents(lineGrossCents - remainingBaseCents))
                line.NetAfterItemDiscounts = RoundMoney(FromCents(remainingBaseCents))
            Next

            result.ItemDiscountTotal = RoundMoney(result.Lines.Sum(Function(l) l.ItemDiscountAmount))
            result.SubtotalAfterItemDiscounts = RoundMoney(result.Lines.Sum(Function(l) l.NetAfterItemDiscounts))

            ' 2) Apply SUBTOTAL discounts sequentially and allocate each discount across lines for tax.
            Dim workingBases As List(Of Integer) = result.Lines.Select(Function(l) ToCents(l.NetAfterItemDiscounts)).ToList()

            Dim subtotalDiscounts = normalizedDiscounts.
                Where(Function(d) d.Scope = DiscountScope.Subtotal).
                OrderByDescending(Function(d) d.Priority).
                ThenBy(Function(d) d.Ordinal).
                ToList()

            For Each d In subtotalDiscounts
                Dim currentSubtotalCents = workingBases.Sum()
                If currentSubtotalCents <= 0 Then Exit For

                Dim currentSubtotal = FromCents(currentSubtotalCents)
                Dim discountCents = ComputeDiscountCents(currentSubtotal, currentSubtotalCents, d)
                If discountCents <= 0 Then Continue For

                ' Allocate this subtotal discount across lines proportionally to their current bases.
                Dim allocations = AllocateProportionally(discountCents, workingBases)

                Dim appliedSummary = New AppliedDiscountSummary() With {
                    .Scope = "SUBTOTAL",
                    .ValueType = d.ValueTypeText,
                    .Value = d.Value,
                    .AppliedBaseAmount = RoundMoney(currentSubtotal),
                    .AppliedAmount = FromCents(discountCents),
                    .Source = d.Source,
                    .Reference = d.Reference,
                    .Description = d.Description,
                    .Priority = d.Priority,
                    .IsStackable = d.IsStackable
                }
                result.AppliedDiscounts.Add(appliedSummary)

                Dim application As New SubtotalDiscountApplication() With {
                    .Discount = appliedSummary
                }

                For i = 0 To result.Lines.Count - 1
                    Dim allocCents = allocations(i)
                    If allocCents <> 0 Then
                        result.Lines(i).SubtotalDiscountAllocated = RoundMoney(result.Lines(i).SubtotalDiscountAllocated + FromCents(allocCents))
                        application.Allocations.Add(New DiscountAllocation() With {
                            .ProductId = result.Lines(i).ProductId,
                            .BasisAmount = FromCents(workingBases(i)),
                            .AllocatedAmount = FromCents(allocCents)
                        })
                    End If
                    workingBases(i) = Math.Max(0, workingBases(i) - allocCents)
                Next

                result.SubtotalDiscountApplications.Add(application)
            Next

            result.SubtotalDiscountTotal = RoundMoney(result.Lines.Sum(Function(l) l.SubtotalDiscountAllocated))
            result.TaxableSubtotal = RoundMoney(result.SubtotalAfterItemDiscounts - result.SubtotalDiscountTotal)

            ' 3) VAT per line on taxable base (after item + allocated subtotal discounts).
            Dim taxableBasesCents As List(Of Integer) = workingBases.ToList()
            Dim targetVatCents = ToCents(RoundMoney(FromCents(taxableBasesCents.Sum()) * (safeVatPercent / 100D)))

            Dim vatCents As New List(Of Integer)(taxableBasesCents.Count)
            For Each baseCents In taxableBasesCents
                Dim base = FromCents(baseCents)
                Dim vat = RoundMoney(base * (safeVatPercent / 100D))
                vatCents.Add(ToCents(vat))
            Next
            ReconcileCents(targetVatCents, vatCents, Nothing)

            For i = 0 To result.Lines.Count - 1
                result.Lines(i).TaxableBase = RoundMoney(FromCents(taxableBasesCents(i)))
                result.Lines(i).VatAmount = RoundMoney(FromCents(Math.Max(0, vatCents(i))))
            Next

            result.VatPercent = safeVatPercent
            result.VatTotal = RoundMoney(result.Lines.Sum(Function(l) l.VatAmount))
            result.TotalDue = RoundMoney(result.Lines.Sum(Function(l) l.TaxableBase + l.VatAmount))

            ' Compatibility fields.
            result.TotalDiscount = RoundMoney(result.ItemDiscountTotal + result.SubtotalDiscountTotal)
            result.EffectiveDiscountPercent = If(result.SubtotalGross > 0D,
                                                 Decimal.Round((result.TotalDiscount / result.SubtotalGross) * 100D, 2, MidpointRounding.AwayFromZero),
                                                 0D)

            Return result
        End Function

        Private Shared Function NormalizeDiscounts(discounts As IList(Of CheckoutDiscount)) As List(Of NormalizedDiscount)
            Dim normalized As New List(Of NormalizedDiscount)()
            If discounts Is Nothing OrElse discounts.Count = 0 Then
                Return normalized
            End If

            Dim ordinal = 0
            For Each d In discounts
                If d Is Nothing Then Continue For
                Dim scope = ParseScope(d.Scope)
                If scope = DiscountScope.Unknown Then Continue For

                Dim vt = ParseValueType(d.ValueType)
                If vt = DiscountValueType.Unknown Then Continue For

                Dim value = Math.Max(0D, d.Value)
                If vt = DiscountValueType.Percent Then
                    value = ClampPercent(value)
                End If

                Dim source = If(String.IsNullOrWhiteSpace(d.Source), "Unknown", d.Source.Trim())

                normalized.Add(New NormalizedDiscount() With {
                    .Scope = scope,
                    .ValueType = vt,
                    .ValueTypeText = If(vt = DiscountValueType.Percent, "PERCENT", "AMOUNT"),
                    .Value = value,
                    .TargetProductId = d.TargetProductId,
                    .Source = source,
                    .Reference = If(d.Reference, String.Empty),
                    .Description = If(d.Description, String.Empty),
                    .Priority = d.Priority,
                    .IsStackable = d.IsStackable,
                    .Ordinal = ordinal
                })
                ordinal += 1
            Next
            Return normalized
        End Function

        Private Shared Function ComputeDiscountCents(baseAmount As Decimal, baseCents As Integer, d As NormalizedDiscount) As Integer
            If baseCents <= 0 Then Return 0
            If d.Value <= 0D Then Return 0

            Dim discountCents As Integer
            If d.ValueType = DiscountValueType.Percent Then
                Dim computed = RoundMoney(baseAmount * (d.Value / 100D))
                discountCents = ToCents(computed)
            Else
                discountCents = ToCents(RoundMoney(d.Value))
            End If

            If discountCents <= 0 Then Return 0
            If discountCents > baseCents Then discountCents = baseCents
            Return discountCents
        End Function

        Private Shared Function AllocateProportionally(totalCents As Integer, bases As IList(Of Integer)) As List(Of Integer)
            Dim allocations As New List(Of Integer)()
            If bases Is Nothing OrElse bases.Count = 0 OrElse totalCents <= 0 Then
                Return allocations
            End If

            For i = 0 To bases.Count - 1
                allocations.Add(0)
            Next

            Dim baseSum = bases.Sum()
            If baseSum <= 0 Then
                Return allocations
            End If

            For i = 0 To bases.Count - 1
                If i = bases.Count - 1 Then
                    Continue For
                End If
                If bases(i) <= 0 Then
                    Continue For
                End If

                Dim weight = CType(bases(i), Decimal) / CType(baseSum, Decimal)
                Dim alloc = CInt(Decimal.Round(totalCents * weight, 0, MidpointRounding.AwayFromZero))
                If alloc < 0 Then alloc = 0
                If alloc > bases(i) Then alloc = bases(i)
                allocations(i) = alloc
            Next

            Dim maxima = bases.Select(Function(b) FromCents(b)).Select(Function(v) RoundMoney(v)).ToList()
            Dim allocMoney = allocations.Select(Function(a) FromCents(a)).ToList()
            ReconcileRoundedAmounts(FromCents(totalCents), allocMoney, maxima)
            For i = 0 To allocations.Count - 1
                allocations(i) = Math.Min(bases(i), ToCents(allocMoney(i)))
            Next

            ' Final cent reconciliation (guarantee sum == totalCents).
            Dim delta = totalCents - allocations.Sum()
            If delta <> 0 Then
                For i = allocations.Count - 1 To 0 Step -1
                    If delta = 0 Then Exit For
                    Dim current = allocations(i)
                    Dim maxC = bases(i)
                    If delta > 0 Then
                        Dim room = maxC - current
                        If room <= 0 Then Continue For
                        Dim add = Math.Min(delta, room)
                        allocations(i) = current + add
                        delta -= add
                    Else
                        Dim room = current
                        If room <= 0 Then Continue For
                        Dim take = Math.Min(-delta, room)
                        allocations(i) = current - take
                        delta += take
                    End If
                Next
            End If

            Return allocations
        End Function

        Private Shared Sub ReconcileCents(targetCents As Integer, valuesCents As IList(Of Integer), maximaCents As IList(Of Integer))
            If valuesCents Is Nothing OrElse valuesCents.Count = 0 Then Return
            Dim delta = targetCents - valuesCents.Sum()
            If delta = 0 Then Return

            For i = valuesCents.Count - 1 To 0 Step -1
                If delta = 0 Then Exit For

                Dim current = valuesCents(i)
                Dim minC = 0
                Dim maxC = If(maximaCents Is Nothing OrElse i >= maximaCents.Count, Integer.MaxValue, maximaCents(i))

                If delta > 0 Then
                    Dim room = maxC - current
                    If room <= 0 Then Continue For
                    Dim add = Math.Min(delta, room)
                    valuesCents(i) = current + add
                    delta -= add
                Else
                    Dim room = current - minC
                    If room <= 0 Then Continue For
                    Dim take = Math.Min(-delta, room)
                    valuesCents(i) = current - take
                    delta += take
                End If
            Next
        End Sub

        Private Shared Function RoundMoney(amount As Decimal) As Decimal
            Return Decimal.Round(amount, 2, MidpointRounding.AwayFromZero)
        End Function

        Private Shared Function ToCents(amount As Decimal) As Integer
            Return Convert.ToInt32(Decimal.Round(amount * 100D, 0, MidpointRounding.AwayFromZero))
        End Function

        Private Shared Function FromCents(cents As Integer) As Decimal
            Return cents / 100D
        End Function

        Private Shared Function ClampPercent(value As Decimal) As Decimal
            If value < 0D Then Return 0D
            If value > 100D Then Return 100D
            Return value
        End Function

        Private Shared Function ParseScope(value As String) As DiscountScope
            If String.IsNullOrWhiteSpace(value) Then Return DiscountScope.Unknown
            Dim v = value.Trim()
            If v.Equals("ITEM", StringComparison.OrdinalIgnoreCase) Then Return DiscountScope.Item
            If v.Equals("SUBTOTAL", StringComparison.OrdinalIgnoreCase) Then Return DiscountScope.Subtotal
            Return DiscountScope.Unknown
        End Function

        Private Shared Function ParseValueType(value As String) As DiscountValueType
            If String.IsNullOrWhiteSpace(value) Then Return DiscountValueType.Unknown
            Dim v = value.Trim()
            If v.Equals("PERCENT", StringComparison.OrdinalIgnoreCase) OrElse v.Equals("%", StringComparison.OrdinalIgnoreCase) Then Return DiscountValueType.Percent
            If v.Equals("AMOUNT", StringComparison.OrdinalIgnoreCase) OrElse v.Equals("VALUE", StringComparison.OrdinalIgnoreCase) Then Return DiscountValueType.Amount
            Return DiscountValueType.Unknown
        End Function

        Private Enum DiscountScope
            Unknown = 0
            Item = 1
            Subtotal = 2
        End Enum

        Private Enum DiscountValueType
            Unknown = 0
            Percent = 1
            Amount = 2
        End Enum

        Private Class NormalizedDiscount
            Public Property Scope As DiscountScope
            Public Property ValueType As DiscountValueType
            Public Property ValueTypeText As String
            Public Property Value As Decimal
            Public Property TargetProductId As Integer?
            Public Property Source As String
            Public Property Reference As String
            Public Property Description As String
            Public Property Priority As Integer
            Public Property IsStackable As Boolean
            Public Property Ordinal As Integer
        End Class

        ' Shared reconcile helper (Decimal-based) used by AllocateProportionally.
        Private Shared Sub ReconcileRoundedAmounts(targetAmount As Decimal, values As IList(Of Decimal), maxima As IList(Of Decimal))
            If values Is Nothing OrElse values.Count = 0 Then
                Return
            End If

            Dim targetCents = ToCents(targetAmount)
            Dim currentCents = values.Sum(Function(v) ToCents(v))
            Dim delta = targetCents - currentCents
            If delta = 0 Then
                Return
            End If

            For i = values.Count - 1 To 0 Step -1
                If delta = 0 Then Exit For

                Dim current = ToCents(values(i))
                Dim minC = 0
                Dim maxC = If(maxima Is Nothing OrElse i >= maxima.Count, Integer.MaxValue, ToCents(maxima(i)))

                If delta > 0 Then
                    Dim room = maxC - current
                    If room <= 0 Then Continue For
                    Dim add = Math.Min(delta, room)
                    current += add
                    delta -= add
                Else
                    Dim room = current - minC
                    If room <= 0 Then Continue For
                    Dim take = Math.Min(-delta, room)
                    current -= take
                    delta += take
                End If

                values(i) = FromCents(current)
            Next
        End Sub
    End Class

    Public Class OrderPricingResult
        Public Sub New()
            Lines = New List(Of PricingLineResult)()
            AppliedDiscounts = New List(Of AppliedDiscountSummary)()
            SubtotalDiscountApplications = New List(Of SubtotalDiscountApplication)()
        End Sub

        Public Property Lines As List(Of PricingLineResult)

        Public Property SubtotalGross As Decimal
        Public Property ItemDiscountTotal As Decimal
        Public Property SubtotalAfterItemDiscounts As Decimal
        Public Property SubtotalDiscountTotal As Decimal
        Public Property TaxableSubtotal As Decimal
        Public Property VatPercent As Decimal
        Public Property VatTotal As Decimal
        Public Property TotalDue As Decimal

        ' Compatibility / reporting helpers
        Public Property TotalDiscount As Decimal
        Public Property EffectiveDiscountPercent As Decimal

        Public Property AppliedDiscounts As List(Of AppliedDiscountSummary)
        Public Property SubtotalDiscountApplications As List(Of SubtotalDiscountApplication)
    End Class

    Public Class PricingLineResult
        Public Sub New()
            AppliedDiscounts = New List(Of AppliedDiscountSummary)()
        End Sub

        Public Property ProductId As Integer
        Public Property Name As String
        Public Property Quantity As Integer
        Public Property UnitPrice As Decimal
        Public Property LineGross As Decimal

        Public Property ItemDiscountAmount As Decimal
        Public Property NetAfterItemDiscounts As Decimal

        ''' <summary>
        ''' Allocated share of SUBTOTAL discounts, stored separately from item discounts.
        ''' </summary>
        Public Property SubtotalDiscountAllocated As Decimal

        Public Property TaxableBase As Decimal
        Public Property VatAmount As Decimal

        Public Property AppliedDiscounts As List(Of AppliedDiscountSummary)
    End Class

    Public Class SubtotalDiscountApplication
        Public Sub New()
            Allocations = New List(Of DiscountAllocation)()
        End Sub

        Public Property Discount As AppliedDiscountSummary
        Public Property Allocations As List(Of DiscountAllocation)
    End Class

Public Class DiscountAllocation
    Public Property ProductId As Integer
    Public Property BasisAmount As Decimal
    Public Property AllocatedAmount As Decimal
End Class

