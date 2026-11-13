// Module dùng chung. Mỗi trang chỉ cần đăng ký callback rồi gọi start().
window.notificationClient = (function () {
    let connection;
    let messageCallback = function () {};
    let feedbackCallback = function () {};
    let statusCallback = function () {};

    async function start() {
        if (!window.signalR) {
            throw new Error("Không tải được thư viện SignalR. Kiểm tra kết nối mạng rồi tải lại trang.");
        }

        if (connection) {
            return;
        }

        connection = new signalR.HubConnectionBuilder()
            .withUrl("/hubs/notifications") // Trình duyệt tự gửi cookie đăng nhập.
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveNotification", function (message) {
            messageCallback(message);
        });
        connection.on("FeedbackChanged", function (update) {
            feedbackCallback(update);
        });

        connection.onreconnecting(function () {
            statusCallback("Đang kết nối lại...", false);
        });

        connection.onreconnected(function () {
            statusCallback("Đã kết nối lại.", true);
        });

        connection.onclose(function () {
            connection = null;
            statusCallback("Đã ngắt kết nối. Kiểm tra đăng nhập rồi tải lại trang.", false);
        });

        try {
            await connection.start();
            statusCallback("Đã kết nối SignalR.", true);
        } catch (error) {
            connection = null;
            statusCallback("Không kết nối được. Kiểm tra đăng nhập rồi tải lại trang.", false);
            throw error;
        }
    }

    return {
        start: start,
        onMessage: function (callback) { messageCallback = callback; },
        onFeedback: function (callback) { feedbackCallback = callback; },
        onStatus: function (callback) { statusCallback = callback; }
    };
})();
