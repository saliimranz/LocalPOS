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

        paymentList.addEventListener('change', function (event) {
            var target = event.target;
            if (!target || target.type !== 'radio') {
                return;
            }
            togglePartial(target.value);
        });

        var checked = paymentList.querySelector('input[type="radio"]:checked');
        if (checked) {
            togglePartial(checked.value);
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
    });
})();
