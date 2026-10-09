// ==========================================================================================
// WebDesk ERP - Real-time SignalR & Web Push Notifications Client
// ==========================================================================================

(function () {
    'use strict';

    // --------------------------------------------------------------------------------------
    // 1. WEB AUDIO API CHIME (Zero external audio file dependency)
    // --------------------------------------------------------------------------------------
    function playNotificationChime() {
        try {
            const AudioContext = window.AudioContext || window.webkitAudioContext;
            if (!AudioContext) return;

            const ctx = new AudioContext();
            const osc = ctx.createOscillator();
            const gain = ctx.createGain();

            osc.type = 'sine';
            osc.frequency.setValueAtTime(587.33, ctx.currentTime); // D5
            osc.frequency.setValueAtTime(880.00, ctx.currentTime + 0.12); // A5

            gain.gain.setValueAtTime(0.15, ctx.currentTime);
            gain.gain.exponentialRampToValueAtTime(0.001, ctx.currentTime + 0.4);

            osc.connect(gain);
            gain.connect(ctx.destination);

            osc.start();
            osc.stop(ctx.currentTime + 0.4);
        } catch (e) {
            // Audio context may be restricted before user gesture; silently ignore
        }
    }

    // --------------------------------------------------------------------------------------
    // 2. NATIVE DESKTOP NOTIFICATION (Windows / macOS / Android OS Toast)
    // --------------------------------------------------------------------------------------
    function showNativeDesktopNotification(data) {
        if (!('Notification' in window)) return;

        if (Notification.permission === 'granted') {
            const title = data.title || 'WebDesk ERP Notification';
            const options = {
                body: data.message || `${data.entityType || 'Task'} assigned to you.`,
                icon: '/favicon.ico',
                badge: '/favicon.ico',
                tag: 'webdesk-notif-' + (data.id || Date.now()),
                data: { url: data.targetUrl || '/viewtask' }
            };

            // Use Service Worker if available for robust background handling
            if (navigator.serviceWorker && navigator.serviceWorker.controller) {
                navigator.serviceWorker.ready.then(function (reg) {
                    reg.showNotification(title, options);
                }).catch(function () {
                    triggerFallbackNotification(title, options, data.targetUrl);
                });
            } else {
                triggerFallbackNotification(title, options, data.targetUrl);
            }
        }
    }

    function triggerFallbackNotification(title, options, targetUrl) {
        try {
            const notif = new Notification(title, options);
            notif.onclick = function () {
                window.focus();
                if (targetUrl) {
                    window.location.href = targetUrl;
                }
                notif.close();
            };
        } catch (e) {
            console.warn('[Notifications] Native notification fallback failed:', e);
        }
    }

    // --------------------------------------------------------------------------------------
    // 3. LIVE IN-APP TOAST POPUP NOTIFICATION
    // --------------------------------------------------------------------------------------
    function showInAppToast(data) {
        let container = document.getElementById('erpToastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'erpToastContainer';
            container.style.cssText = 'position: fixed; top: 20px; right: 20px; z-index: 999999; display: flex; flex-direction: column; gap: 10px; max-width: 380px; width: 100%; pointer-events: none;';
            document.body.appendChild(container);
        }

        const toast = document.createElement('div');
        toast.className = 'erp-toast shadow-lg animate__animated animate__fadeInRight';
        toast.style.cssText = 'pointer-events: auto; background: #FFFFFF; border-left: 4px solid var(--teal, #0F766E); border-radius: 12px; padding: 14px 16px; box-shadow: 0 10px 25px rgba(0,0,0,0.15); display: flex; gap: 12px; align-items: start; transition: all 0.3s ease; cursor: pointer;';

        const priorityBadge = data.priority === 'High' || data.priority === 'Urgent'
            ? `<span class="badge bg-danger" style="font-size: 0.68rem;">${data.priority}</span>`
            : '';

        let icon = 'fa-tasks';
        let iconBg = '#E6F4F1';
        let iconColor = '#0F766E';
        if (data.entityType && data.entityType.toLowerCase() === 'ticket') {
            icon = 'fa-ticket';
            iconBg = '#EFF6FF';
            iconColor = '#2563EB';
        } else if (data.entityType && data.entityType.toLowerCase() === 'subtask') {
            icon = 'fa-list-check';
            iconBg = '#F5F3FF';
            iconColor = '#7C3AED';
        }

        toast.innerHTML = `
            <div style="width: 38px; height: 38px; border-radius: 10px; background: ${iconBg}; color: ${iconColor}; display: flex; align-items: center; justify-content: center; flex-shrink: 0;">
                <i class="fa-solid ${icon}"></i>
            </div>
            <div style="flex-grow: 1; min-width: 0;">
                <div style="display: flex; align-items: center; justify-content: space-between; gap: 6px; margin-bottom: 2px;">
                    <strong style="font-size: 0.85rem; color: #0F172A; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${data.title || 'New Notification'}</strong>
                    ${priorityBadge}
                </div>
                <div style="font-size: 0.78rem; color: #64748B; margin-bottom: 4px; line-height: 1.3;">${data.message || ''}</div>
                <div style="font-size: 0.70rem; color: #94A3B8;">${data.timeAgo || 'Just now'}</div>
            </div>
            <button type="button" style="background: none; border: none; color: #94A3B8; font-size: 0.9rem; cursor: pointer; padding: 0;" aria-label="Close">&times;</button>
        `;

        toast.addEventListener('click', function (e) {
            if (e.target.tagName !== 'BUTTON') {
                if (data.targetUrl) {
                    window.location.href = data.targetUrl;
                }
            }
        });

        toast.querySelector('button').addEventListener('click', function (e) {
            e.stopPropagation();
            toast.style.opacity = '0';
            toast.style.transform = 'translateX(50px)';
            setTimeout(() => toast.remove(), 300);
        });

        container.appendChild(toast);

        // Auto remove after 6 seconds
        setTimeout(function () {
            if (toast.parentElement) {
                toast.style.opacity = '0';
                toast.style.transform = 'translateX(50px)';
                setTimeout(() => toast.remove(), 300);
            }
        }, 6000);
    }

    // --------------------------------------------------------------------------------------
    // 4. TOPBAR DROPDOWN PREPEND & BADGE INCREMENT
    // --------------------------------------------------------------------------------------
    function updateDropdownAndBadge(data) {
        const badge = $('#topbarNotificationBadge');
        let currentCount = parseInt(badge.text()) || 0;
        let newCount = currentCount + 1;
        badge.text(newCount > 99 ? '99+' : newCount).show();

        // Animate badge
        badge.addClass('animate__animated animate__bounce');
        setTimeout(() => badge.removeClass('animate__animated animate__bounce'), 1000);

        const list = $('#topbarNotificationList');
        if (list.length) {
            // Remove empty placeholder if any
            list.find('.text-muted.small').parent().remove();

            let iconClass = 'notif-icon-task';
            let icon = 'fa-tasks';
            if (data.entityType && data.entityType.toLowerCase() === 'ticket') {
                iconClass = 'notif-icon-ticket';
                icon = 'fa-ticket';
            } else if (data.entityType && data.entityType.toLowerCase() === 'subtask') {
                iconClass = 'notif-icon-subtask';
                icon = 'fa-list-check';
            }

            const itemHtml = `
                <div class="notification-item unread animate__animated animate__fadeInDown" data-id="${data.id || 0}" onclick="window.location.href='${data.targetUrl || '#'}'">
                    <div class="notif-icon ${iconClass}">
                        <i class="fa-solid ${icon}"></i>
                    </div>
                    <div class="notif-content">
                        <div class="notif-title">${data.title}</div>
                        <div class="notif-desc">${data.message || ''}</div>
                        <div class="notif-meta">
                            <span class="notif-time"><i class="fa-regular fa-clock me-1"></i>Just now</span>
                            ${data.priority ? `<span class="badge ${data.priority === 'High' || data.priority === 'Urgent' ? 'bg-danger' : 'bg-light text-dark border'}">${data.priority}</span>` : ''}
                        </div>
                    </div>
                </div>
            `;

            list.prepend(itemHtml);
        }
    }

    // --------------------------------------------------------------------------------------
    // 5. SIGNALR CONNECTION
    // --------------------------------------------------------------------------------------
    function initSignalR() {
        if (typeof signalR === 'undefined') {
            console.warn('[SignalR] @microsoft/signalr library not loaded yet. Retrying...');
            setTimeout(initSignalR, 1000);
            return;
        }

        const connection = new signalR.HubConnectionBuilder()
            .withUrl('/hubs/notifications')
            .withAutomaticReconnect([0, 2000, 5000, 10000, 30000])
            .configureLogging(signalR.LogLevel.Information)
            .build();

        connection.on('ReceiveNotification', function (data) {
            console.log('[SignalR] Real-time notification received:', data);
            playNotificationChime();
            showNativeDesktopNotification(data); // 🚀 Native OS Desktop notification popup
            showInAppToast(data);                // 🚀 In-app Toast
            
            if (typeof window.loadNotifications === 'function') {
                window.loadNotifications();
            } else {
                updateDropdownAndBadge(data);
            }
        });

        connection.start()
            .then(function () {
                console.log('[SignalR] Connected to Notification Hub successfully.');
            })
            .catch(function (err) {
                console.error('[SignalR] Connection error:', err.toString());
            });
    }

    // --------------------------------------------------------------------------------------
    // 6. BROWSER WEB PUSH API & SERVICE WORKER REGISTRATION
    // --------------------------------------------------------------------------------------
    function urlBase64ToUint8Array(base64String) {
        const padding = '='.repeat((4 - base64String.length % 4) % 4);
        const base64 = (base64String + padding)
            .replace(/\-/g, '+')
            .replace(/_/g, '/');

        const rawData = window.atob(base64);
        const outputArray = new Uint8Array(rawData.length);

        for (let i = 0; i < rawData.length; ++i) {
            outputArray[i] = rawData.charCodeAt(i);
        }
        return outputArray;
    }

    function initWebPush() {
        if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
            console.log('[WebPush] Push messaging is not supported in this browser.');
            return;
        }

        navigator.serviceWorker.register('/sw.js')
            .then(function (registration) {
                console.log('[WebPush] Service Worker registered with scope:', registration.scope);

                if (Notification.permission === 'granted') {
                    subscribeUserToPush(registration);
                } else if (Notification.permission !== 'denied') {
                    // Prompt user for notification permission
                    Notification.requestPermission().then(function (permission) {
                        if (permission === 'granted') {
                            subscribeUserToPush(registration);
                        }
                    });
                }
            })
            .catch(function (err) {
                console.warn('[WebPush] Service Worker registration failed:', err);
            });
    }

    function subscribeUserToPush(registration) {
        $.get('/notifications/get-vapid-public-key', function (res) {
            if (!res || !res.publicKey) return;

            try {
                const applicationServerKey = urlBase64ToUint8Array(res.publicKey);
                registration.pushManager.subscribe({
                    userVisibleOnly: true,
                    applicationServerKey: applicationServerKey
                })
                .then(function (subscription) {
                    console.log('[WebPush] User subscribed successfully:', subscription);
                    const subJson = subscription.toJSON();

                    $.ajax({
                        url: '/notifications/subscribe-push',
                        type: 'POST',
                        contentType: 'application/json',
                        data: JSON.stringify({
                            endpoint: subJson.endpoint,
                            keys: {
                                p256dh: subJson.keys.p256dh,
                                auth: subJson.keys.auth
                            }
                        }),
                        success: function (saveRes) {
                            console.log('[WebPush] Server saved subscription:', saveRes);
                        }
                    });
                })
                .catch(function (err) {
                    console.warn('[WebPush] Failed to subscribe user:', err);
                });
            } catch (ex) {
                console.warn('[WebPush] Error setting up subscription:', ex);
            }
        });
    }

    // Initialize on DOM ready
    $(document).ready(function () {
        initSignalR();
        initWebPush();
    });

})();
