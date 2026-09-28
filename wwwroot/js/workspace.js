// Team workspace: real-time chat (SignalR /hubs/team), older-message paging, milestone modal and live board refresh.
// All user content is inserted with textContent — never as HTML.
(function () {
    'use strict';

    var dataEl = document.getElementById('workspaceData');
    if (!dataEl) return;
    var cfg = JSON.parse(dataEl.textContent);
    var teamId = cfg.teamId;
    var me = cfg.currentUserId;

    var body = document.getElementById('chatBody');
    var list = document.getElementById('chatMessages');
    var empty = document.getElementById('chatEmpty');
    var loadOlderWrap = document.getElementById('loadOlderWrap');
    var loadOlderBtn = document.getElementById('loadOlderBtn');
    var newPill = document.getElementById('newMsgPill');
    var form = document.getElementById('chatForm');
    var input = document.getElementById('chatInput');
    var statusEl = document.getElementById('chatStatus');

    var all = [];           // loaded messages, sorted by id (oldest first)
    var seen = {};          // message id -> true (dedupe hub push vs. POST response)
    function dayKey(d) { return d.getFullYear() + '-' + d.getMonth() + '-' + d.getDate(); }

    function dayLabel(d) {
        var today = new Date();
        var yesterday = new Date(); yesterday.setDate(today.getDate() - 1);
        if (dayKey(d) === dayKey(today)) return 'Today';
        if (dayKey(d) === dayKey(yesterday)) return 'Yesterday';
        return d.toLocaleDateString(undefined, { weekday: 'short', day: 'numeric', month: 'short', year: d.getFullYear() === today.getFullYear() ? undefined : 'numeric' });
    }

    function separator(d) {
        var el = document.createElement('div');
        el.className = 'sc-chat-day';
        var span = document.createElement('span');
        span.textContent = dayLabel(d);
        el.appendChild(span);
        return el;
    }

    function initials(name) {
        var parts = String(name || '?').trim().split(/\s+/);
        var s = parts[0] ? parts[0][0] : '?';
        if (parts.length > 1) s += parts[parts.length - 1][0];
        return s.toUpperCase();
    }

    function bubble(m) {
        var mine = m.senderId === me;
        var row = document.createElement('div');
        row.className = 'sc-chat-row' + (mine ? ' mine' : '');
        row.dataset.id = m.id;

        if (!mine) {
            var av = document.createElement('span');
            av.className = 'sc-chat-avatar';
            av.textContent = initials(m.senderName);
            av.title = m.senderName;
            row.appendChild(av);
        }

        var b = document.createElement('div');
        b.className = 'sc-chat-bubble';
        if (!mine) {
            var who = document.createElement('div');
            who.className = 'sc-chat-sender';
            who.textContent = m.senderName;
            b.appendChild(who);
        }
        var text = document.createElement('div');
        text.className = 'sc-chat-text';
        text.textContent = m.content;
        b.appendChild(text);
        var time = document.createElement('div');
        time.className = 'sc-chat-time';
        var d = new Date(m.sentAt);
        time.textContent = d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
        time.title = d.toLocaleString();
        b.appendChild(time);
        row.appendChild(b);
        return row;
    }

    function nearBottom() { return body.scrollHeight - body.scrollTop - body.clientHeight < 80; }
    function scrollToBottom() { body.scrollTop = body.scrollHeight; newPill.classList.add('d-none'); }
    function firstId() { return all.length ? all[0].id : null; }
    function lastId() { return all.length ? all[all.length - 1].id : null; }

    function render() {
        var frag = document.createDocumentFragment();
        var key = null;
        all.forEach(function (m) {
            var d = new Date(m.sentAt);
            var k = dayKey(d);
            if (k !== key) { frag.appendChild(separator(d)); key = k; }
            frag.appendChild(bubble(m));
        });
        list.replaceChildren(frag);
        empty.classList.toggle('d-none', all.length > 0);
    }

    function add(messages) {
        var added = false;
        (messages || []).forEach(function (m) {
            if (!m || m.teamId !== teamId || seen[m.id]) return;
            seen[m.id] = true;
            all.push(m);
            added = true;
        });
        if (added) all.sort(function (x, y) { return x.id - y.id; });
        return added;
    }

    function append(m, opts) {
        var stick = (opts && opts.forceScroll) || nearBottom();
        if (!add([m])) return;
        render();
        if (stick) scrollToBottom(); else newPill.classList.remove('d-none');
    }

    function prepend(messages) {
        var prevHeight = body.scrollHeight, prevTop = body.scrollTop;
        if (!add(messages)) return;
        render();
        body.scrollTop = prevTop + (body.scrollHeight - prevHeight);
    }

    add(cfg.messages);
    render();
    scrollToBottom();

    newPill.addEventListener('click', scrollToBottom);
    body.addEventListener('scroll', function () { if (nearBottom()) newPill.classList.add('d-none'); });

    if (loadOlderBtn) {
        loadOlderBtn.addEventListener('click', function () {
            if (firstId() === null) return;
            loadOlderBtn.disabled = true;
            fetch('/Workspace/Messages?id=' + encodeURIComponent(teamId) + '&beforeId=' + encodeURIComponent(firstId()), { headers: { 'Accept': 'application/json' } })
                .then(function (r) { if (!r.ok) throw new Error(r.status); return r.json(); })
                .then(function (res) {
                    prepend(res.messages || []);
                    loadOlderWrap.classList.toggle('d-none', !res.hasMore);
                })
                .catch(function () { showToast('Could not load earlier messages.', 'error'); })
                .finally(function () { loadOlderBtn.disabled = false; });
        });
    }

    // ---- Sending ----
    if (form && input) {
        var autosize = function () { input.style.height = 'auto'; input.style.height = Math.min(input.scrollHeight, 160) + 'px'; };
        input.addEventListener('input', autosize);
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Enter' && !e.shiftKey && !e.isComposing) { e.preventDefault(); form.requestSubmit ? form.requestSubmit() : form.dispatchEvent(new Event('submit', { cancelable: true })); }
        });
        form.addEventListener('submit', function (e) {
            e.preventDefault();
            var content = input.value.trim();
            if (!content) return;
            var btn = document.getElementById('chatSend');
            btn.disabled = true;
            var fd = new FormData(form);
            fetch(form.action, { method: 'POST', body: fd, headers: { 'X-Requested-With': 'XMLHttpRequest', 'Accept': 'application/json' } })
                .then(function (r) { return r.json().then(function (j) { return { ok: r.ok, body: j }; }); })
                .then(function (res) {
                    if (!res.ok || !res.body.success) { showToast((res.body && res.body.message) || 'Message not sent.', 'error'); return; }
                    input.value = '';
                    autosize();
                    append(res.body.message, { forceScroll: true });
                })
                .catch(function () { showToast('Message not sent — check your connection.', 'error'); })
                .finally(function () { btn.disabled = false; input.focus(); });
        });
    }

    // ---- Milestone modal (create / edit) ----
    var modal = document.getElementById('milestoneModal');
    if (modal) {
        var mForm = document.getElementById('milestoneForm');
        modal.addEventListener('show.bs.modal', function (e) {
            var t = e.relatedTarget;
            var edit = t && t.dataset.mode === 'edit';
            mForm.action = edit ? mForm.dataset.editAction : mForm.dataset.createAction;
            document.getElementById('milestoneModalLabel').textContent = edit ? 'Edit milestone' : 'Add milestone';
            document.getElementById('msSubmit').textContent = edit ? 'Save changes' : 'Add milestone';
            document.getElementById('msId').value = edit ? t.dataset.id : '';
            document.getElementById('msTitle').value = edit ? (t.dataset.title || '') : '';
            document.getElementById('msDescription').value = edit ? (t.dataset.description || '') : '';
            document.getElementById('msDue').value = edit ? (t.dataset.due || '') : '';
            document.getElementById('msAssignee').value = edit ? (t.dataset.assignee || '') : '';
        });
        modal.addEventListener('shown.bs.modal', function () { document.getElementById('msTitle').focus(); });
    }

    function refreshMilestones() {
        var board = document.getElementById('milestoneBoard');
        if (!board || (modal && modal.classList.contains('show'))) return;
        fetch('/Workspace/Milestones/' + encodeURIComponent(teamId), { headers: { 'Accept': 'text/html' } })
            .then(function (r) { if (!r.ok) throw new Error(r.status); return r.text(); })
            .then(function (html) { board.innerHTML = html; }) // server-rendered Razor partial (HTML-encoded)
            .catch(function () { /* keep the current board */ });
    }

    // ---- Real-time ----
    function setStatus(state, label) {
        if (!statusEl) return;
        statusEl.dataset.state = state;
        statusEl.querySelector('.label').textContent = label;
    }

    if (typeof signalR === 'undefined') { setStatus('offline', 'Offline'); return; }

    var connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/team')
        .withAutomaticReconnect()
        .build();

    connection.on('teamMessage', function (m) { append(m); });
    connection.on('teamChanged', function (e) {
        if (!e || e.teamId !== teamId) return;
        if (e.what === 'removed') {
            window.location.href = '/Workspace';
        } else if (e.what === 'milestones') {
            refreshMilestones();
        } else {
            showToast('The team was updated — refresh to see the latest.', 'success');
        }
    });

    function catchUp() {
        // After a reconnect, fetch the latest page and append anything we missed.
        fetch('/Workspace/Messages?id=' + encodeURIComponent(teamId), { headers: { 'Accept': 'application/json' } })
            .then(function (r) { return r.ok ? r.json() : { messages: [] }; })
            .then(function (res) { var before = lastId(); (res.messages || []).forEach(function (m) { if (before === null || m.id > before) append(m); }); })
            .catch(function () { });
    }

    connection.onreconnecting(function () { setStatus('connecting', 'Reconnecting…'); });
    connection.onreconnected(function () { setStatus('live', 'Live'); catchUp(); });
    connection.onclose(function () { setStatus('offline', 'Offline — refresh to reconnect'); });

    connection.start()
        .then(function () { setStatus('live', 'Live'); })
        .catch(function () { setStatus('offline', 'Offline — messages still send'); });
})();
