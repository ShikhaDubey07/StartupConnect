// ---------- Security helpers ----------
// Antiforgery: every same-origin unsafe request (jQuery AJAX or fetch) automatically carries the
// token from <meta name="csrf-token">, matching the server's AutoValidateAntiforgeryToken filter.
function scCsrfToken() {
    var meta = document.querySelector('meta[name="csrf-token"]');
    if (meta && meta.content) return meta.content;
    var input = document.querySelector('input[name="__RequestVerificationToken"]');
    return input ? input.value : '';
}

function scIsSameOrigin(url) {
    try { return new URL(url, window.location.href).origin === window.location.origin; }
    catch (e) { return false; }
}

// Escape text before inserting it into HTML strings.
function scEscapeHtml(value) {
    return String(value === null || value === undefined ? '' : value)
        .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
}

// Only allow app-relative links (e.g. "/Ideas/Detail/5") from server data in hrefs.
function scSafeLocalUrl(url) {
    return (typeof url === 'string' && /^\/(?![\/\\])/.test(url)) ? url : null;
}

(function () {
    if (window.fetch && !window.fetch.__scCsrf) {
        var originalFetch = window.fetch;
        var wrapped = function (input, init) {
            init = init || {};
            var method = (init.method || (input && input.method) || 'GET').toUpperCase();
            var url = typeof input === 'string' ? input : (input && input.url) || '';
            if (!/^(GET|HEAD|OPTIONS|TRACE)$/.test(method) && scIsSameOrigin(url)) {
                var headers = new Headers(init.headers || (typeof input !== 'string' && input.headers) || {});
                if (!headers.has('RequestVerificationToken')) headers.set('RequestVerificationToken', scCsrfToken());
                init.headers = headers;
            }
            return originalFetch.call(this, input, init);
        };
        wrapped.__scCsrf = true;
        window.fetch = wrapped;
    }
    if (window.jQuery) {
        jQuery.ajaxPrefilter(function (options, originalOptions, jqXHR) {
            var method = (options.type || options.method || 'GET').toUpperCase();
            if (!/^(GET|HEAD|OPTIONS|TRACE)$/.test(method) && scIsSameOrigin(options.url)) {
                jqXHR.setRequestHeader('RequestVerificationToken', scCsrfToken());
            }
        });
    }
})();

$(function () {
    // Auto-hide toasts
    setTimeout(function () {
        $('.toast').fadeOut(400);
    }, 4000);

    // Interest type toggle
    $('input[name="InterestType"]').on('change', function () {
        var val = $(this).val();
        var $invest = $('#investSection');
        var $work = $('#workSection');
        if (val === 'Work' || val === '0') {
            $invest.slideUp(200);
            $work.slideDown(200);
        } else if (val === 'Invest' || val === '1') {
            $work.slideUp(200);
            $invest.slideDown(200);
        } else {
            $invest.slideDown(200);
            $work.slideDown(200);
        }
    });

    // Toggle Like
    $('.toggle-like-btn').on('click', function (e) {
        e.preventDefault();
        var btn = $(this);
        var id = btn.data('id');
        
        $.post('/Ideas/ToggleLike', { id: id }, function (res) {
            btn.find('.likes-count').text(res.count);
            var icon = btn.find('i.bi');
            var text = btn.find('.like-text');
            
            if (res.liked) {
                btn.removeClass('btn-outline-danger').addClass('btn-danger');
                icon.removeClass('bi-heart').addClass('bi-heart-fill');
                text.text('Liked');
                $('#likesCountHero').text(res.count);
            } else {
                btn.removeClass('btn-danger').addClass('btn-outline-danger');
                icon.removeClass('bi-heart-fill').addClass('bi-heart');
                text.text('Like');
                $('#likesCountHero').text(res.count);
            }
        });
    });

    // Toggle Save Detail
    $('.toggle-save-detail-btn').on('click', function (e) {
        e.preventDefault();
        var btn = $(this);
        var id = btn.data('id');
        
        $.post('/Ideas/ToggleSave', { id: id }, function (res) {
            var icon = btn.find('.save-icon');
            var text = btn.find('.save-text');
            
            if (res.saved) {
                btn.removeClass('btn-outline-primary').addClass('btn-primary');
                icon.removeClass('bi-bookmark').addClass('bi-bookmark-fill');
                text.text('Saved');
            } else {
                btn.removeClass('btn-primary').addClass('btn-outline-primary');
                icon.removeClass('bi-bookmark-fill').addClass('bi-bookmark');
                text.text('Save');
            }
        });
    });

    // Toggle Save Form (Card UI)
    $(document).on('submit', '.toggle-save-form', function (e) {
        e.preventDefault();
        var form = $(this);
        var btn = form.find('.save-btn');
        var icon = btn.find('.save-icon');
        var id = btn.data('id');
        var url = form.attr('action');

        $.ajax({
            url: url,
            type: 'POST',
            data: form.serialize(),
            success: function (res) {
                if (res.saved) {
                    icon.removeClass('bi-bookmark text-muted').addClass('bi-bookmark-fill text-primary');
                } else {
                    icon.removeClass('bi-bookmark-fill text-primary').addClass('bi-bookmark text-muted');
                    // If we are on the Saved page, fade out the card
                    var $card = $('#idea-card-' + id);
                    if ($card.length) {
                        $card.fadeOut(300, function () {
                            $(this).remove();
                            if ($('.row.g-4').children().length === 0) {
                                location.reload(); // Reload to show empty state
                            }
                        });
                    }
                }
            }
        });
    });

    // Show Interest AJAX
    $('#interestForm').on('submit', function (e) {
        e.preventDefault();
        var $btn = $('#submitInterestBtn');
        var $spinner = $btn.find('.spinner-border');
        $btn.prop('disabled', true);
        $spinner.removeClass('d-none');

        $.ajax({
            url: $(this).attr('action'),
            type: 'POST',
            data: $(this).serialize(),
            success: function (res) {
                if (res.success) {
                    $('#interestModal').modal('hide');
                    showToast(res.message, 'success');
                    setTimeout(function () { location.reload(); }, 1500);
                } else {
                    showToast(res.message, 'error');
                }
            },
            error: function () {
                showToast('Something went wrong. Please try again.', 'error');
            },
            complete: function () {
                $btn.prop('disabled', false);
                $spinner.addClass('d-none');
            }
        });
    });

    // Admin reject confirm
    $('.reject-form').on('submit', function (e) {
        var reason = $(this).find('[name="reason"]').val();
        if (!reason || !reason.trim()) {
            e.preventDefault();
            showToast('Please provide a rejection reason.', 'error');
        }
    });

    // Load notifications
    if ($('#notifList').length) {
        loadNotifications();
        // Remove polling: setInterval(loadNotifications, 60000);
        
        // Setup SignalR connection
        if (typeof signalR !== 'undefined') {
            const connection = new signalR.HubConnectionBuilder()
                .withUrl("/hubs/notifications")
                .withAutomaticReconnect()
                .build();

            connection.on("ReceiveNotification", function (n) {
                var $list = $('#notifList');
                var $badge = $('#notifCount');
                
                // Remove 'No new notifications' if present
                $list.find('li:contains("No new notifications")').remove();
                
                $list.prepend(renderNotificationItem(n));
                
                var currentCount = parseInt($badge.text()) || 0;
                $badge.text(currentCount + 1).removeClass('d-none');
                
                // Keep only top 10 in dropdown
                if ($list.children().length > 10) {
                    $list.children().last().remove();
                }
                
                showToast(n.title, 'success');
            });

            connection.start().catch(function (err) {
                return console.error(err.toString());
            });
        }
    }
    
    // Mark as read when clicking dropdown notification
    $(document).on('click', '.notif-item', function(e) {
        var link = $(this).attr('href');
        if (link) {
            e.preventDefault(); // Wait to navigate after ajax
        }
        var id = $(this).data('id');
        var item = $(this);
        
        $.ajax({
            url: '/Notifications/MarkRead',
            type: 'POST',
            data: { id: id },
            success: function() {
                var $badge = $('#notifCount');
                var count = parseInt($badge.text()) || 0;
                if (count > 1) {
                    $badge.text(count - 1);
                } else {
                    $badge.addClass('d-none').text('0');
                }
                item.parent().remove();
                if ($('#notifList').children().length === 0) {
                    $('#notifList').append('<li><span class="dropdown-item-text text-muted small px-3">No new notifications</span></li>');
                }
                if (link) {
                    window.location.href = link;
                }
            },
            error: function () {
                if (link) window.location.href = link;
            }
        });
    });

    // Skill/role checkbox styling
    $('.sc-checkbox-card input').on('change', function () {
        $(this).closest('.sc-checkbox-card').toggleClass('selected', this.checked);
    });

    // Navbar scroll effect
    $(window).on('scroll', function () {
        var $nav = $('.sc-navbar');
        if ($(window).scrollTop() > 50) {
            $nav.addClass('scrolled');
        } else {
            $nav.removeClass('scrolled');
        }
    });

    // Scroll-triggered card reveal animation
    if ('IntersectionObserver' in window) {
        var observer = new IntersectionObserver(function(entries) {
            entries.forEach(function(entry) {
                if (entry.isIntersecting) {
                    entry.target.style.opacity = '1';
                    entry.target.style.transform = 'translateY(0)';
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.08, rootMargin: '0px 0px -40px 0px' });

        document.querySelectorAll('.sc-idea-card, .sc-problem-card, .sc-hover-card, .sc-dash-stat, .sc-admin-stat-card, .sc-admin-idea-card, .sc-form-card').forEach(function(el) {
            el.style.opacity = '0';
            el.style.transform = 'translateY(24px)';
            el.style.transition = 'opacity 0.5s cubic-bezier(0.16,1,0.3,1), transform 0.5s cubic-bezier(0.16,1,0.3,1)';
            observer.observe(el);
        });
    }
});

function renderNotificationItem(n) {
    var safeLink = scSafeLocalUrl(n.linkUrl);
    var link = safeLink ? ' href="' + scEscapeHtml(safeLink) + '"' : '';
    return '<li><a class="dropdown-item py-2 notif-item" data-id="' + scEscapeHtml(n.id) + '"' + link + '><strong class="d-block small">' +
        scEscapeHtml(n.title) + '</strong><span class="text-muted small">' + scEscapeHtml(n.message) + '</span></a></li>';
}

function showToast(message, type) {
    var bg = type === 'success' ? 'sc-toast-success' : 'sc-toast-error';
    var icon = type === 'success' ? 'check-circle-fill' : 'exclamation-circle-fill';
    var iconColor = type === 'success' ? 'color:#10b981' : 'color:#ef4444';
    var html = '<div class="toast-container position-fixed top-0 end-0 p-3" style="z-index:9999">' +
        '<div class="toast show ' + bg + '" style="min-width:280px"><div class="toast-body d-flex align-items-center gap-2 py-3 px-3">' +
        '<i class="bi bi-' + icon + ' fs-5" style="' + iconColor + '"></i><span class="fw-medium">' + scEscapeHtml(message) + '</span>' +
        '<button type="button" class="btn-close ms-auto" style="font-size:0.65rem" onclick="$(this).closest(\'.toast-container\').remove()"></button>' +
        '</div></div></div>';
    $('body').append(html);
    setTimeout(function () { $('.toast-container').last().fadeOut(400, function () { $(this).remove(); }); }, 4000);
}

function loadNotifications() {
    $.get('/Notifications/Unread', function (data) {
        var $list = $('#notifList');
        var $badge = $('#notifCount');
        $list.empty();
        if (data.length === 0) {
            $list.append('<li><span class="dropdown-item-text text-muted small px-3">No new notifications</span></li>');
            $badge.addClass('d-none');
        } else {
            $badge.text(data.length).removeClass('d-none');
            data.forEach(function (n) {
                $list.append(renderNotificationItem(n));
            });
        }
    });
}

// Live deadline countdowns: <el data-countdown="ISO-UTC" [data-countdown-style="long"]><span class="sc-countdown-text">…</span></el>
function scFormatCountdown(ms, long) {
    if (ms <= 0) return 'Closed';
    var mins = Math.floor(ms / 60000), days = Math.floor(mins / 1440), hours = Math.floor((mins % 1440) / 60), m = mins % 60;
    if (long) {
        if (days > 0) return days + 'd ' + hours + 'h ' + m + 'm left';
        if (hours > 0) return hours + 'h ' + m + 'm left';
        return Math.max(1, m) + ' min left';
    }
    if (days >= 2) return days + ' days left';
    if (hours + days * 24 >= 2) return (hours + days * 24) + ' hours left';
    return Math.max(1, mins) + ' minute' + (mins === 1 ? '' : 's') + ' left';
}

function scTickCountdowns() {
    document.querySelectorAll('[data-countdown]').forEach(function (el) {
        var deadline = Date.parse(el.getAttribute('data-countdown'));
        if (isNaN(deadline)) return;
        var target = el.querySelector('.sc-countdown-text') || el;
        target.textContent = scFormatCountdown(deadline - Date.now(), el.getAttribute('data-countdown-style') === 'long');
    });
}

document.addEventListener('DOMContentLoaded', function () {
    if (document.querySelector('[data-countdown]')) {
        scTickCountdowns();
        setInterval(scTickCountdowns, 30000);
    }
});

function formatIndianCurrency(amount) {
    return '₹' + amount.toLocaleString('en-IN');
}
