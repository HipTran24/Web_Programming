(function () {
  const payButton = document.getElementById("payosPayButton");
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
      throw new Error("Khong the tai trang thai Premium.");
    }

    const data = await response.json();
    if (data?.isPremium) {
      statusText.textContent = `Premium dang hoat dong den ${formatDate(data.expiresAt) || "ngay het han"}.`;
      openPremiumButton?.classList.remove("d-none");
      if (payButton) {
        payButton.classList.add("d-none");
        payButton.disabled = true;
      }
      showNotice("Tai khoan da co Premium, khong can thanh toan.", "success");
      window.location.replace("/premium/account.html");
      return;
    }

    statusText.textContent = "Tai khoan chua co Premium. Thanh toan de mo khoa.";
  };

  const createPayment = async () => {
    if (!payButton) return;
    payButton.disabled = true;
    payButton.textContent = "Dang tao link PayOS...";

    try {
      const response = await fetch("/api/payments/payos/create", {
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
        throw new Error(data?.message || "PayOS chua tao duoc link thanh toan.");
      }

      window.location.href = data.payUrl;
    } catch (error) {
      showNotice(error.message || "Co loi khi tao thanh toan PayOS.", "error");
      payButton.disabled = false;
      payButton.textContent = "Thanh toan bang PayOS";
    }
  };

  const showReturnMessage = () => {
    const params = new URLSearchParams(window.location.search);
    const payment = params.get("payment");
    if (payment === "success") {
      showNotice(params.get("message") || "PayOS da redirect ve thanh cong. He thong se mo Premium sau khi webhook duoc xac nhan.", "success");
    } else if (payment === "failed") {
      showNotice(params.get("message") || "Giao dich PayOS chua thanh cong.", "error");
    }
  };

  payButton?.addEventListener("click", createPayment);

  window.AuthClient?.whenReady?.()
    .then(() => {
      showReturnMessage();
      return loadStatus();
    })
    .catch((error) => {
      statusText.textContent = "Khong the kiem tra Premium.";
      showNotice(error.message || "Co loi khi tai trang thai Premium.", "error");
    });
})();
