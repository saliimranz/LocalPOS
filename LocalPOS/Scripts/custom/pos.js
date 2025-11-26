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

    function parseDecimal(value) {
        var parsed = parseFloat(value);
        return isNaN(parsed) ? 0 : parsed;
    }

    function getHiddenDecimal(id) {
        var field = document.getElementById(id);
        if (!field) {
            return 0;
        }
        return parseDecimal(field.value);
    }

    function setHiddenDecimal(id, value) {
        var field = document.getElementById(id);
        if (!field) {
            return;
        }
        var safeValue = typeof value === 'number' && isFinite(value) ? value : 0;
        field.value = safeValue.toFixed(2);
    }

    function formatCurrency(value) {
        var symbolField = document.getElementById('hfCurrencySymbol');
        var symbol = symbolField ? symbolField.value : '';
        var numericValue = Number(value || 0);
        var formatted = numericValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        return symbol ? symbol + ' ' + formatted : formatted;
    }

    function refreshAmountDisplays(amount) {
        var formatted = formatCurrency(amount);
        document.querySelectorAll('[data-amount-due-display]').forEach(function (node) {
            node.textContent = formatted;
        });
    }

    function getSelectedPaymentMethod() {
        var list = document.getElementById('rblPaymentMethod');
        if (!list) {
            return null;
        }
        var checked = list.querySelector('input[type="radio"]:checked');
        return checked ? checked.value : null;
    }

    function showPaymentPanel(method) {
        var panels = document.querySelectorAll('[data-payment-panel]');
        if (!panels.length) {
            return;
        }
        var targetMethod = method || getSelectedPaymentMethod() || panels[0].getAttribute('data-payment-panel');
        panels.forEach(function (panel) {
            var panelMethod = panel.getAttribute('data-payment-panel');
            if (panelMethod === targetMethod) {
                panel.classList.remove('d-none');
            } else {
                panel.classList.add('d-none');
            }
        });
    }

    function toggleCorporatePartialInput() {
        var wrapper = document.getElementById('corporatePartialWrapper');
        var typeList = document.getElementById('rblCorporatePaymentType');
        var isCorporateField = document.getElementById('hfIsCorporateCustomer');
        if (!wrapper || !typeList || !isCorporateField || isCorporateField.value !== 'true') {
            if (wrapper) {
                wrapper.classList.add('d-none');
            }
            return;
        }

        var selected = typeList.querySelector('input[type="radio"]:checked');
        var shouldShow = selected && selected.value === 'Partial';
        wrapper.classList.toggle('d-none', !shouldShow);
    }

    function updateAmountDueFromInputs() {
        var baseAmount = getHiddenDecimal('hfBaseAmountDue');
        if (baseAmount <= 0) {
            baseAmount = getHiddenDecimal('hfAmountDue');
        }
        var isCorporateField = document.getElementById('hfIsCorporateCustomer');
        var amount = baseAmount;

        if (isCorporateField && isCorporateField.value === 'true') {
            var typeList = document.getElementById('rblCorporatePaymentType');
            var partialInput = document.getElementById('txtCorporatePartialAmount');
            if (typeList) {
                var selected = typeList.querySelector('input[type="radio"]:checked');
                if (selected && selected.value === 'Partial' && partialInput) {
                    var partialValue = parseDecimal(partialInput.value);
                    if (partialValue > 0) {
                        amount = partialValue;
                    }
                }
            }
        }

        setHiddenDecimal('hfAmountDue', amount);
        refreshAmountDisplays(amount);
        updateCashChange();
    }

    function setupCorporateOptions() {
        var typeList = document.getElementById('rblCorporatePaymentType');
        var isCorporateField = document.getElementById('hfIsCorporateCustomer');
        if (!typeList || !isCorporateField || isCorporateField.value !== 'true') {
            return;
        }

        if (typeList.dataset.wired !== 'true') {
            typeList.addEventListener('change', function (event) {
                if (event.target && event.target.type === 'radio') {
                    toggleCorporatePartialInput();
                    updateAmountDueFromInputs();
                }
            });
            typeList.dataset.wired = 'true';
        }

        var partialInput = document.getElementById('txtCorporatePartialAmount');
        if (partialInput && partialInput.dataset.wired !== 'true') {
            partialInput.addEventListener('input', updateAmountDueFromInputs);
            partialInput.dataset.wired = 'true';
        }

        toggleCorporatePartialInput();
    }

    function updateCashChange() {
        var cashInput = document.getElementById('txtCashReceived');
        var changeDisplay = document.getElementById('cashChangeDisplay');
        if (!cashInput || !changeDisplay) {
            return;
        }
        var amountDue = getHiddenDecimal('hfAmountDue');
        if (amountDue <= 0) {
            amountDue = getHiddenDecimal('hfBaseAmountDue');
        }
        var received = parseDecimal(cashInput.value);
        var change = received - amountDue;
        if (!isFinite(change) || change < 0) {
            change = 0;
        }
        changeDisplay.textContent = formatCurrency(change);
    }

    function setupCashQuickButtons(cashInput) {
        var buttons = document.querySelectorAll('.cash-quick-btn');
        if (!buttons.length || !cashInput) {
            return;
        }

        buttons.forEach(function (button) {
            if (button.dataset.wired === 'true') {
                return;
            }
            button.addEventListener('click', function () {
                var action = button.getAttribute('data-cash-action');
                var amountDue = getHiddenDecimal('hfAmountDue') || getHiddenDecimal('hfBaseAmountDue');
                var delta = parseDecimal(button.getAttribute('data-cash-value'));
                var current = parseDecimal(cashInput.value);

                if (action === 'add' && delta > 0) {
                    cashInput.value = (current + delta).toFixed(2);
                } else if (action === 'exact') {
                    cashInput.value = amountDue.toFixed(2);
                }
                cashInput.dispatchEvent(new Event('input'));
            });
            button.dataset.wired = 'true';
        });
    }

    function setupNumericKeypad() {
        var keypad = document.querySelector('.numeric-keypad');
        var cashInput = document.getElementById('txtCashReceived');
        if (!keypad || !cashInput) {
            return;
        }
        if (keypad.dataset.wired === 'true') {
            return;
        }
        keypad.addEventListener('click', function (event) {
            var button = event.target;
            if (!button || button.tagName !== 'BUTTON') {
                return;
            }
            var key = button.getAttribute('data-key');
            if (!key) {
                return;
            }
            var current = cashInput.value || '';
            switch (key) {
                case 'clear':
                    current = '';
                    break;
                case 'back':
                    current = current.slice(0, -1);
                    break;
                case '.':
                    if (current.indexOf('.') === -1) {
                        current = current ? current + '.' : '0.';
                    }
                    break;
                default:
                    current += key;
                    break;
            }
            cashInput.value = current;
            cashInput.dispatchEvent(new Event('input'));
        });
        keypad.dataset.wired = 'true';
    }

    function setupCashInput() {
        var cashInput = document.getElementById('txtCashReceived');
        if (!cashInput) {
            return;
        }
        if (cashInput.dataset.wired !== 'true') {
            cashInput.addEventListener('input', updateCashChange);
            cashInput.dataset.wired = 'true';
        }
        setupCashQuickButtons(cashInput);
    }

    function wirePaymentOptions() {
        var paymentList = document.getElementById('rblPaymentMethod');
        if (paymentList) {
            if (paymentList.dataset.posPaymentWired !== 'true') {
                paymentList.addEventListener('change', function (event) {
                    if (event.target && event.target.type === 'radio') {
                        showPaymentPanel(event.target.value);
                    }
                });
                paymentList.dataset.posPaymentWired = 'true';
            }
            showPaymentPanel(getSelectedPaymentMethod());
        }

        setupCorporateOptions();
        updateAmountDueFromInputs();
        setupCashInput();
        setupNumericKeypad();
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

    function synchronizePaymentUi() {
        updateAmountDueFromInputs();
        showPaymentPanel(getSelectedPaymentMethod());
        updateCashChange();
    }

    window.PosUI = {
        showPaymentModal: function () {
            var modalEl = document.getElementById('paymentModal');
            if (!modalEl || typeof bootstrap === 'undefined') {
                return;
            }
            synchronizePaymentUi();
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
