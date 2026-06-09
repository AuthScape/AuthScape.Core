(function () {
    document.addEventListener('change', function (event) {
        var checkbox = event.target.closest('.sso-toggle-input');
        if (!checkbox) return;
        var form = checkbox.closest('form');
        if (!form) return;
        var hiddenInput = form.querySelector('.enabled-input');
        if (hiddenInput) {
            hiddenInput.value = checkbox.checked ? 'true' : 'false';
        }
        form.submit();
    });

    document.addEventListener('click', function (event) {
        var button = event.target.closest('.sso-delete-btn');
        if (!button) return;
        var name = button.dataset.ssoName || '';
        var type = button.dataset.ssoType || '';
        var nameEl = document.getElementById('providerNameToDelete');
        var typeEl = document.getElementById('providerTypeToDelete');
        if (nameEl) nameEl.textContent = name;
        if (typeEl) typeEl.value = type;
        var modalEl = document.getElementById('deleteModal');
        if (modalEl && window.bootstrap && window.bootstrap.Modal) {
            new window.bootstrap.Modal(modalEl).show();
        }
    });

    window.setTimeout(function () {
        var alerts = document.querySelectorAll('.alert');
        alerts.forEach(function (alert) {
            if (window.bootstrap && window.bootstrap.Alert) {
                new window.bootstrap.Alert(alert).close();
            }
        });
    }, 5000);
})();
