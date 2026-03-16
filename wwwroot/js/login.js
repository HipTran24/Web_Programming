(function () {
  const form = document.getElementById("loginForm");
  if (!form) {
    return;
  }

  const tokenStorageKey = "auth.accessToken";
  const userStorageKey = "auth.currentUser";
  const googleIdentitySdkUrl = "https://accounts.google.com/gsi/client";
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

  const persistSession = (data, rememberMe) => {
    const token = data?.accessToken || "";
    if (!token) {
      return false;
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

    return true;
  };

  const setSubmitting = (submitting) => {
    if (!loginButton) {
      return;
    }

    loginButton.disabled = submitting;
    loginButton.textContent = submitting ? "Đang đăng nhập..." : "Đăng nhập";
  };

  const wait = (ms) =>
    new Promise((resolve) => {
      window.setTimeout(resolve, ms);
    });

  const ensureGoogleIdentitySdkReady = async (timeoutMs = 12000) => {
    if (window.google?.accounts?.id) {
      return true;
    }

    let sdkScript = document.querySelector(`script[src="${googleIdentitySdkUrl}"]`);
    if (!sdkScript) {
      sdkScript = document.createElement("script");
      sdkScript.src = googleIdentitySdkUrl;
      sdkScript.async = true;
      sdkScript.defer = true;
      document.head.appendChild(sdkScript);
    }

    const startedAt = Date.now();
    while (Date.now() - startedAt < timeoutMs) {
      if (window.google?.accounts?.id) {
        return true;
      }

      await wait(120);
    }

    return false;
  };

  const ensureCustomGoogleButtonOverlay = (googleButton) => {
    if (!googleButton) {
      return;
    }

    googleButton.classList.add("google-login-host--customized");

    const nativeButtonHost = googleButton.querySelector(":scope > div");
    if (nativeButtonHost) {
      nativeButtonHost.classList.add("google-login-native");
    }

    let overlay = googleButton.querySelector(".google-login-overlay");
    if (!overlay) {
      overlay = document.createElement("div");
      overlay.className = "google-login-overlay";
      overlay.setAttribute("aria-hidden", "true");
      overlay.innerHTML = `
        <span class="google-login-overlay__icon">
          <svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false">
            <path fill="#EA4335" d="M12 10.2v3.9h5.5c-.2 1.3-1.6 3.9-5.5 3.9-3.3 0-6-2.7-6-6s2.7-6 6-6c1.9 0 3.2.8 3.9 1.5l2.7-2.6C17 3.3 14.7 2.4 12 2.4 6.7 2.4 2.4 6.7 2.4 12S6.7 21.6 12 21.6c6.9 0 9.1-4.8 9.1-7.3 0-.5-.1-.9-.1-1.2H12Z"/>
            <path fill="#4285F4" d="M21.1 12.3c0-.5-.1-.9-.1-1.2H12v3.9h5.5c-.1.8-.6 2-1.7 2.8l2.6 2c1.6-1.5 2.7-3.8 2.7-6.5Z"/>
            <path fill="#FBBC05" d="M6 14.3c-.2-.6-.3-1.2-.3-1.8s.1-1.3.3-1.8l-3-2.3C2.6 9.5 2.4 10.7 2.4 12s.2 2.5.6 3.6l3-2.3Z"/>
            <path fill="#34A853" d="M12 21.6c2.7 0 5-.9 6.7-2.5l-3-2.3c-.8.6-1.9 1.1-3.7 1.1-2.6 0-4.9-1.8-5.7-4.1l-3 2.3c1.6 3.2 4.9 5.5 8.7 5.5Z"/>
          </svg>
        </span>
        <span class="google-login-overlay__label">Đăng nhập với Google</span>
      `;
      googleButton.appendChild(overlay);
    }
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

  const submitGoogleToken = async (idToken) => {
    if (isSubmitting) {
      return;
    }

    const rememberMe = !!rememberMeInput?.checked;
    isSubmitting = true;
    setSubmitting(true);
    setMessage("", false);

    try {
      const response = await fetch("/api/auth/google-login", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify({
          idToken,
          rememberMe,
        }),
      });

      const data = await readJsonSafely(response);
      if (!response.ok) {
        setMessage(data?.message || "Đăng nhập Google thất bại.", false);
        return;
      }

      if (!persistSession(data, rememberMe)) {
        setMessage("Đăng nhập thất bại: không nhận được token.", false);
        return;
      }

      window.sessionStorage.removeItem("pendingEmailVerification");
      setMessage("Đăng nhập Google thành công. Đang chuyển trang...", true);
      window.setTimeout(() => {
        window.location.href = returnUrl || "/home/index.html";
      }, 700);
    } catch (error) {
      console.error("google_login_failed", error);
      setMessage("Không thể kết nối tới máy chủ.", false);
    } finally {
      isSubmitting = false;
      setSubmitting(false);
    }
  };

  const setupGoogleLogin = async () => {
    const googleButton = document.getElementById("googleLoginButton");
    if (!googleButton) {
      return;
    }

    let config = null;
    try {
      const response = await fetch("/api/auth/google-config", {
        method: "GET",
        headers: {
          Accept: "application/json",
        },
      });

      config = await readJsonSafely(response);
      if (!response.ok || !config?.enabled || !config?.clientId) {
        setMessage(
          "Đăng nhập Google chưa được cấu hình trên môi trường này. Vui lòng dùng email/mật khẩu.",
          false,
        );
        return;
      }
    } catch (error) {
      console.error("google_config_failed", error);
      return;
    }

    const sdkReady = await ensureGoogleIdentitySdkReady();
    if (!sdkReady || !window.google?.accounts?.id) {
      setMessage("Không tải được dịch vụ đăng nhập Google. Vui lòng thử lại sau.", false);
      return;
    }

    window.google.accounts.id.initialize({
      client_id: config.clientId,
      callback: async (googleResponse) => {
        const idToken = googleResponse?.credential || "";
        if (!idToken) {
          setMessage("Không lấy được Google token hợp lệ.", false);
          return;
        }

        await submitGoogleToken(idToken);
      },
      ux_mode: "popup",
      use_fedcm_for_prompt: false,
      use_fedcm_for_button: false,
      auto_select: false,
      cancel_on_tap_outside: true,
    });

    const width = Math.max(240, Math.min(googleButton.clientWidth || 360, 400));
    window.google.accounts.id.renderButton(googleButton, {
      type: "standard",
      theme: "outline",
      size: "large",
      text: "signin_with",
      locale: "vi",
      shape: "pill",
      width,
      logo_alignment: "left",
    });

    window.setTimeout(() => {
      ensureCustomGoogleButtonOverlay(googleButton);
    }, 0);
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

      if (!persistSession(data, rememberMe)) {
        setMessage("Đăng nhập thất bại: không nhận được token.", false);
        return;
      }

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
  void setupGoogleLogin();
})();
