(function () {
    function keepClockUpdated() {
        var dateEl = document.getElementById('currentDate');
        var timeEl = document.getElementById('currentTime');
        if (!dateEl || !timeEl) {
            return;
        }

        function tick() {
            var now = new Date();
            var dateOptions = { weekday: 'long', month: 'long', day: 'numeric', year: 'numeric' };
            dateEl.textContent = now.toLocaleDateString(undefined, dateOptions);
            timeEl.textContent = now.toLocaleTimeString(undefined, { hour12: false });
        }

        tick();
        setInterval(tick, 1000);
    }

    function wirePaymentOptions() {
        var paymentList = document.getElementById('paymentOptions');
        var partialWrapper = document.getElementById('partialAmountWrapper');
        if (!paymentList) {
            return;
        }

        function togglePartial(value) {
            if (!partialWrapper) {
                return;
            }
            if (value === 'Partial') {
                partialWrapper.classList.remove('d-none');
            } else {
                partialWrapper.classList.add('d-none');
            }
        }

        function refreshPartialFromSelection() {
            var checked = paymentList.querySelector('input[type="radio"]:checked');
            if (!checked || checked.dataset.hidden === 'true') {
                togglePartial('');
                return;
            }
            togglePartial(checked.value);
        }

        if (paymentList.dataset.posPaymentWired !== 'true') {
            paymentList.addEventListener('change', function (event) {
                var target = event.target;
                if (!target || target.type !== 'radio') {
                    return;
                }
                togglePartial(target.value);
            });
            paymentList.dataset.posPaymentWired = 'true';
        }

        refreshPartialFromSelection();
    }

    var ajaxHandlersAttached = false;

    function attachAjaxHandlers() {
        if (ajaxHandlersAttached) {
            return;
        }
        if (window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager) {
            var manager = Sys.WebForms.PageRequestManager.getInstance();
            if (!manager) {
                return;
            }
            manager.add_endRequest(function () {
                wirePaymentOptions();
            });
            ajaxHandlersAttached = true;
        }
    }

    window.PosUI = {
        showPaymentModal: function () {
            var modalEl = document.getElementById('paymentModal');
            if (!modalEl || typeof bootstrap === 'undefined') {
                return;
            }
            var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
            modal.show();
        },
        hidePaymentModal: function () {
            var modalEl = document.getElementById('paymentModal');
            if (!modalEl || typeof bootstrap === 'undefined') {
                return;
            }
            var modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) {
                modal.hide();
            }
        }
    };

    document.addEventListener('DOMContentLoaded', function () {
        keepClockUpdated();
        wirePaymentOptions();
        attachAjaxHandlers();
    });
})();
