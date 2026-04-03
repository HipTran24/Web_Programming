(function () {
  const AVATAR_STORAGE_KEY = "auth.avatar";
  const SYSTEM_NOTIFICATION_POLL_MS = 9000;
  let systemNotificationPollTimer = 0;
  let profileVisibilityHandler = null;

  const el = {
    displayName: document.getElementById("displayName"),
    displayEmail: document.getElementById("displayEmail"),
    displayUsername: document.getElementById("displayUsername"),
    displayRole: document.getElementById("displayRole"),
    accountStatus: document.getElementById("accountStatus"),
    accountCreatedAt: document.getElementById("accountCreatedAt"),
    statUploads: document.getElementById("statUploads"),
    statTests: document.getElementById("statTests"),
    statAvg: document.getElementById("statAvg"),
    statDays: document.getElementById("statDays"),
    avatarImg: document.getElementById("avatarImg"),
    avatarInput: document.getElementById("avatarInput"),
    firstName: document.getElementById("firstName"),
    lastName: document.getElementById("lastName"),
    email: document.getElementById("email"),
    phone: document.getElementById("phone"),
    bio: document.getElementById("bio"),
    formInfo: document.getElementById("formInfo"),
    formPassword: document.getElementById("formPassword"),
    btnResetInfo: document.getElementById("btnResetInfo"),
    btnSaveNotify: document.getElementById("btnSaveNotify"),
    btnRefreshSystemNotify: document.getElementById("btnRefreshSystemNotify"),
    btnMarkAllSystemNotify: document.getElementById("btnMarkAllSystemNotify"),
    btnDeleteAccount: document.getElementById("btnDeleteAccount"),
    btnCancelDelete: document.getElementById("btnCancelDelete"),
    btnConfirmDelete: document.getElementById("btnConfirmDelete"),
    notifyReview: document.getElementById("notifyReview"),
    notifyResult: document.getElementById("notifyResult"),
    notifyNews: document.getElementById("notifyNews"),
    systemNotifyMeta: document.getElementById("systemNotifyMeta"),
    systemNotifyList: document.getElementById("systemNotifyList"),
    currentPwd: document.getElementById("currentPwd"),
    newPwd: document.getElementById("newPwd"),
    confirmPwd: document.getElementById("confirmPwd"),
    pwdStrLbl: document.getElementById("pwdStrLbl"),
    infoAlert: document.getElementById("infoAlert"),
    infoAlertMsg: document.getElementById("infoAlertMsg"),
    pwdAlert: document.getElementById("pwdAlert"),
    pwdAlertMsg: document.getElementById("pwdAlertMsg"),
    deleteModal: document.getElementById("deleteModal"),
    toast: document.getElementById("toast"),
    toastMsg: document.getElementById("toastMsg"),
  };
  const DEFAULT_AVATAR_SRC = el.avatarImg?.getAttribute("src") || "";

  const state = {
    profile: null,
    notifySettings: {
      notifyReviewReminder: true,
      notifyQuizResult: false,
      notifyProductNews: true,
    },
    systemNotifications: {
      unreadCount: 0,
      items: [],
      lastPreviewId: "",
    },
    pending: {
      updateProfile: false,
      changePassword: false,
      saveNotifications: false,
      deleteAccount: false,
    },
  };

  const readSavedAvatar = () =>
    localStorage.getItem(AVATAR_STORAGE_KEY) ||
    sessionStorage.getItem(AVATAR_STORAGE_KEY) ||
    localStorage.getItem("avatar") ||
    sessionStorage.getItem("avatar") ||
    "";

  const getAvatarDisplayInitials = (profile) => {
    const source = String(profile?.fullName || profile?.email || "").trim();
    if (!source) {
      return "US";
    }

    const parts = source.split(/\s+/).filter(Boolean);
    if (parts.length >= 2) {
      return `${parts[0][0]}${parts[1][0]}`.toUpperCase();
    }

    return source.slice(0, 2).toUpperCase();
  };

  const splitFullName = (fullName) => {
    const parts = String(fullName || "").trim().split(/\s+/).filter(Boolean);
    if (parts.length === 0) {
      return { firstName: "", lastName: "" };
    }

    if (parts.length === 1) {
      return { firstName: "", lastName: parts[0] };
    }

    return {
      firstName: parts.slice(0, -1).join(" "),
      lastName: parts[parts.length - 1],
    };
  };

  const composeFullName = (firstName, lastName) =>
    [String(firstName || "").trim(), String(lastName || "").trim()]
      .filter(Boolean)
      .join(" ")
      .trim();

  const normalizeRoleLabel = (role) => {
    const key = String(role || "").trim().toLowerCase();
    if (key === "admin") return "Quản trị viên";
    if (key === "user") return "Học viên";
    return role || "Người dùng";
  };

  const getAuthHeaders = (json = false) => {
    const headers = {};
    if (json) {
      headers["Content-Type"] = "application/json";
    }

    const token = window.AuthClient?.getAccessToken?.() || "";
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    return headers;
  };

  const showToast = (message, type = "success") => {
    if (!el.toast || !el.toastMsg) {
      return;
    }

    el.toastMsg.textContent = message;
    el.toast.className = `toast toast-${type} show`;
    clearTimeout(showToast._timer);
    showToast._timer = setTimeout(() => {
      el.toast.classList.remove("show");
    }, 3200);
  };

  const setAlert = (target, message, type = "error") => {
    if (!target?.container || !target?.message) {
      return;
    }

    if (!message) {
      target.container.classList.remove("show");
      target.message.textContent = "";
      return;
    }

    target.container.classList.add("show");
    target.container.classList.toggle("alert-error", type === "error");
    target.container.classList.toggle("alert-success", type === "success");
    target.message.textContent = message;
  };

  const clearFieldErrors = () => {
    ["firstName", "lastName", "email", "currentPwd", "newPwd", "confirmPwd"].forEach((field) => {
      const input = document.getElementById(field);
      if (input) {
        input.classList.remove("err");
      }

      const error = document.getElementById(`${field}Err`);
      if (error) {
        error.textContent = "";
        error.classList.remove("show");
      }
    });
  };

  const showFieldError = (field, message) => {
    const input = document.getElementById(field);
    if (input) {
      input.classList.add("err");
    }

    const error = document.getElementById(`${field}Err`);
    if (error) {
      error.textContent = message;
      error.classList.add("show");
    }
  };

  const saveAvatar = (dataUrl) => {
    const normalized = String(dataUrl || "").trim();

    if (normalized) {
      localStorage.setItem(AVATAR_STORAGE_KEY, normalized);
      sessionStorage.setItem(AVATAR_STORAGE_KEY, normalized);
      localStorage.setItem("avatar", normalized);
      sessionStorage.setItem("avatar", normalized);
    } else {
      localStorage.removeItem(AVATAR_STORAGE_KEY);
      sessionStorage.removeItem(AVATAR_STORAGE_KEY);
      localStorage.removeItem("avatar");
      sessionStorage.removeItem("avatar");
    }

    const patchStorage = (storage) => {
      const raw = storage.getItem("auth.currentUser");
      if (!raw) {
        return;
      }

      try {
        const currentUser = JSON.parse(raw);
        currentUser.avatarUrl = normalized;
        storage.setItem("auth.currentUser", JSON.stringify(currentUser));
      } catch {
        // Ignore malformed storage payload.
      }
    };

    patchStorage(localStorage);
    patchStorage(sessionStorage);
  };

  const applyAvatar = (src) => {
    if (!el.avatarImg) {
      return;
    }

    const normalized = String(src || "").trim();
    el.avatarImg.src = normalized || DEFAULT_AVATAR_SRC;

    if (normalized) {
      document.querySelectorAll("#app-shell-navbar .app-shell-avatar-img").forEach((img) => {
        img.src = normalized;
      });

      document.querySelectorAll("#app-shell-navbar .app-shell-avatar").forEach((oldAvatar) => {
        const img = document.createElement("img");
        img.className = "app-shell-avatar-img";
        img.alt = "Avatar";
        img.src = normalized;
        oldAvatar.replaceWith(img);
      });
      return;
    }

    const initials = getAvatarDisplayInitials(state.profile);
    document.querySelectorAll("#app-shell-navbar .app-shell-avatar-img").forEach((oldAvatar) => {
      const avatar = document.createElement("span");
      avatar.className = "app-shell-avatar";
      avatar.textContent = initials;
      oldAvatar.replaceWith(avatar);
    });

    document.querySelectorAll("#app-shell-navbar .app-shell-avatar").forEach((avatar) => {
      avatar.textContent = initials;
    });
  };

  const setPending = (name, isPending) => {
    state.pending[name] = isPending;

    if (name === "updateProfile") {
      const submitButton = el.formInfo?.querySelector("button[type='submit']");
      if (submitButton) {
        submitButton.disabled = isPending;
        submitButton.textContent = isPending ? "Đang lưu..." : "Lưu thay đổi";
      }
    }

    if (name === "changePassword") {
      const submitButton = el.formPassword?.querySelector("button[type='submit']");
      if (submitButton) {
        submitButton.disabled = isPending;
        submitButton.textContent = isPending ? "Đang cập nhật..." : "Cập nhật mật khẩu";
      }
    }

    if (name === "saveNotifications" && el.btnSaveNotify) {
      el.btnSaveNotify.disabled = isPending;
      el.btnSaveNotify.textContent = isPending ? "Đang lưu..." : "Lưu cài đặt";
    }

    if (name === "deleteAccount" && el.btnConfirmDelete) {
      el.btnConfirmDelete.disabled = isPending;
      el.btnConfirmDelete.textContent = isPending ? "Đang xóa..." : "Xác nhận xóa";
    }
  };

  const formatDateTime = (value) => {
    const date = new Date(value || "");
    if (Number.isNaN(date.getTime())) {
      return "-";
    }

    return date.toLocaleString("vi-VN", {
      year: "numeric",
      month: "2-digit",
      day: "2-digit",
      hour: "2-digit",
      minute: "2-digit",
    });
  };

  const escapeHtml = (value) =>
    String(value ?? "")
      .replaceAll("&", "&amp;")
      .replaceAll("<", "&lt;")
      .replaceAll(">", "&gt;")
      .replaceAll('"', "&quot;")
      .replaceAll("'", "&#39;");

  const emitUnreadChange = (unreadCount) => {
    window.dispatchEvent(new CustomEvent("system-notifications:changed", {
      detail: { unreadCount: Number(unreadCount || 0) },
    }));
  };

  const getActiveTabName = () =>
    document.querySelector(".tab-btn.active")?.dataset.tab || "profile";

  const renderProfile = (profile) => {
    state.profile = {
      ...profile,
      avatarUrl: String(profile?.avatarUrl || "").trim(),
    };

    const fullName = String(state.profile?.fullName || "").trim();
    const firstLast = splitFullName(fullName);

    if (el.firstName) el.firstName.value = firstLast.firstName;
    if (el.lastName) el.lastName.value = firstLast.lastName;
    if (el.email) el.email.value = String(state.profile?.email || "").trim();
    if (el.phone) el.phone.value = String(state.profile?.phone || "").trim();
    if (el.bio) el.bio.value = String(state.profile?.bio || "").trim();

    if (el.displayName) {
      el.displayName.textContent = fullName || "Người dùng";
    }

    if (el.displayEmail) {
      el.displayEmail.textContent = String(state.profile?.email || "").trim();
    }

    if (el.displayUsername) {
      el.displayUsername.textContent = `@${String(state.profile?.username || "user").trim()}`;
    }

    if (el.displayRole) {
      const roleLabel = normalizeRoleLabel(state.profile?.role);
      const verifiedTag = state.profile?.isEmailVerified ? " • Đã xác thực" : " • Chưa xác thực";
      el.displayRole.textContent = `${roleLabel}${verifiedTag}`;
    }

    if (el.accountStatus) {
      const lockText = state.profile?.isLocked ? "Đã khóa" : "Đang hoạt động";
      const verifyText = state.profile?.isEmailVerified ? "Email đã xác thực" : "Email chưa xác thực";
      el.accountStatus.textContent = `Trạng thái tài khoản: ${lockText} • ${verifyText}`;
    }

    if (el.accountCreatedAt) {
      el.accountCreatedAt.textContent = `Ngày tạo: ${formatDateTime(state.profile?.createdAt)}`;
    }

    if (el.statUploads) {
      el.statUploads.textContent = String(state.profile?.totalUploads || 0);
    }

    if (el.statTests) {
      el.statTests.textContent = String(state.profile?.totalQuizAttempts || 0);
    }

    if (el.statAvg) {
      const avg = Number(state.profile?.averageQuizScore || 0);
      el.statAvg.textContent = avg > 0 ? avg.toFixed(2) : "—";
    }

    if (el.statDays) {
      el.statDays.textContent = String(state.profile?.activeLearningDays || 0);
    }

    saveAvatar(state.profile.avatarUrl);
    applyAvatar(state.profile.avatarUrl);
  };

  const loadProfile = async () => {
    const response = await fetch("/api/profile", {
      method: "GET",
      headers: getAuthHeaders(),
      cache: "no-store",
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok || !payload) {
      throw new Error(payload?.message || "Không tải được hồ sơ tài khoản.");
    }

    renderProfile(payload);

    return payload;
  };

  const loadNotifications = async () => {
    const response = await fetch("/api/profile/notifications", {
      method: "GET",
      headers: getAuthHeaders(),
      cache: "no-store",
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok || !payload) {
      throw new Error(payload?.message || "Không tải được cài đặt thông báo.");
    }

    state.notifySettings = {
      notifyReviewReminder: Boolean(payload.notifyReviewReminder),
      notifyQuizResult: Boolean(payload.notifyQuizResult),
      notifyProductNews: Boolean(payload.notifyProductNews),
    };

    if (el.notifyReview) el.notifyReview.checked = state.notifySettings.notifyReviewReminder;
    if (el.notifyResult) el.notifyResult.checked = state.notifySettings.notifyQuizResult;
    if (el.notifyNews) el.notifyNews.checked = state.notifySettings.notifyProductNews;
  };

  const buildSeverityLabel = (severity) => {
    const key = String(severity || "").toLowerCase();
    if (key === "success") return "Thành công";
    if (key === "warning") return "Cảnh báo";
    if (key === "danger") return "Khẩn cấp";
    return "Thông tin";
  };

  const buildCategoryLabel = (category) => {
    const key = String(category || "").toLowerCase();
    if (key === "moderation") return "Kiểm duyệt";
    if (key === "account") return "Tài khoản";
    if (key === "security") return "Bảo mật";
    if (key === "quiz") return "Quiz";
    return "Hệ thống";
  };

  const renderSystemNotifications = (payload, options = {}) => {
    const items = Array.isArray(payload?.items) ? payload.items : [];
    const unreadCount = Number(payload?.unreadCount || 0);
    const previousUnread = Number(state.systemNotifications.unreadCount || 0);
    const previousTopId = state.systemNotifications.items?.[0]?.notificationId || "";
    const nextTopId = items[0]?.notificationId || "";
    const hasNewRealtimeItem =
      Boolean(options.realtime) &&
      Boolean(previousTopId) &&
      Boolean(nextTopId) &&
      nextTopId !== previousTopId &&
      unreadCount >= previousUnread;

    state.systemNotifications = {
      unreadCount,
      items,
      lastPreviewId: nextTopId,
    };

    if (el.systemNotifyMeta) {
      el.systemNotifyMeta.textContent = unreadCount > 0
        ? `Bạn có ${unreadCount} thông báo chưa đọc.`
        : `Tổng ${items.length} thông báo hệ thống.`;
    }

    if (!el.systemNotifyList) {
      emitUnreadChange(unreadCount);
      if (hasNewRealtimeItem) {
        showToast(`Bạn có ${Math.max(1, unreadCount - previousUnread)} thông báo hệ thống mới.`, "success");
      }
      return;
    }

    if (items.length === 0) {
      el.systemNotifyList.innerHTML = '<div class="system-notify-empty">Chưa có thông báo hệ thống nào cho tài khoản này.</div>';
      emitUnreadChange(unreadCount);
      if (hasNewRealtimeItem) {
        showToast(`Bạn có ${Math.max(1, unreadCount - previousUnread)} thông báo hệ thống mới.`, "success");
      }
      return;
    }

    el.systemNotifyList.innerHTML = items.map((item) => `
      <article class="system-notify-card ${item.isRead ? "" : "is-unread"}">
        <div class="system-notify-top">
          <div>
            <div class="system-notify-title">${escapeHtml(item.title || "Thông báo hệ thống")}</div>
            <div class="system-notify-meta">
              <span class="system-notify-pill" data-severity="${escapeHtml(item.severity || "info")}">${escapeHtml(buildSeverityLabel(item.severity))}</span>
              <span class="system-notify-pill">${escapeHtml(buildCategoryLabel(item.category))}</span>
              <span>${escapeHtml(formatDateTime(item.createdAt))}</span>
              <span>${escapeHtml(item.isRead ? "Đã đọc" : "Chưa đọc")}</span>
            </div>
          </div>
        </div>
        <div class="system-notify-message">${escapeHtml(item.message || "")}</div>
        <div class="system-notify-actions">
          ${item.actionUrl ? `<button type="button" class="btn btn-primary" data-open-notify-url="${escapeHtml(item.actionUrl)}" data-notification-id="${escapeHtml(item.notificationId)}">Mở liên kết</button>` : ""}
          ${item.isRead ? "" : `<button type="button" class="btn btn-outline-light" data-mark-notify-read="${escapeHtml(item.notificationId)}">Đánh dấu đã đọc</button>`}
        </div>
      </article>
    `).join("");

    Array.from(el.systemNotifyList.querySelectorAll("[data-mark-notify-read]")).forEach((button) => {
      button.addEventListener("click", async () => {
        await markSystemNotificationsAsRead(button.getAttribute("data-mark-notify-read") || "", false);
      });
    });

    Array.from(el.systemNotifyList.querySelectorAll("[data-open-notify-url]")).forEach((button) => {
      button.addEventListener("click", async () => {
        const notificationId = button.getAttribute("data-notification-id") || "";
        const targetUrl = button.getAttribute("data-open-notify-url") || "";
        if (notificationId) {
          await markSystemNotificationsAsRead(notificationId, false);
        }

        if (!targetUrl) {
          return;
        }

        if (window.AjaxNavigation?.navigate) {
          await window.AjaxNavigation.navigate(targetUrl);
          return;
        }

        window.location.assign(targetUrl);
      });
    });

    emitUnreadChange(unreadCount);
    if (hasNewRealtimeItem) {
      showToast(`Bạn có ${Math.max(1, unreadCount - previousUnread)} thông báo hệ thống mới.`, "success");
    }
  };

  const loadSystemNotifications = async (options = {}) => {
    const response = await fetch("/api/profile/system-notifications", {
      method: "GET",
      headers: getAuthHeaders(),
      cache: "no-store",
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok || !payload) {
      throw new Error(payload?.message || "Không tải được thông báo hệ thống.");
    }

    renderSystemNotifications(payload, options);
  };

  const markSystemNotificationsAsRead = async (notificationId, markAll) => {
    const response = await fetch("/api/profile/system-notifications/read", {
      method: "PUT",
      headers: getAuthHeaders(true),
      body: JSON.stringify({
        notificationId: notificationId || "",
        markAll: Boolean(markAll),
      }),
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(payload?.message || "Không thể cập nhật trạng thái thông báo.");
    }

    await loadSystemNotifications();
  };

  const validateProfileForm = () => {
    clearFieldErrors();
    setAlert({ container: el.infoAlert, message: el.infoAlertMsg }, "");

    const firstName = String(el.firstName?.value || "").trim();
    const lastName = String(el.lastName?.value || "").trim();
    const email = String(el.email?.value || "").trim();

    let valid = true;

    if (!firstName) {
      showFieldError("firstName", "Vui lòng nhập họ.");
      valid = false;
    }

    if (!lastName) {
      showFieldError("lastName", "Vui lòng nhập tên.");
      valid = false;
    }

    if (!email) {
      showFieldError("email", "Vui lòng nhập email.");
      valid = false;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      showFieldError("email", "Email không hợp lệ.");
      valid = false;
    }

    return valid;
  };

  const updateProfile = async () => {
    if (!state.profile || !validateProfileForm()) {
      return;
    }

    setPending("updateProfile", true);

    try {
      const fullName = composeFullName(el.firstName?.value, el.lastName?.value);
      const email = String(el.email?.value || "").trim();
      const phone = String(el.phone?.value || "").trim();
      const bio = String(el.bio?.value || "").trim();

      const response = await fetch("/api/profile", {
        method: "PUT",
        headers: getAuthHeaders(true),
        body: JSON.stringify({
          fullName,
          email,
          phone,
          bio,
          avatarUrl: String(state.profile?.avatarUrl || readSavedAvatar() || "").trim(),
        }),
      });

      const payload = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(payload?.message || "Không thể cập nhật hồ sơ.");
      }

      const profile = payload?.profile || state.profile;
      renderProfile(profile);

      if (window.AuthClient?.validateSession) {
        await window.AuthClient.validateSession();
      }

      showToast(payload?.message || "Cập nhật hồ sơ thành công.");
      setAlert({ container: el.infoAlert, message: el.infoAlertMsg }, payload?.message || "Cập nhật hồ sơ thành công.", "success");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể cập nhật hồ sơ.";
      setAlert({ container: el.infoAlert, message: el.infoAlertMsg }, message, "error");
      showToast(message, "error");
    } finally {
      setPending("updateProfile", false);
    }
  };

  const persistAvatarChange = async (avatarUrl) => {
    if (!state.profile) {
      return;
    }

    const response = await fetch("/api/profile", {
      method: "PUT",
      headers: getAuthHeaders(true),
      body: JSON.stringify({
        fullName: String(state.profile.fullName || "").trim(),
        email: String(state.profile.email || "").trim(),
        phone: String(state.profile.phone || "").trim(),
        bio: String(state.profile.bio || "").trim(),
        avatarUrl: String(avatarUrl || "").trim(),
      }),
    });

    const payload = await response.json().catch(() => null);
    if (!response.ok) {
      throw new Error(payload?.message || "Không thể lưu ảnh đại diện.");
    }

    const profile = payload?.profile || {
      ...state.profile,
      avatarUrl: String(avatarUrl || "").trim(),
    };
    renderProfile(profile);

    if (window.AuthClient?.validateSession) {
      await window.AuthClient.validateSession();
    }
  };

  const validatePasswordStrength = (value) => {
    const bars = ["b1", "b2", "b3", "b4"].map((id) => document.getElementById(id));
    bars.forEach((bar) => {
      if (bar) bar.className = "pwd-bar";
    });

    let score = 0;
    if (value.length >= 8) score += 1;
    if (/[A-Z]/.test(value)) score += 1;
    if (/[0-9]/.test(value)) score += 1;
    if (/[^A-Za-z0-9]/.test(value)) score += 1;

    const level = score <= 1 ? "weak" : score <= 2 ? "medium" : "strong";
    const label = score === 0 ? "" : level === "weak" ? "Yếu" : level === "medium" ? "Trung bình" : "Mạnh";

    for (let i = 0; i < score && i < bars.length; i += 1) {
      bars[i]?.classList.add(level);
    }

    if (el.pwdStrLbl) {
      el.pwdStrLbl.textContent = label;
    }

    return score;
  };

  const validatePasswordForm = () => {
    clearFieldErrors();
    setAlert({ container: el.pwdAlert, message: el.pwdAlertMsg }, "");

    const currentPwd = String(el.currentPwd?.value || "");
    const newPwd = String(el.newPwd?.value || "");
    const confirmPwd = String(el.confirmPwd?.value || "");

    let valid = true;

    if (!currentPwd) {
      showFieldError("currentPwd", "Nhập mật khẩu hiện tại.");
      valid = false;
    }

    if (!newPwd) {
      showFieldError("newPwd", "Nhập mật khẩu mới.");
      valid = false;
    } else if (newPwd.length < 8) {
      showFieldError("newPwd", "Mật khẩu tối thiểu 8 ký tự.");
      valid = false;
    }

    if (newPwd && confirmPwd !== newPwd) {
      showFieldError("confirmPwd", "Mật khẩu xác nhận không khớp.");
      valid = false;
    }

    return valid;
  };

  const changePassword = async () => {
    if (!validatePasswordForm()) {
      return;
    }

    setPending("changePassword", true);

    try {
      const response = await fetch("/api/profile/password", {
        method: "PUT",
        headers: getAuthHeaders(true),
        body: JSON.stringify({
          currentPassword: String(el.currentPwd?.value || ""),
          newPassword: String(el.newPwd?.value || ""),
        }),
      });

      const payload = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(payload?.message || "Không thể cập nhật mật khẩu.");
      }

      if (el.formPassword) {
        el.formPassword.reset();
      }

      validatePasswordStrength("");
      showToast(payload?.message || "Đổi mật khẩu thành công.");
      setAlert({ container: el.pwdAlert, message: el.pwdAlertMsg }, payload?.message || "Đổi mật khẩu thành công.", "success");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể đổi mật khẩu.";
      showToast(message, "error");
      setAlert({ container: el.pwdAlert, message: el.pwdAlertMsg }, message, "error");
    } finally {
      setPending("changePassword", false);
    }
  };

  const saveNotifications = async () => {
    setPending("saveNotifications", true);

    try {
      const response = await fetch("/api/profile/notifications", {
        method: "PUT",
        headers: getAuthHeaders(true),
        body: JSON.stringify({
          notifyReviewReminder: Boolean(el.notifyReview?.checked),
          notifyQuizResult: Boolean(el.notifyResult?.checked),
          notifyProductNews: Boolean(el.notifyNews?.checked),
        }),
      });

      const payload = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(payload?.message || "Không thể lưu cài đặt thông báo.");
      }

      showToast(payload?.message || "Đã lưu cài đặt thông báo.");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể lưu cài đặt thông báo.";
      showToast(message, "error");
    } finally {
      setPending("saveNotifications", false);
    }
  };

  const deleteAccount = async () => {
    if (!state.profile) {
      return;
    }

    setPending("deleteAccount", true);

    try {
      const response = await fetch("/api/profile", {
        method: "DELETE",
        headers: getAuthHeaders(),
      });

      const payload = await response.json().catch(() => null);
      if (!response.ok) {
        throw new Error(payload?.message || "Không thể xóa tài khoản.");
      }

      showToast(payload?.message || "Tài khoản đã được xóa.");
      el.deleteModal?.classList.remove("show");

      window.AuthClient?.clearSession?.();
      window.location.replace("/home/login.html?message=Tài khoản của bạn đã được xóa.");
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể xóa tài khoản.";
      showToast(message, "error");
    } finally {
      setPending("deleteAccount", false);
    }
  };

  const setActiveTab = (tabName) => {
    document.querySelectorAll(".tab-btn").forEach((item) => item.classList.toggle("active", item.dataset.tab === tabName));
    document.querySelectorAll(".tab-panel").forEach((panel) => panel.classList.toggle("active", panel.id === `tab-${tabName}`));
  };

  const bindTabs = () => {
    document.querySelectorAll(".tab-btn").forEach((button) => {
      button.addEventListener("click", () => {
        setActiveTab(button.dataset.tab || "profile");
      });
    });
  };

  const bindPasswordToggles = () => {
    document.querySelectorAll(".input-toggle-btn").forEach((button) => {
      button.addEventListener("click", () => {
        const target = document.getElementById(String(button.dataset.target || ""));
        if (!target) {
          return;
        }

        target.type = target.type === "password" ? "text" : "password";
      });
    });
  };

  const bindAvatarUpload = () => {
    if (!el.avatarInput) {
      return;
    }

    el.avatarInput.addEventListener("change", async () => {
      const file = el.avatarInput.files?.[0];
      if (!file) {
        return;
      }

      if (!file.type.startsWith("image/")) {
        showToast("Vui lòng chọn file ảnh hợp lệ.", "error");
        return;
      }

      if (file.size > 2 * 1024 * 1024) {
        showToast("Ảnh đại diện tối đa 2MB.", "error");
        return;
      }

      const reader = new FileReader();
      reader.onload = async (event) => {
        const dataUrl = String(event.target?.result || "");
        const previousAvatar = String(state.profile?.avatarUrl || readSavedAvatar() || "").trim();
        applyAvatar(dataUrl);
        saveAvatar(dataUrl);
        if (state.profile) {
          state.profile.avatarUrl = dataUrl;
        }

        try {
          await persistAvatarChange(dataUrl);
          showToast("Đã cập nhật ảnh đại diện.");
        } catch (error) {
          if (state.profile) {
            state.profile.avatarUrl = previousAvatar;
          }
          saveAvatar(previousAvatar);
          applyAvatar(previousAvatar);
          showToast(error instanceof Error ? error.message : "Không thể lưu ảnh đại diện.", "error");
        } finally {
          el.avatarInput.value = "";
        }
      };
      reader.readAsDataURL(file);
    });
  };

  const bindEvents = () => {
    bindTabs();
    bindPasswordToggles();
    bindAvatarUpload();

    el.newPwd?.addEventListener("input", () => {
      validatePasswordStrength(String(el.newPwd?.value || ""));
    });

    el.formInfo?.addEventListener("submit", (event) => {
      event.preventDefault();
      updateProfile();
    });

    el.formPassword?.addEventListener("submit", (event) => {
      event.preventDefault();
      changePassword();
    });

    el.btnSaveNotify?.addEventListener("click", () => {
      saveNotifications();
    });

    el.btnRefreshSystemNotify?.addEventListener("click", async () => {
      try {
        await loadSystemNotifications();
        showToast("Đã tải lại thông báo hệ thống.");
      } catch (error) {
        showToast(error instanceof Error ? error.message : "Không tải được thông báo hệ thống.", "error");
      }
    });

    el.btnMarkAllSystemNotify?.addEventListener("click", async () => {
      try {
        await markSystemNotificationsAsRead("", true);
        showToast("Đã đánh dấu toàn bộ thông báo là đã đọc.");
      } catch (error) {
        showToast(error instanceof Error ? error.message : "Không thể cập nhật thông báo.", "error");
      }
    });

    el.btnResetInfo?.addEventListener("click", () => {
      if (!state.profile) {
        return;
      }

      renderProfile(state.profile);
      setAlert({ container: el.infoAlert, message: el.infoAlertMsg }, "");
      clearFieldErrors();
    });

    el.btnDeleteAccount?.addEventListener("click", () => {
      el.deleteModal?.classList.add("show");
    });

    el.btnCancelDelete?.addEventListener("click", () => {
      el.deleteModal?.classList.remove("show");
    });

    el.btnConfirmDelete?.addEventListener("click", () => {
      deleteAccount();
    });
  };

  const startSystemNotificationRealtime = () => {
    if (systemNotificationPollTimer) {
      window.clearInterval(systemNotificationPollTimer);
    }

    systemNotificationPollTimer = window.setInterval(() => {
      if (document.visibilityState !== "visible") {
        return;
      }

      if (getActiveTabName() !== "notify") {
        return;
      }

      void loadSystemNotifications({ realtime: true }).catch(() => {});
    }, SYSTEM_NOTIFICATION_POLL_MS);

    if (profileVisibilityHandler) {
      document.removeEventListener("visibilitychange", profileVisibilityHandler);
    }

    profileVisibilityHandler = () => {
      if (document.visibilityState === "visible") {
        void loadSystemNotifications({ realtime: true }).catch(() => {});
      }
    };

    document.addEventListener("visibilitychange", profileVisibilityHandler);
  };

  const disposePage = () => {
    if (systemNotificationPollTimer) {
      window.clearInterval(systemNotificationPollTimer);
      systemNotificationPollTimer = 0;
    }

    if (profileVisibilityHandler) {
      document.removeEventListener("visibilitychange", profileVisibilityHandler);
      profileVisibilityHandler = null;
    }
  };

  const boot = async () => {
    try {
      const me = await window.AuthClient?.requireAuth?.();
      if (!me) {
        return;
      }

      window.AuthClient?.bindUserUi?.(me);

      applyAvatar(readSavedAvatar());
      bindEvents();

      await loadProfile();

      try {
        await loadNotifications();
        await loadSystemNotifications();
        startSystemNotificationRealtime();
      } catch (notifyError) {
        const message = notifyError instanceof Error
          ? notifyError.message
          : "Không tải được cài đặt thông báo.";
        showToast(message, "error");
      }

      if (window.location.hash === "#notify-center") {
        setActiveTab("notify");
        document.getElementById("notify-center")?.scrollIntoView({ behavior: "smooth", block: "start" });
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : "Không thể khởi tạo hồ sơ tài khoản.";
      showToast(message, "error");
      setAlert({ container: el.infoAlert, message: el.infoAlertMsg }, message, "error");
    }
  };

  document.addEventListener("ajax:before-swap", disposePage, { once: true });
  boot();
})();
