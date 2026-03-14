(function () {
  const form = document.getElementById("loginForm");
  if (!form) {
    return;
  }

  const tokenStorageKey = "auth.accessToken";
  const userStorageKey = "auth.currentUser";
  const identifierInput = document.getElementById("email");
  const passwordInput = document.getElementById("password");
  const rememberMeInput = document.getElementById("rememberMe");
  const loginButton = document.getElementById("loginButton");
  const alertBox = document.getElementById("alertBox");
  const alertMsg = document.getElementById("alertMsg");
  let returnUrl = "/home/index.html";
  let isSubmitting = false;

  const normalizeReturnUrl = (value) => {
    const raw = String(value || "").trim();
    if (!raw) {
      return "/home/index.html";
    }

    if (!raw.startsWith("/") || raw.startsWith("//")) {
      return "/home/index.html";
    }

    if (raw.startsWith("login.html")) {
      return "/home/index.html";
    }

    return raw;
  };

  const setMessage = (message, isSuccess) => {
    if (!alertBox || !alertMsg) {
      return;
    }

    alertMsg.textContent = message || "";
    alertBox.className = `alert ${isSuccess ? "alert-success" : "alert-danger"}${message ? " d-block" : " d-none"}`;
  };

  const setFieldError = (fieldId, message) => {
    const input = document.getElementById(fieldId);
    const error = document.getElementById(`${fieldId}Error`);

    if (input) {
      input.classList.add("is-invalid");
    }

    if (error) {
      error.textContent = message || "";
    }
  };

  const clearFieldError = (fieldId) => {
    const input = document.getElementById(fieldId);
    const error = document.getElementById(`${fieldId}Error`);

    if (input) {
      input.classList.remove("is-invalid");
    }

    if (error) {
      error.textContent = "";
    }
  };

  const clearErrors = () => {
    ["email", "password"].forEach(clearFieldError);
  };

  const setSubmitting = (submitting) => {
    if (!loginButton) {
      return;
    }

    loginButton.disabled = submitting;
    loginButton.textContent = submitting ? "Đang đăng nhập..." : "Đăng nhập";
  };

  const setupPasswordToggle = () => {
    const toggleButton = document.getElementById("togglePwd");
    const eyeIcon = document.getElementById("eyeIcon");

    if (!toggleButton || !passwordInput || !eyeIcon) {
      return;
    }

    toggleButton.addEventListener("click", () => {
      const show = passwordInput.type === "password";
      passwordInput.type = show ? "text" : "password";
      eyeIcon.innerHTML = show
        ? '<path stroke="currentColor" stroke-width="2" d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19M1 1l22 22"/>'
        : '<path stroke="currentColor" stroke-width="2" d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>';
    });
  };

  const setupGoogleLogin = () => {
    const googleButton = document.getElementById("googleLoginButton");
    if (!googleButton) {
      return;
    }

    googleButton.addEventListener("click", () => {
      setMessage(
        "Đăng nhập Google chưa được cấu hình trên môi trường này. Vui lòng dùng email/mật khẩu.",
        false,
      );
    });
  };

  const readJsonSafely = async (response) => {
    const contentType = response.headers.get("content-type") || "";
    if (!contentType.toLowerCase().includes("application/json")) {
      return null;
    }

    try {
      return await response.json();
    } catch {
      return null;
    }
  };

  const prefillFromQuery = () => {
    const query = new URLSearchParams(window.location.search);
    const email = (query.get("email") || "").trim();
    const verified = query.get("verified");
    const notice = (query.get("message") || "").trim();
    const requestedReturn = (query.get("returnUrl") || "").trim();

    if (requestedReturn) {
      returnUrl = normalizeReturnUrl(requestedReturn);
    }

    if (identifierInput && email) {
      identifierInput.value = email;
    }

    if (verified === "1") {
      setMessage("Email đã được xác thực. Bạn có thể đăng nhập ngay.", true);
      return;
    }

    if (notice) {
      setMessage(notice, false);
    }
  };

  const handleLogin = async () => {
    if (isSubmitting) {
      return;
    }

    clearErrors();
    setMessage("", false);

    const emailOrUsername = (identifierInput?.value || "").trim();
    const password = passwordInput?.value || "";
    const rememberMe = !!rememberMeInput?.checked;

    if (!emailOrUsername) {
      setFieldError("email", "Vui lòng nhập email hoặc tên đăng nhập.");
      return;
    }

    if (!password) {
      setFieldError("password", "Vui lòng nhập mật khẩu.");
      return;
    }

    isSubmitting = true;
    setSubmitting(true);

    try {
      const response = await fetch("/api/auth/login", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          emailOrUsername,
          password,
          rememberMe,
        }),
      });

      const data = await readJsonSafely(response);
      if (!response.ok) {
        if (data?.errors && typeof data.errors === "object") {
          Object.entries(data.errors).forEach(([field, messages]) => {
            const key = String(field || "").toLowerCase();
            const firstMessage = Array.isArray(messages)
              ? String(messages[0] || "")
              : String(messages || "");

            if (
              key.includes("emailorusername") ||
              key.includes("email") ||
              key.includes("username")
            ) {
              setFieldError(
                "email",
                firstMessage || "Thông tin đăng nhập chưa hợp lệ.",
              );
            }
            if (key.includes("password")) {
              setFieldError(
                "password",
                firstMessage || "Thông tin đăng nhập chưa hợp lệ.",
              );
            }
          });

          setMessage(data.title || "Thông tin đăng nhập chưa hợp lệ.", false);
          return;
        }

        setMessage(data?.message || "Đăng nhập thất bại.", false);
        return;
      }

      const token = data?.accessToken || "";
      if (!token) {
        setMessage("Đăng nhập thất bại: không nhận được token.", false);
        return;
      }

      const storage = rememberMe ? window.localStorage : window.sessionStorage;
      const otherStorage = rememberMe
        ? window.sessionStorage
        : window.localStorage;
      otherStorage.removeItem(tokenStorageKey);
      otherStorage.removeItem(userStorageKey);

      storage.setItem(tokenStorageKey, token);
      storage.setItem(
        userStorageKey,
        JSON.stringify({
          userId: data?.userId ?? null,
          username: data?.username ?? "",
          fullName: data?.fullName ?? "",
          email: data?.email ?? "",
          role: data?.role ?? "",
          expiresAt: data?.expiresAt ?? null,
        }),
      );

      window.sessionStorage.removeItem("pendingEmailVerification");
      setMessage("Đăng nhập thành công. Đang chuyển trang...", true);
      window.setTimeout(() => {
        window.location.href = returnUrl || "/home/index.html";
      }, 700);
    } catch (error) {
      console.error("login_failed", error);
      setMessage("Không thể kết nối tới máy chủ.", false);
    } finally {
      isSubmitting = false;
      setSubmitting(false);
    }
  };

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    await handleLogin();
  });

  if (identifierInput) {
    identifierInput.addEventListener("input", () => clearFieldError("email"));
  }

  if (passwordInput) {
    passwordInput.addEventListener("input", () => clearFieldError("password"));
  }

  prefillFromQuery();
  setupPasswordToggle();
  setupGoogleLogin();
})();
