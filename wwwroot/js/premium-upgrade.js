(function () {
  const payButton = document.getElementById("momoPayButton");
  const openPremiumButton = document.getElementById("openPremiumButton");
  const notice = document.getElementById("premiumPaymentNotice");
  const statusText = document.getElementById("premiumStatusText");

  const showNotice = (message, kind) => {
    if (!notice) return;
    notice.textContent = message;
    notice.classList.remove("d-none", "is-error", "is-success");
    if (kind) {
      notice.classList.add(`is-${kind}`);
    }
  };

  const getAuthHeaders = () => {
    const token = window.AuthClient?.getAccessToken?.() || "";
    return token ? { Authorization: `Bearer ${token}` } : {};
  };

  const formatDate = (value) => {
    if (!value) return "";
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return "";
    return date.toLocaleDateString("vi-VN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
    });
  };

  const loadStatus = async () => {
    const response = await fetch("/api/payments/premium/status", {
      headers: {
        Accept: "application/json",
        ...getAuthHeaders(),
      },
    });

    if (!response.ok) {
      throw new Error("Không thể tải trạng thái Premium.");
    }

    const data = await response.json();
    if (data?.isPremium) {
      statusText.textContent = `Premium đang hoạt động đến ${formatDate(data.expiresAt) || "ngày hết hạn"}.`;
      openPremiumButton?.classList.remove("d-none");
      if (payButton) {
        payButton.classList.add("d-none");
        payButton.disabled = true;
      }
      showNotice("Tài khoản đã có Premium, không cần thanh toán.", "success");
      window.location.replace("/premium/account.html");
      return;
    }

    statusText.textContent = "Tài khoản chưa có Premium. Thanh toán để mở khóa.";
  };

  const createPayment = async () => {
    if (!payButton) return;
    payButton.disabled = true;
    payButton.textContent = "Đang tạo link MoMo...";

    try {
      const response = await fetch("/api/payments/momo/create", {
        method: "POST",
        headers: {
          Accept: "application/json",
          "Content-Type": "application/json",
          ...getAuthHeaders(),
        },
        body: JSON.stringify({ planCode: "PREMIUM_30D" }),
      });
      const data = await response.json().catch(() => ({}));

      if (!response.ok || !data?.success || !data?.payUrl) {
        throw new Error(data?.message || "MoMo chưa tạo được link thanh toán.");
      }

      window.location.href = data.payUrl;
    } catch (error) {
      showNotice(error.message || "Có lỗi khi tạo thanh toán MoMo.", "error");
      payButton.disabled = false;
      payButton.textContent = "Thanh toán bằng MoMo";
    }
  };

  const showReturnMessage = () => {
    const params = new URLSearchParams(window.location.search);
    const payment = params.get("payment");
    if (payment === "success") {
      showNotice(params.get("message") || "MoMo đã redirect về thành công. Hệ thống sẽ mở Premium sau khi giao dịch được xác nhận.", "success");
    } else if (payment === "failed") {
      showNotice(params.get("message") || "Giao dịch MoMo chưa thành công.", "error");
    }
  };

  payButton?.addEventListener("click", createPayment);

  window.AuthClient?.whenReady?.()
    .then(() => {
      showReturnMessage();
      return loadStatus();
    })
    .catch((error) => {
      statusText.textContent = "Không thể kiểm tra Premium.";
      showNotice(error.message || "Có lỗi khi tải trạng thái Premium.", "error");
    });
})();
