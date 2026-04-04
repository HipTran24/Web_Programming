(function () {
  const form = document.getElementById("loginForm");
  if (!form) {
    return;
  }

  const tokenStorageKey = "auth.accessToken";
  const userStorageKey = "auth.currentUser";
  const googleIdentitySdkUrl = "https://accounts.google.com/gsi/client";
  const identifierInput = document.getElementById("username") || document.getElementById("email");
  const passwordInput = document.getElementById("password");
  const rememberMeInput = document.getElementById("rememberMe");
  const passwordToggleBtn = document.getElementById("passwordToggleBtn");
  const passwordToggleIcon = document.getElementById("loginPasswordEyeIcon");
    // Toggle show/hide password
    if (passwordToggleBtn && passwordInput) {
      passwordToggleBtn.addEventListener("click", function () {
        const isVisible = passwordInput.type === "text";
        passwordInput.type = isVisible ? "password" : "text";
        passwordToggleBtn.classList.toggle("is-visible", !isVisible);
        // Optional: change icon if you want (not required for basic eye)
      });
    }
  const loginButton = document.getElementById("loginButton");
  const alertBox = document.getElementById("alertBox");
  const alertMsg = document.getElementById("alertMsg");
  let returnUrl = "/dashboard";
  let isSubmitting = false;

  const getDefaultLandingByRole = (role) => {
    const normalizedRole = String(role || "").trim().toLowerCase();
    if (normalizedRole === "admin") {
      return "/admin";
    }

    return "/dashboard";
  };

  const normalizeReturnUrl = (value) => {
    const raw = String(value || "").trim();
    if (!raw) {
      return "/dashboard";
    }

    if (!raw.startsWith("/") || raw.startsWith("//")) {
      return "/dashboard";
    }

    const normalizedRaw = raw.toLowerCase();
    if (
      normalizedRaw === "/" ||
      normalizedRaw === "/home" ||
      normalizedRaw === "/home/" ||
      normalizedRaw.startsWith("/home/login.html") ||
      normalizedRaw.startsWith("/home/index.html")
    ) {
      return "/dashboard";
    }

    return raw;
  };

  const resolveReturnUrlForRole = (role, requestedUrl) => {
    const normalizedRole = String(role || "").trim().toLowerCase();
    const normalizedRequestedUrl = normalizeReturnUrl(requestedUrl);
    const isAdminRequest = normalizedRequestedUrl === "/admin" || normalizedRequestedUrl.startsWith("/admin/");
    const isUserDashboardRequest = normalizedRequestedUrl === "/dashboard" || normalizedRequestedUrl.startsWith("/dashboard/");

    if (normalizedRole === "admin") {
      if (isUserDashboardRequest) {
        return "/admin";
      }

      return normalizedRequestedUrl || "/admin";
    }

    if (isAdminRequest) {
      return "/dashboard";
    }

    return normalizedRequestedUrl || "/dashboard";
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
      input.classList.remove("is-valid");
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
      input.classList.remove("is-valid");
    }

    if (error) {
      error.textContent = "";
    }
  };

  const getIdentifierFieldId = () => (document.getElementById("username") ? "username" : "email");

  const clearErrors = () => {
    [getIdentifierFieldId(), "password"].forEach(clearFieldError);
  };

  const persistSession = (data, rememberMe) => {
    if (window.AuthClient?.storeSession) {
      return Boolean(window.AuthClient.storeSession(data, rememberMe));
    }

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
    storage.setItem("token", token);
    storage.setItem("role", data?.role ?? "");
    storage.setItem("name", data?.fullName || data?.username || data?.email || "");
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

  const getTargetAfterLogin = (responseData) => {
    return resolveReturnUrlForRole(responseData?.role, returnUrl || getDefaultLandingByRole(responseData?.role));
  };

  const setSubmitting = (submitting) => {
    if (!loginButton) {
      return;
    }

    loginButton.disabled = submitting;
    loginButton.classList.toggle("is-loading", submitting);
    loginButton.setAttribute("aria-busy", submitting ? "true" : "false");
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

  const setupPasswordToggle = () => {
    const toggleButton = document.getElementById("togglePwd");
    const eyeIcon = document.getElementById("eyeIcon");

    if (!toggleButton || !passwordInput || !eyeIcon) {
      return;
    }

    toggleButton.addEventListener("click", () => {
      const show = passwordInput.type === "password";
      passwordInput.type = show ? "text" : "password";
      toggleButton.classList.toggle("is-visible", show);
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
        window.location.replace(getTargetAfterLogin(data));
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

    const measuredWidth = googleButton.clientWidth || googleButton.getBoundingClientRect().width || 0;
    const buttonWidth = Math.min(Math.max(Math.round(measuredWidth), 220), 420);

    window.google.accounts.id.renderButton(googleButton, {
      type: "standard",
      theme: "outline",
      size: "large",
      text: "signin_with",
      locale: "vi",
      shape: "pill",
      width: buttonWidth,
      logo_alignment: "left",
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

  const setupPostLogoutBackBehavior = () => {
    const query = new URLSearchParams(window.location.search);
    if (query.get("loggedOut") !== "1") {
      return;
    }

    window.history.pushState({ loggedOutLanding: true }, "", window.location.href);
    window.addEventListener("popstate", () => {
      window.location.replace("/home/index.html");
    }, { once: true });
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
      setFieldError(getIdentifierFieldId(), "Vui lòng nhập email hoặc tên đăng nhập.");
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
                getIdentifierFieldId(),
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
        window.location.replace(getTargetAfterLogin(data));
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
    identifierInput.addEventListener("input", () => {
      clearFieldError(getIdentifierFieldId());
      if ((identifierInput.value || "").trim()) {
        identifierInput.classList.add("is-valid");
      }
    });
  }

  if (passwordInput) {
    passwordInput.addEventListener("input", () => {
      clearFieldError("password");
      if ((passwordInput.value || "").trim()) {
        passwordInput.classList.add("is-valid");
      }
    });
  }

  prefillFromQuery();
  setupPostLogoutBackBehavior();
  setupPasswordToggle();
  void setupGoogleLogin();
})();
