// ==========================================================================================
// WebDesk ERP Service Worker for Desktop / Browser Push Notifications
// ==========================================================================================

self.addEventListener('install', function (event) {
    self.skipWaiting();
});

self.addEventListener('activate', function (event) {
    event.waitUntil(self.clients.claim());
});

// Handle incoming Web Push from server
self.addEventListener('push', function (event) {
    let payload = {
        title: 'WebDesk ERP Notification',
        body: 'You have a new update in WebDesk ERP.',
        icon: '/favicon.ico',
        badge: '/favicon.ico',
        url: '/viewtask'
    };

    if (event.data) {
        try {
            const data = event.data.json();
            payload = {
                title: data.title || payload.title,
                body: data.body || payload.body,
                icon: data.icon || payload.icon,
                badge: data.badge || payload.badge,
                url: (data.data && data.data.url) || data.url || payload.url
            };
        } catch (e) {
            payload.body = event.data.text();
        }
    }

    const options = {
        body: payload.body,
        icon: payload.icon,
        badge: payload.badge,
        data: { url: payload.url },
        vibrate: [150, 50, 150],
        requireInteraction: false
    };

    event.waitUntil(
        self.registration.showNotification(payload.title, options)
    );
});

// Handle postMessage from client tab
self.addEventListener('message', function (event) {
    if (event.data && event.data.type === 'SHOW_NOTIFICATION') {
        const payload = event.data.payload || {};
        const title = payload.title || 'WebDesk ERP';
        const options = {
            body: payload.body || payload.message || 'New update available',
            icon: payload.icon || '/favicon.ico',
            badge: payload.badge || '/favicon.ico',
            data: { url: payload.url || payload.targetUrl || '/viewtask' },
            vibrate: [150, 50, 150]
        };

        event.waitUntil(
            self.registration.showNotification(title, options)
        );
    }
});

// Handle notification click -> Focus or open window
self.addEventListener('notificationclick', function (event) {
    event.notification.close();

    const targetUrl = (event.notification.data && event.notification.data.url) ? event.notification.data.url : '/';

    event.waitUntil(
        clients.matchAll({ type: 'window', includeUncontrolled: true }).then(function (clientList) {
            for (let i = 0; i < clientList.length; i++) {
                const client = clientList[i];
                if (client.url.includes(self.location.origin) && 'focus' in client) {
                    client.navigate(targetUrl);
                    return client.focus();
                }
            }
            if (clients.openWindow) {
                return clients.openWindow(targetUrl);
            }
        })
    );
});
