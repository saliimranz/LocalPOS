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

        setupTaxInput();
        setupCorporateOptions();
        setupCashInput();
        setupNumericKeypad();
        updateAmountDueFromInputs();
        flushPendingReceiptDownload();
    }

    function getCatalogSearchInput() {
        return document.querySelector('input[type="search"][name$="txtSearch"], input[type="search"][id$="txtSearch"], input[type="search"][aria-label="Search products"]');
    }

    function getCatalogSearchButton() {
        // asp:Button renders as <input type="submit" ...>, sometimes <button>.
        return document.querySelector('input[name$="btnSearch"], input[id$="btnSearch"], button[name$="btnSearch"], button[id$="btnSearch"]');
    }

    function isCatalogSearchInput(node) {
        if (!node || node.tagName !== 'INPUT') {
            return false;
        }
        var type = (node.getAttribute('type') || '').toLowerCase();
        // Some WebForms builds may not render type="search" reliably.
        // Prefer matching by name/id suffix.
        if (type && type !== 'search' && type !== 'text') {
            return false;
        }

        var name = node.name || '';
        var id = node.id || '';
        if (/(^|\$)txtSearch$/i.test(name)) {
            return true;
        }
        if (/txtSearch$/i.test(id)) {
            return true;
        }
        if ((node.getAttribute('aria-label') || '') === 'Search products') {
            return true;
        }
        if ((node.getAttribute('aria-label') || '') === 'Search products or scan barcode...') {
            return true;
        }
        return false;
    }

    function postBackCatalogSearch(searchInput) {
        var targetControl = getCatalogSearchButton() || searchInput;
        if (!targetControl || !targetControl.name) {
            return;
        }
        if (typeof window.__doPostBack !== 'function') {
            return;
        }
        window.__doPostBack(targetControl.name, '');
    }

    var lastCatalogSearchValueByTarget = Object.create(null);

    function initCatalogSearchState() {
        var input = getCatalogSearchInput();
        if (!input) {
            return;
        }
        var key = input.name || input.id;
        if (!key) {
            return;
        }
        lastCatalogSearchValueByTarget[key] = (input.value || '').trim();
    }

    function attachCatalogSearchDelegates() {
        if (document.documentElement && document.documentElement.dataset.posCatalogSearchDelegatesWired === 'true') {
            return;
        }

        function evaluatePotentialReset(target) {
            if (!isCatalogSearchInput(target)) {
                return;
            }

            var key = target.name || target.id;
            if (!key) {
                return;
            }

            var current = (target.value || '').trim();
            var previous = (lastCatalogSearchValueByTarget[key] || '').trim();

            // Update before posting back so repeated events don't spam.
            lastCatalogSearchValueByTarget[key] = current;

            // Only trigger when transitioning non-empty -> empty.
            if (previous !== '' && current === '') {
                postBackCatalogSearch(target);
            }
        }

        function handleValueChangeEvent(e) {
            var target = e && e.target;
            // Some browsers update the value *after* the event fires (notably for the search clear "x").
            // Defer the check to the next tick to read the final value.
            if (target) {
                setTimeout(function () { evaluatePotentialReset(target); }, 0);
            }
        }

        // Capture phase so we still run even if the textbox is replaced / handlers stop propagation.
        document.addEventListener('input', handleValueChangeEvent, true);
        document.addEventListener('search', handleValueChangeEvent, true);
        document.addEventListener('change', handleValueChangeEvent, true);

        // Optional: Enter triggers a postback (useful on some mobile keyboards).
        document.addEventListener('keydown', function (e) {
            var target = e && e.target;
            if (!isCatalogSearchInput(target)) {
                return;
            }
            if (e.key === 'Enter') {
                postBackCatalogSearch(target);
            }
        }, true);

        if (document.documentElement) {
            document.documentElement.dataset.posCatalogSearchDelegatesWired = 'true';
        }
    }

    function startCatalogSearchEmptyMonitor(searchInput) {
        if (!searchInput) {
            return;
        }
        if (searchInput.dataset.posEmptyMonitorActive === 'true') {
            return;
        }

        var key = searchInput.name || searchInput.id;
        if (!key) {
            return;
        }

        var intervalId = window.setInterval(function () {
            // Only monitor while focused to avoid polling overhead.
            if (document.activeElement !== searchInput) {
                window.clearInterval(intervalId);
                searchInput.dataset.posEmptyMonitorActive = 'false';
                return;
            }

            var current = (searchInput.value || '').trim();
            var previous = (lastCatalogSearchValueByTarget[key] || '').trim();

            lastCatalogSearchValueByTarget[key] = current;
            if (previous !== '' && current === '') {
                window.clearInterval(intervalId);
                searchInput.dataset.posEmptyMonitorActive = 'false';
                postBackCatalogSearch(searchInput);
            }
        }, 150);

        searchInput.dataset.posEmptyMonitorActive = 'true';
    }

    var catalogSearchWatchdogId = null;
    var catalogSearchWatchdogLastKey = null;
    var catalogSearchWatchdogLastValue = null;

    function isAsyncPostBackInProgress() {
        if (!(window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager)) {
            return false;
        }
        var manager = Sys.WebForms.PageRequestManager.getInstance();
        return manager && typeof manager.get_isInAsyncPostBack === 'function' ? manager.get_isInAsyncPostBack() : false;
    }

    function ensureCatalogSearchWatchdogStarted() {
        if (catalogSearchWatchdogId) {
            return;
        }

        catalogSearchWatchdogId = window.setInterval(function () {
            if (document.hidden) {
                return;
            }

            var input = getCatalogSearchInput();
            if (!input) {
                catalogSearchWatchdogLastKey = null;
                catalogSearchWatchdogLastValue = null;
                return;
            }

            var key = input.name || input.id || null;
            var current = (input.value || '').trim();

            if (!key) {
                return;
            }

            // If the control was replaced, reset baseline.
            if (catalogSearchWatchdogLastKey !== key) {
                catalogSearchWatchdogLastKey = key;
                catalogSearchWatchdogLastValue = current;
                return;
            }

            var previous = (catalogSearchWatchdogLastValue || '').trim();
            catalogSearchWatchdogLastValue = current;

            // Only when transitioning non-empty -> empty.
            if (previous !== '' && current === '') {
                // Avoid spamming while an async postback is already running.
                if (!isAsyncPostBackInProgress()) {
                    postBackCatalogSearch(input);
                }
            }
        }, 200);
    }

    function wireCatalogSearchAutoReset() {
        var searchInput = getCatalogSearchInput();
        if (!searchInput || searchInput.dataset.posCatalogSearchWired === 'true') {
            return;
        }

        function getNormalizedValue() {
            return (searchInput.value || '').trim();
        }

        searchInput.dataset.posLastNormalizedValue = getNormalizedValue();

        function handlePotentialReset() {
            var previous = (searchInput.dataset.posLastNormalizedValue || '').trim();
            var current = getNormalizedValue();
            searchInput.dataset.posLastNormalizedValue = current;

            if (previous !== '' && current === '') {
                postBackCatalogSearch(searchInput);
            }
        }

        // Fires on typing/backspace and on the type="search" clear (x) in most browsers.
        searchInput.addEventListener('input', handlePotentialReset);
        searchInput.addEventListener('search', handlePotentialReset);

        // Optional: Enter triggers a postback even if TextChanged wouldn't fire.
        searchInput.addEventListener('keydown', function (e) {
            if (e && e.key === 'Enter') {
                postBackCatalogSearch(searchInput);
            }
        });

        // Fallback: monitor while focused so "clear (x)" that emits no events still works.
        searchInput.addEventListener('focus', function () {
            initCatalogSearchState();
            startCatalogSearchEmptyMonitor(searchInput);
        });

        searchInput.dataset.posCatalogSearchWired = 'true';
    }

    var ajaxHandlersAttached = false;
    var ajaxAttachAttempts = 0;

    function attachAjaxHandlers() {
        if (ajaxHandlersAttached) {
            return;
        }
        if (!(window.Sys && Sys.WebForms && Sys.WebForms.PageRequestManager)) {
            // MS AJAX might not be available yet depending on script load order.
            // Retry a few times so we can rewire handlers after UpdatePanel refreshes.
            if (ajaxAttachAttempts < 40) { // ~2s worst-case (40 * 50ms)
                ajaxAttachAttempts += 1;
                setTimeout(attachAjaxHandlers, 50);
            }
            return;
        }

        var manager = Sys.WebForms.PageRequestManager.getInstance();
        if (!manager) {
            return;
        }

        manager.add_endRequest(function () {
            wirePaymentOptions();
            // Re-init state after UpdatePanel replaces controls.
            initCatalogSearchState();
            wireCatalogSearchAutoReset();
            cleanupModalArtifacts();
            flushPendingReceiptDownload();
        });

        ajaxHandlersAttached = true;
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
        attachCatalogSearchDelegates();
        initCatalogSearchState();
        ensureCatalogSearchWatchdogStarted();
        wireCatalogSearchAutoReset();
        attachAjaxHandlers();
        flushPendingReceiptDownload();
    });
})();
