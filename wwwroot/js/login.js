(function () {
  const form = document.getElementById("loginForm");
  if (!form) {
    return;
  }

  const tokenStorageKey = "auth.accessToken";
  const userStorageKey = "auth.currentUser";
  const identifierInput = document.getElementById("email") || document.getElementById("emailOrUsername");
  const passwordInput = document.getElementById("password");
  const rememberMeInput = document.getElementById("rememberMe") || document.getElementById("remember");
  const loginButton = document.getElementById("loginButton") || form.querySelector('button[type="submit"]');
  const alertBox = document.getElementById("alertBox");
  const alertMsg = document.getElementById("alertMsg");
  const messageElement = document.getElementById("loginMessage");
  let returnUrl = "index.html";
  let isSubmitting = false;

  const setMessage = (message, isSuccess) => {
    if (alertBox && alertMsg) {
      alertMsg.textContent = message || "";
      alertBox.className = `alert ${isSuccess ? "alert-success" : "alert-error"}${message ? " show" : ""}`;
      return;
    }

    if (!messageElement) {
      return;
    }

    messageElement.textContent = message || "";
    messageElement.className = `small mb-3 ${isSuccess ? "text-success" : "text-warning"}`;
  };

  const setFieldError = (fieldId, message) => {
    const input = document.getElementById(fieldId);
    const error = document.getElementById(`${fieldId}Error`);

    if (input) {
      input.classList.add("error");
    }

    if (error) {
      error.textContent = message || "";
      error.classList.add("show");
    }
  };

  const clearFieldError = (fieldId) => {
    const input = document.getElementById(fieldId);
    const error = document.getElementById(`${fieldId}Error`);

    if (input) {
      input.classList.remove("error");
    }

    if (error) {
      error.textContent = "";
      error.classList.remove("show");
    }
  };

  const clearErrors = () => {
    ["email", "emailOrUsername", "password"].forEach(clearFieldError);
  };

  const setSubmitting = (isSubmitting) => {
    if (!loginButton) {
      return;
    }

    loginButton.disabled = isSubmitting;
    loginButton.textContent = isSubmitting ? "Đang đăng nhập..." : "Đăng nhập";
  };

  const setupPasswordToggle = () => {
    const toggleButton = document.getElementById("togglePwd");
    const eyeIcon = document.getElementById("eyeIcon");

    if (toggleButton && passwordInput && eyeIcon) {
      toggleButton.addEventListener("click", () => {
        const show = passwordInput.type === "password";
        passwordInput.type = show ? "text" : "password";
        eyeIcon.innerHTML = show
          ? '<path stroke="currentColor" stroke-width="2" d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19M1 1l22 22"/>'
          : '<path stroke="currentColor" stroke-width="2" d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3" stroke="currentColor" stroke-width="2"/>';
      });
      return;
    }

    const toggleCheckbox = document.getElementById("toggleLoginPassword");
    if (toggleCheckbox && passwordInput) {
      toggleCheckbox.addEventListener("change", () => {
        passwordInput.type = toggleCheckbox.checked ? "text" : "password";
      });
    }
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
      returnUrl = requestedReturn;
    }

    if (identifierInput && email) {
      identifierInput.value = email;
    }

    if (verified === "1") {
      setMessage("Email đã được xác thực. Bạn có thể đăng nhập ngay.", true);
    }

    if (notice) {
      setMessage(notice, false);
    }
  };

  const redirectIfAlreadyLoggedIn = () => {
    const hasToken =
      !!window.localStorage.getItem(tokenStorageKey) ||
      !!window.sessionStorage.getItem(tokenStorageKey);

    if (!hasToken) {
      return;
    }

    window.location.href = returnUrl || "index.html";
  };

  const handleLogin = async (identifier, password) => {
    if (isSubmitting) {
      return;
    }

    clearErrors();
    setMessage("", false);

    const emailOrUsername = (identifier || "").trim();
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
            const fieldName = String(field || "").toLowerCase();
            const firstMessage = Array.isArray(messages)
              ? String(messages[0] || "")
              : String(messages || "");

            if (fieldName.includes("emailorusername") || fieldName.includes("email") || fieldName.includes("username")) {
              setFieldError("email", firstMessage || "Thông tin đăng nhập chưa hợp lệ.");
            }

            if (fieldName.includes("password")) {
              setFieldError("password", firstMessage || "Thông tin đăng nhập chưa hợp lệ.");
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
      const otherStorage = rememberMe ? window.sessionStorage : window.localStorage;

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
        })
      );

      window.sessionStorage.removeItem("pendingEmailVerification");
      setMessage("Đăng nhập thành công. Đang chuyển trang...", true);
      window.setTimeout(() => {
        window.location.href = returnUrl || "index.html";
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
    await handleLogin(identifierInput?.value || "", passwordInput?.value || "");
  });

  if (identifierInput) {
    identifierInput.addEventListener("input", () => {
      clearFieldError("email");
      clearFieldError("emailOrUsername");
    });
  }

  if (passwordInput) {
    passwordInput.addEventListener("input", () => {
      clearFieldError("password");
    });
  }

  window.handleLogin = handleLogin;

  prefillFromQuery();
  redirectIfAlreadyLoggedIn();
  setupPasswordToggle();
})();
