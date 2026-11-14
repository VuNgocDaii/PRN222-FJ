(() => {
    const scopeLabels = { SYSTEM: "Toàn hệ thống", SELF: "Cá nhân", AREA: "Xã", GROUP: "Thôn" };
    const verificationLabels = { PENDING: "Chờ xác minh", APPROVED: "Đã xác minh", REJECTED: "Chưa được duyệt", NOT_APPLICABLE: "Không áp dụng" };
    const instant = value => new Date(/Z$|[+-]\d{2}:\d{2}$/.test(value) ? value : value + "Z");
    window.accountDisplay = {
        scope: value => scopeLabels[value] || "Chưa xác định",
        verification: value => verificationLabels[value] || "Chưa xác định",
        date: value => value ? instant(value).toLocaleString("vi-VN") : "Không giới hạn"
    };
})();
