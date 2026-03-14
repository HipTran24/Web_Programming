(function () {
  const pendingVerificationKey = "pendingEmailVerification";
  const form = document.getElementById("registerForm");
  if (!form) {
    return;
  }

  const fieldIds = [
    "username",
    "fullName",
    "email",
    "password",
    "confirmPassword",
    "acceptTerms",
  ];

  const messageElement = document.getElementById("registerMessage");
  const registerButton = document.getElementById("registerButton");

  const clearErrors = () => {
    for (const fieldId of fieldIds) {
      const errorElement = document.getElementById(`${fieldId}Error`);
      if (errorElement) {
        errorElement.textContent = "";
      }
    }

    for (const inputId of ["username", "fullName", "email", "password", "confirmPassword"]) {
      const input = document.getElementById(inputId);
      if (input) {
        input.classList.remove("is-invalid");
      }
    }
  };

  const setFieldError = (fieldName, message) => {
    const errorElement = document.getElementById(`${fieldName}Error`);
    if (errorElement) {
      errorElement.textContent = message;
    }

    if (fieldName !== "acceptTerms") {
      const input = document.getElementById(fieldName);
      if (input) {
        input.classList.add("is-invalid");
      }
    }
  };

  const setMessage = (message, isSuccess) => {
    if (!messageElement) {
      return;
    }

    messageElement.textContent = message;
    messageElement.className = `small mb-3 ${isSuccess ? "text-success" : "text-warning"}`;
  };

  const setSubmitting = (isSubmitting) => {
    if (!registerButton) {
      return;
    }

    registerButton.disabled = isSubmitting;
    registerButton.textContent = isSubmitting ? "Đang tạo tài khoản..." : "Tạo tài khoản";
  };

  const setupPasswordToggle = () => {
    const toggle = document.getElementById("toggleRegisterPasswords");
    const passwordInput = document.getElementById("password");
    const confirmPasswordInput = document.getElementById("confirmPassword");

    if (!toggle || !passwordInput || !confirmPasswordInput) {
      return;
    }

    toggle.addEventListener("change", () => {
      const inputType = toggle.checked ? "text" : "password";
      passwordInput.type = inputType;
      confirmPasswordInput.type = inputType;
    });
  };

  const getPayload = () => {
    return {
      username: (document.getElementById("username")?.value || "").trim(),
      fullName: (document.getElementById("fullName")?.value || "").trim(),
      email: (document.getElementById("email")?.value || "").trim(),
      password: document.getElementById("password")?.value || "",
      confirmPassword: document.getElementById("confirmPassword")?.value || "",
      acceptTerms: document.getElementById("agree")?.checked || false,
    };
  };

  const mapServerFieldToClientField = (fieldName) => {
    const normalized = String(fieldName || "")
      .trim()
      .replace(/^\$\./, "")
      .split(".")
      .pop()
      .replace(/\[|\]/g, "")
      .toLowerCase();

    switch (normalized) {
      case "username":
        return "username";
      case "fullname":
        return "fullName";
      case "email":
        return "email";
      case "password":
        return "password";
      case "confirmpassword":
        return "confirmPassword";
      case "acceptterms":
        return "acceptTerms";
      default:
        return "";
    }
  };

  const savePendingVerification = (registerData, fallbackEmail) => {
    const email = String(registerData?.email || fallbackEmail || "")
      .trim()
      .toLowerCase();

    if (!email) {
      return "";
    }

    const payload = {
      email,
      userId: registerData?.userId ?? null,
      username: registerData?.username ?? "",
      fullName: registerData?.fullName ?? "",
      createdAt: registerData?.createdAt ?? null,
      otpDispatched: registerData?.otpDispatched ?? null,
      otpExpiresAt: registerData?.otpExpiresAt ?? null,
      message: registerData?.message ?? "",
      savedAt: new Date().toISOString(),
    };

    try {
      sessionStorage.setItem(pendingVerificationKey, JSON.stringify(payload));
    } catch (error) {
      console.warn("cannot_save_pending_verification", error);
    }

    return email;
  };

  form.addEventListener("submit", async (event) => {
    event.preventDefault();
    clearErrors();
    setMessage("", false);

    const payload = getPayload();

    if (!payload.acceptTerms) {
      setFieldError("acceptTerms", "Bạn cần đồng ý với điều khoản sử dụng.");
      return;
    }

    if (payload.password !== payload.confirmPassword) {
      setFieldError("confirmPassword", "Mật khẩu nhập lại không khớp.");
      return;
    }

    setSubmitting(true);

    try {
      const response = await fetch("/api/auth/register", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
        },
        body: JSON.stringify(payload),
      });

      const contentType = response.headers.get("content-type") || "";
      const data = contentType.toLowerCase().includes("json")
        ? await response.json().catch(() => null)
        : null;

      if (!response.ok) {
        if (response.status === 400) {
          if (data?.errors && typeof data.errors === "object") {
            for (const [fieldName, messages] of Object.entries(data.errors)) {
              const clientField = mapServerFieldToClientField(fieldName);
              if (clientField) {
                setFieldError(clientField, Array.isArray(messages) ? messages[0] : String(messages));
              }
            }

            setMessage(data.title || "Thông tin đăng ký chưa hợp lệ.", false);
            return;
          }

          setMessage(
            data?.message || data?.title || data?.detail || "Đăng ký thất bại. Vui lòng thử lại.",
            false
          );
          return;
        }

        if (response.status === 409) {
          setMessage(data?.message || "Tên đăng nhập hoặc email đã tồn tại.", false);
          return;
        }

        setMessage("Đăng ký thất bại. Vui lòng thử lại.", false);
        return;
      }

      const emailForVerification = savePendingVerification(data, payload.email);
      form.reset();
      setMessage(
        data?.message || "Đăng ký thành công. Đang chuyển sang trang xác thực OTP...",
        true
      );

      window.setTimeout(() => {
        if (emailForVerification) {
          window.location.href = `otp.html?email=${encodeURIComponent(emailForVerification)}`;
          return;
        }

        window.location.href = "otp.html";
      }, 900);
    } catch (error) {
      console.error("register_failed", error);
      setMessage("Không thể kết nối tới máy chủ.", false);
    } finally {
      setSubmitting(false);
    }
  });

  setupPasswordToggle();
})();
