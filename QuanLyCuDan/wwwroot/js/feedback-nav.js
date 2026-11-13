(function () {
    const badges = Array.from(document.querySelectorAll("[data-feedback-count]"));
    const status = document.getElementById("feedback-live-status");
    if (!badges.length && !status) return;

    const seen = new Set();
    if (window.notificationClient) {
        notificationClient.onFeedback(function (update) {
            if (update.requestId == null) return;
            const eventKey = update.eventType + ":" + update.requestId;
            if (seen.has(eventKey)) return;
            seen.add(eventKey);
            if (update.eventType === "created") {
                badges.forEach(function (badge) {
                    badge.textContent = String((Number.parseInt(badge.textContent, 10) || 0) + 1);
                    badge.hidden = false;
                });
            } else if (update.eventType === "accepted") {
                badges.forEach(function (badge) {
                    const nextCount = Math.max(0, (Number.parseInt(badge.textContent, 10) || 0) - 1);
                    badge.textContent = String(nextCount);
                    badge.hidden = nextCount === 0;
                });
            }
        });

        if (status) {
            notificationClient.onStatus(function (message, connected) {
                status.textContent = connected ? "Đang cập nhật trực tiếp" : message;
                status.className = "badge " + (connected ? "text-bg-success" : "text-bg-secondary");
            });
        }
        notificationClient.start().catch(function () {});
    }
})();
