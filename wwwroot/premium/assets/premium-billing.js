(() => {
  const nf = new Intl.NumberFormat("vi-VN");
  const money = new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  });

  const setText = (selector, value) => {
    document.querySelectorAll(selector).forEach((node) => {
      node.textContent = value;
    });
  };

  const showMessage = (message, tone = "info") => {
    document.querySelectorAll("[data-premium-message]").forEach((node) => {
      node.textContent = message;
      node.dataset.tone = tone;
      node.hidden = false;
    });
  };

  const redirectToMain = (delayMs = 3500) => {
    window.setTimeout(() => {
      window.location.href = "/premium/dashboard.html";
    }, delayMs);
  };

  const getAuthHeaders = () => {
    const token = window.AuthClient?.getAccessToken?.() || "";
    return token ? { Authorization: `Bearer ${token}` } : {};
  };

  const fetchJson = async (url, options = {}) => {
    const response = await fetch(url, {
      credentials: "include",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json",
        ...getAuthHeaders(),
        ...(options.headers || {}),
      },
      ...options,
    });

    const payload = await response.json().catch(() => ({}));
    if (!response.ok) {
      throw new Error(payload.message || "Không thể xử lý yêu cầu.");
    }

    return payload;
  };

  const renderStatus = (status) => {
    const limit = status.dailyTokenLimit || 200000;
    const used = status.tokenUsedToday || 0;
    const percent = Math.min(100, Math.round((used / limit) * 100));
    const tier = status.isPremium
      ? (status.subscriptionTier ? String(status.subscriptionTier).trim() : "Premium")
      : "Gói thường";

    setText("[data-premium-tier]", tier);
    setText("[data-premium-token-used]", nf.format(used));
    setText("[data-premium-token-limit]", nf.format(limit));
    setText("[data-premium-token-remaining]", nf.format(Math.max(0, limit - used)));
    setText("[data-premium-token-percent]", `${percent}%`);
    setText("[data-premium-started]", status.premiumStartedAt ? new Date(status.premiumStartedAt).toLocaleDateString("vi-VN") : "Chưa kích hoạt");
    setText(
      "[data-premium-expires]",
      status.premiumExpiresAt
        ? new Date(status.premiumExpiresAt).toLocaleDateString("vi-VN")
        : status.isPremium
          ? "Không giới hạn"
          : "Chưa có",
    );

    document.querySelectorAll("[data-premium-meter-fill]").forEach((node) => {
      node.style.width = `${percent}%`;
    });

    document.querySelectorAll("[data-normal-only]").forEach((node) => {
      node.hidden = status.isPremium;
    });

    document.querySelectorAll("[data-premium-only]").forEach((node) => {
      node.hidden = !status.isPremium;
    });
  };

  const loadStatus = async () => {
    if (!document.querySelector("[data-premium-status]")) {
      return null;
    }

    try {
      const status = await fetchJson("/api/premium/status");
      renderStatus(status);
      return status;
    } catch (error) {
      showMessage(error.message || "Vui lòng đăng nhập để xem trạng thái gói.", "warning");
      return null;
    }
  };

  const wireCheckout = () => {
    document.querySelectorAll("[data-premium-checkout]").forEach((button) => {
      button.addEventListener("click", async () => {
        button.disabled = true;
        button.classList.add("is-loading");
        const original = button.textContent;
        button.textContent = "Đang tạo giao dịch...";

        try {
          const checkout = await fetchJson("/api/payments/payos/create", {
            method: "POST",
            body: JSON.stringify({ planCode: "PREMIUM_30D" }),
          });

          if (!checkout?.success || !checkout?.payUrl) {
            throw new Error(checkout?.message || "PayOS chưa tạo được liên kết thanh toán.");
          }

          setText("[data-premium-price]", money.format(checkout.amount || 0));
          window.location.href = checkout.payUrl;
        } catch (error) {
          button.disabled = false;
          button.classList.remove("is-loading");
          button.textContent = original;
          showMessage(error.message || "Không tạo được giao dịch thanh toán.", "danger");
        }
      });
    });
  };

  const completePayment = async () => {
    const root = document.querySelector("[data-payment-success]");
    if (!root) {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const transactionId = params.get("transactionId");
    const orderId = params.get("orderId") || params.get("orderCode");
    const message = params.get("message");

    if (orderId) {
      setText("[data-payment-id]", orderId);
    }

    if (message) {
      showMessage(message, "info");
    }

    const wait = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
    const pollStatus = async () => {
      for (let attempt = 0; attempt < 5; attempt += 1) {
        const status = await loadStatus();
        if (status?.isPremium) {
          return status;
        }
        await wait(2000);
      }
      return null;
    };

    try {
      if (transactionId) {
        const payment = await fetchJson(`/api/premium/payments/${transactionId}/success`, { method: "POST" });
        setText("[data-payment-id]", payment.paymentTransactionId);
        setText("[data-payment-status]", "Thành công");
        showMessage("Thanh toán thành công. Cảm ơn bạn đã nâng cấp Premium. Hệ thống sẽ quay lại màn hình chính sau vài giây.", "success");
        redirectToMain();
        await loadStatus();
        return;
      }

      setText("[data-payment-status]", "Đang xác nhận");
      const status = await pollStatus();
      if (status?.isPremium) {
        setText("[data-payment-status]", "Thành công");
        showMessage("Thanh toán thành công. Cảm ơn bạn đã nâng cấp Premium. Hệ thống sẽ quay lại màn hình chính sau vài giây.", "success");
        redirectToMain();
      } else if (!message) {
        showMessage("Hệ thống đang xác nhận thanh toán PayOS. Vui lòng đợi vài phút và tải lại trang.", "info");
      }
    } catch (error) {
      showMessage(error.message || "Chưa thể xác nhận thanh toán.", "danger");
    }
  };

  const failPayment = async () => {
    const root = document.querySelector("[data-payment-failed]");
    if (!root) {
      return;
    }

    const params = new URLSearchParams(window.location.search);
    const transactionId = params.get("transactionId");
    const message = params.get("message");

    if (message) {
      showMessage(message, "warning");
    }

    if (!transactionId) {
      return;
    }

    try {
      await fetchJson(`/api/premium/payments/${transactionId}/failed?status=Cancelled`, { method: "POST" });
    } catch {
      // The failure page remains useful even if the pending transaction was already closed.
    }
  };

  loadStatus();
  wireCheckout();
  completePayment();
  failPayment();
})();
