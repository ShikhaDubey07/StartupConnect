// Shared behaviour for Views/Shared/_ConnectButton.cshtml (loaded by _Layout for signed-in users).
// Buttons carry data-connect="send|accept|decline|withdraw|remove" inside a .sc-connect[data-user-id] wrapper.
(function () {
    'use strict';

    var MAX_NOTE = 300;
    var modal = null;
    var pendingSend = null;

    function wrapperFor(el) { return el.closest('.sc-connect'); }

    function refreshButtons(userId) {
        var wrappers = document.querySelectorAll('.sc-connect[data-user-id="' + CSS.escape(userId) + '"]');
        wrappers.forEach(function (w) {
            var url = '/Network/Button?userId=' + encodeURIComponent(userId) + '&variant=' + encodeURIComponent(w.dataset.variant || 'card');
            fetch(url, { headers: { 'X-Requested-With': 'XMLHttpRequest' }, credentials: 'same-origin' })
                .then(function (r) { return r.ok ? r.text() : null; })
                .then(function (html) {
                    if (!html) return;
                    var tmp = document.createElement('div');
                    tmp.innerHTML = html.trim();
                    var fresh = tmp.querySelector('.sc-connect');
                    if (fresh) {
                        if (w.dataset.name) fresh.dataset.name = w.dataset.name;
                        w.replaceWith(fresh);
                    }
                });
        });
    }

    function post(action, userId, extra, button) {
        var body = new URLSearchParams({ userId: userId });
        if (extra) Object.keys(extra).forEach(function (k) { body.append(k, extra[k]); });
        if (button) { button.disabled = true; }
        return fetch('/Network/' + action, {
            method: 'POST',
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' },
            body: body
        })
            .then(function (r) {
                return r.json().catch(function () { return { success: false, message: 'Something went wrong. Please try again.' }; });
            })
            .then(function (res) {
                if (typeof showToast === 'function') showToast(res.message || (res.success ? 'Done.' : 'Something went wrong.'), res.success ? 'success' : 'error');
                refreshButtons(userId);
                document.dispatchEvent(new CustomEvent('sc:connection-changed', { detail: { userId: userId, action: action, result: res } }));
                return res;
            })
            .catch(function () {
                if (typeof showToast === 'function') showToast('Network error — please try again.', 'error');
                if (button) button.disabled = false;
            });
    }

    function ensureModal() {
        if (modal) return modal;
        var el = document.createElement('div');
        el.className = 'modal fade';
        el.id = 'scConnectModal';
        el.tabIndex = -1;
        el.setAttribute('aria-labelledby', 'scConnectModalTitle');
        el.setAttribute('aria-hidden', 'true');
        el.innerHTML =
            '<div class="modal-dialog modal-dialog-centered"><div class="modal-content border-0 shadow rounded-4">' +
            '<div class="modal-header border-0 pb-0"><h5 class="modal-title fw-bold" id="scConnectModalTitle">Send a connection request</h5>' +
            '<button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div>' +
            '<div class="modal-body">' +
            '<p class="text-muted small mb-3" data-role="intro"></p>' +
            '<label for="scConnectNote" class="form-label fw-medium">Add a note <span class="text-muted fw-normal">(optional)</span></label>' +
            '<textarea id="scConnectNote" class="form-control" rows="3" maxlength="' + MAX_NOTE + '" placeholder="Hi! I saw your profile and would love to connect about…"></textarea>' +
            '<div class="form-text text-end"><span data-role="count">0</span>/' + MAX_NOTE + '</div>' +
            '</div>' +
            '<div class="modal-footer border-0 pt-0">' +
            '<button type="button" class="btn btn-outline-secondary rounded-pill px-4" data-bs-dismiss="modal">Cancel</button>' +
            '<button type="button" class="btn btn-primary-sc rounded-pill px-4" data-role="send"><i class="bi bi-send me-1"></i>Send request</button>' +
            '</div></div></div>';
        document.body.appendChild(el);
        var note = el.querySelector('#scConnectNote');
        var count = el.querySelector('[data-role="count"]');
        note.addEventListener('input', function () { count.textContent = note.value.length; });
        el.querySelector('[data-role="send"]').addEventListener('click', function () {
            if (!pendingSend) return;
            var sendBtn = this;
            sendBtn.disabled = true;
            post('Send', pendingSend.userId, { message: note.value }, pendingSend.button).then(function () {
                sendBtn.disabled = false;
                bootstrap.Modal.getOrCreateInstance(el).hide();
            });
        });
        el.addEventListener('hidden.bs.modal', function () { note.value = ''; count.textContent = '0'; pendingSend = null; });
        el.addEventListener('shown.bs.modal', function () { note.focus(); });
        modal = el;
        return el;
    }

    document.addEventListener('click', function (e) {
        var button = e.target.closest('.sc-connect-action');
        if (!button) return;
        var wrapper = wrapperFor(button);
        if (!wrapper) return;
        e.preventDefault();
        var userId = wrapper.dataset.userId;
        var name = wrapper.dataset.name || 'this member';

        switch (button.dataset.connect) {
            case 'send':
                var el = ensureModal();
                el.querySelector('[data-role="intro"]').textContent =
                    'Connections can see each other’s full profiles, even private ones. ' + name + ' will be notified and can accept or decline.';
                pendingSend = { userId: userId, button: button };
                bootstrap.Modal.getOrCreateInstance(el).show();
                break;
            case 'accept': post('Accept', userId, null, button); break;
            case 'decline': post('Decline', userId, null, button); break;
            case 'withdraw': post('Withdraw', userId, null, button); break;
            case 'remove':
                if (confirm('Remove ' + name + ' from your connections?')) post('Remove', userId, null, button);
                break;
        }
    });
})();
