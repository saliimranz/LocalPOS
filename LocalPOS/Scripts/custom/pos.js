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
        var symbol = (symbolField && symbolField.value) ? symbolField.value : 'AED';
        var numericValue = Number(value || 0);
        var formatted = numericValue.toLocaleString(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 });
        return symbol ? symbol + ' ' + formatted : formatted;
    }

    function clamp(value, min, max) {
        if (!isFinite(value)) {
            return min;
        }
        return Math.min(Math.max(value, min), max);
    }

    function refreshAmountDisplays(amount) {
        var formatted = formatCurrency(amount);
        document.querySelectorAll('[data-amount-due-display]').forEach(function (node) {
            node.textContent = formatted;
        });
    }

    function cleanupModalArtifacts() {
        var body = document.body;
        var html = document.documentElement;
        if (!body) {
            return;
        }

        var openModals = document.querySelectorAll('.modal.show');
        var hasOpenModal = openModals.length > 0;

        if (hasOpenModal) {
            body.classList.add('modal-open');
        } else {
            body.classList.remove('modal-open');
            ['padding-right', 'overflow', '--bs-body-padding-right'].forEach(function (prop) {
                body.style.removeProperty(prop);
            });
            if (html) {
                ['padding-right', '--bs-body-padding-right'].forEach(function (prop) {
                    html.style.removeProperty(prop);
                });
            }
            body.removeAttribute('data-bs-padding-right');
            body.removeAttribute('data-bs-overflow');
        }

        var backdrops = document.querySelectorAll('.modal-backdrop');
        if (hasOpenModal) {
            if (!backdrops.length) {
                var backdrop = document.createElement('div');
                backdrop.className = 'modal-backdrop fade show';
                document.body.appendChild(backdrop);
            } else {
                backdrops.forEach(function (backdrop) {
                    backdrop.classList.add('show');
                });
            }
        } else {
            backdrops.forEach(function (backdrop) {
                if (backdrop.parentNode) {
                    backdrop.parentNode.removeChild(backdrop);
                }
            });
        }
    }

    function toggleModalById(id, action) {
        if (typeof bootstrap === 'undefined') {
            return null;
        }
        var modalEl = document.getElementById(id);
        if (!modalEl) {
            return null;
        }
        var modal = bootstrap.Modal.getOrCreateInstance(modalEl);
        if (action === 'show') {
            modal.show();
        } else {
            modal.hide();
        }
        cleanupModalArtifacts();
        return modal;
    }

    function getTaxInput() {
        return document.getElementById('txtModalTaxPercent');
    }

    function getDefaultTaxPercent() {
        var field = document.getElementById('hfDefaultTaxPercent');
        return field ? parseDecimal(field.value) : 0;
    }

    function getTaxableAmount() {
        return Math.max(0, getHiddenDecimal('hfTaxableAmount'));
    }

    function updateTaxSummaryDisplays(taxAmount, totalAmount) {
        var taxEl = document.getElementById('modalVatDisplay');
        if (taxEl) {
            taxEl.textContent = formatCurrency(taxAmount);
        }
        var totalEl = document.getElementById('modalTotalDisplay');
        if (totalEl) {
            totalEl.textContent = formatCurrency(totalAmount);
        }
        refreshAmountDisplays(totalAmount);
    }

    function applyTaxPercent(percent) {
        var taxable = getTaxableAmount();
        var normalized = clamp(percent, 0, 100);

        var input = getTaxInput();
        if (input) {
            input.value = normalized % 1 === 0 ? normalized.toString() : normalized.toFixed(2);
        }

        var taxAmount = taxable * (normalized / 100);
        var total = taxable + taxAmount;
        setHiddenDecimal('hfBaseAmountDue', total);
        updateTaxSummaryDisplays(taxAmount, total);
        updateAmountDueFromInputs();
        return normalized;
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
                        amount = Math.min(partialValue, baseAmount);
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

    function setupTaxInput() {
        var input = getTaxInput();
        if (!input) {
            return;
        }
        if (input.dataset.wired === 'true') {
            return;
        }
        input.addEventListener('input', function () {
            var raw = input.value;
            var percent = raw === '' ? getDefaultTaxPercent() : parseDecimal(raw);
            applyTaxPercent(percent);
        });
        input.dataset.wired = 'true';
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

    function setUserMenuState(wrapper, isOpen) {
        if (!wrapper) {
            return;
        }
        wrapper.classList.toggle('is-open', Boolean(isOpen));
        var toggle = wrapper.querySelector('[data-user-menu-toggle]');
        var panel = wrapper.querySelector('[data-user-menu-panel]');
        if (toggle) {
            toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        }
        if (panel) {
            panel.hidden = !isOpen;
        }
    }

    function closeUserMenus(except) {
        document.querySelectorAll('[data-user-menu-wrapper].is-open').forEach(function (wrapper) {
            if (wrapper === except) {
                return;
            }
            setUserMenuState(wrapper, false);
        });
    }

    function setupUserMenus() {
        var wrappers = document.querySelectorAll('[data-user-menu-wrapper]');
        if (!wrappers.length) {
            return;
        }
        wrappers.forEach(function (wrapper) {
            if (wrapper.dataset.userMenuWired === 'true') {
                return;
            }
            var toggle = wrapper.querySelector('[data-user-menu-toggle]');
            var panel = wrapper.querySelector('[data-user-menu-panel]');
            if (!toggle || !panel) {
                return;
            }
            panel.hidden = !wrapper.classList.contains('is-open');
            toggle.addEventListener('click', function (event) {
                event.preventDefault();
                event.stopPropagation();
                var willOpen = !wrapper.classList.contains('is-open');
                if (willOpen) {
                    closeUserMenus(wrapper);
                }
                setUserMenuState(wrapper, willOpen);
            });
            wrapper.dataset.userMenuWired = 'true';
        });
    }

    function wirePaymentOptions() {
        setupUserMenus();
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

        setupTaxInput();
        setupCorporateOptions();
        setupCashInput();
        setupNumericKeypad();
        updateAmountDueFromInputs();
        flushPendingReceiptDownload();
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
                cleanupModalArtifacts();
                flushPendingReceiptDownload();
            });
            ajaxHandlersAttached = true;
        }
    }

    function synchronizePaymentUi() {
        var currentPercent = 0;
        var input = getTaxInput();
        if (input && input.value.trim() !== '') {
            currentPercent = parseDecimal(input.value);
        } else {
            currentPercent = getDefaultTaxPercent();
        }
        applyTaxPercent(currentPercent);
        showPaymentPanel(getSelectedPaymentMethod());
        updateCashChange();
    }

    document.addEventListener('shown.bs.modal', cleanupModalArtifacts);
    document.addEventListener('hidden.bs.modal', cleanupModalArtifacts);

    document.addEventListener('click', function (event) {
        var target = event.target;
        document.querySelectorAll('[data-user-menu-wrapper].is-open').forEach(function (wrapper) {
            if (!wrapper.contains(target)) {
                setUserMenuState(wrapper, false);
            }
        });
    });

    document.addEventListener('keydown', function (event) {
        if (event.key === 'Escape') {
            closeUserMenus();
        }
    });

    function performReceiptDownload(url) {
        if (!url) {
            return;
        }
        setTimeout(function () {
            var iframe = document.createElement('iframe');
            iframe.style.display = 'none';
            var separator = url.indexOf('?') === -1 ? '?' : '&';
            iframe.src = url + separator + '_ts=' + Date.now();
            document.body.appendChild(iframe);
            setTimeout(function () {
                if (iframe.parentNode) {
                    iframe.parentNode.removeChild(iframe);
                }
            }, 60000);
        }, 150);
    }

    function flushPendingReceiptDownload() {
        var field = document.getElementById('hfReceiptDownloadUrl');
        if (!field || !field.value) {
            return;
        }
        var pendingUrl = field.value;
        field.value = '';
        performReceiptDownload(pendingUrl);
    }

    window.PosUI = {
        showPaymentModal: function () {
            var modal = toggleModalById('paymentModal', 'show');
            if (modal) {
                synchronizePaymentUi();
            }
        },
        hidePaymentModal: function () {
            toggleModalById('paymentModal', 'hide');
        },
        showHoldConfirm: function () {
            toggleModalById('holdConfirmModal', 'show');
        },
        hideHoldConfirm: function () {
            toggleModalById('holdConfirmModal', 'hide');
        },
        showHeldBills: function () {
            toggleModalById('heldBillsModal', 'show');
        },
        hideHeldBills: function () {
            toggleModalById('heldBillsModal', 'hide');
        },
        downloadReceipt: function (url) {
            performReceiptDownload(url);
        }
    };

    document.addEventListener('DOMContentLoaded', function () {
        keepClockUpdated();
        wirePaymentOptions();
        attachAjaxHandlers();
        flushPendingReceiptDownload();
    });
})();
