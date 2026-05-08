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

  const fetchJson = async (url, options = {}) => {
    const response = await fetch(url, {
      credentials: "include",
      headers: {
        "Content-Type": "application/json",
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
      return;
    }

    try {
      renderStatus(await fetchJson("/api/premium/status"));
    } catch (error) {
      showMessage(error.message || "Vui lòng đăng nhập để xem trạng thái gói.", "warning");
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
          const checkout = await fetchJson("/api/premium/checkout", {
            method: "POST",
            body: JSON.stringify({ planName: "Premium" }),
          });

          setText("[data-premium-price]", money.format(checkout.amount || 0));
          window.location.href = checkout.checkoutUrl;
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

    const transactionId = new URLSearchParams(window.location.search).get("transactionId");
    if (!transactionId) {
      showMessage("Thiếu mã giao dịch để kích hoạt Premium.", "danger");
      return;
    }

    try {
      const payment = await fetchJson(`/api/premium/payments/${transactionId}/success`, { method: "POST" });
      setText("[data-payment-id]", payment.paymentTransactionId);
      setText("[data-payment-status]", "Thành công");
      await loadStatus();
    } catch (error) {
      showMessage(error.message || "Chưa thể xác nhận thanh toán.", "danger");
    }
  };

  const failPayment = async () => {
    const root = document.querySelector("[data-payment-failed]");
    if (!root) {
      return;
    }

    const transactionId = new URLSearchParams(window.location.search).get("transactionId");
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
