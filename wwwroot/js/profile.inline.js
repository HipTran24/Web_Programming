      const AVATAR_STORAGE_KEY = "auth.avatar";

      const readSavedAvatar = () =>
        localStorage.getItem(AVATAR_STORAGE_KEY) ||
        sessionStorage.getItem(AVATAR_STORAGE_KEY) ||
        localStorage.getItem("avatar") ||
        sessionStorage.getItem("avatar") ||
        "";

      const saveAvatar = (dataUrl) => {
        if (!dataUrl) return;
        localStorage.setItem(AVATAR_STORAGE_KEY, dataUrl);
        sessionStorage.setItem(AVATAR_STORAGE_KEY, dataUrl);

        const syncUserRaw =
          localStorage.getItem("auth.currentUser") ||
          sessionStorage.getItem("auth.currentUser");
        if (!syncUserRaw) return;

        try {
          const currentUser = JSON.parse(syncUserRaw);
          const nextUser = { ...currentUser, avatarUrl: dataUrl };
          if (localStorage.getItem("auth.currentUser")) {
            localStorage.setItem("auth.currentUser", JSON.stringify(nextUser));
          }
          if (sessionStorage.getItem("auth.currentUser")) {
            sessionStorage.setItem("auth.currentUser", JSON.stringify(nextUser));
          }
        } catch {
          // Ignore storage sync failures and keep local avatar only.
        }
      };

      const applyAvatar = (src) => {
        if (!src) return;
        const avatarImg = document.getElementById("avatarImg");
        if (avatarImg) avatarImg.src = src;

        document.querySelectorAll("#app-shell-navbar .app-shell-avatar-img").forEach((img) => {
          img.src = src;
        });

        document.querySelectorAll("#app-shell-navbar .app-shell-avatar").forEach((oldAvatar) => {
          const img = document.createElement("img");
          img.className = "app-shell-avatar-img";
          img.alt = "Avatar";
          img.src = src;
          oldAvatar.replaceWith(img);
        });
      };

      /* Tabs */ document.querySelectorAll(".tab-btn").forEach((btn) => {
        btn.addEventListener("click", () => {
          document
            .querySelectorAll(".tab-btn")
            .forEach((b) => b.classList.remove("active"));
          document
            .querySelectorAll(".tab-panel")
            .forEach((p) => p.classList.remove("active"));
          btn.classList.add("active");
          document
            .getElementById("tab-" + btn.dataset.tab)
            .classList.add("active");
        });
      });
      /* Password toggle */ document
        .querySelectorAll(".input-toggle-btn")
        .forEach((btn) => {
          btn.addEventListener("click", () => {
            const inp = document.getElementById(btn.dataset.target);
            inp.type = inp.type === "password" ? "text" : "password";
          });
        });
      /* Password strength */ document
        .getElementById("newPwd")
        .addEventListener("input", function () {
          const v = this.value;
          const bars = ["b1", "b2", "b3", "b4"].map((id) =>
            document.getElementById(id),
          );
          bars.forEach((b) => (b.className = "pwd-bar"));
          let score = 0;
          if (v.length >= 8) score++;
          if (/[A-Z]/.test(v)) score++;
          if (/[0-9]/.test(v)) score++;
          if (/[^A-Za-z0-9]/.test(v)) score++;
          const cls = score <= 1 ? "weak" : score <= 2 ? "medium" : "strong";
          const lbl = { weak: "Yếu", medium: "Trung bình", strong: "Mạnh" };
          for (let i = 0; i < score; i++) bars[i].classList.add(cls);
          document.getElementById("pwdStrLbl").textContent = v ? lbl[cls] : "";
        });
      /* Avatar preview */ document
        .getElementById("avatarInput")
        .addEventListener("change", function () {
          const file = this.files[0];
          if (!file) return;
          if (!file.type.startsWith("image/")) {
            showToast("Vui lòng chọn file ảnh hợp lệ.", "error");
            return;
          }
          if (file.size > 2 * 1024 * 1024) {
            showToast("Ảnh đại diện tối đa 2MB.", "error");
            return;
          }
          const r = new FileReader();
          r.onload = (e) => {
            const dataUrl = String(e.target.result || "");
            applyAvatar(dataUrl);
            saveAvatar(dataUrl);
            showToast("Đã cập nhật ảnh đại diện.");
          };
          r.readAsDataURL(file);
        });
      /* Toast helper */ function showToast(msg, type = "success") {
        const t = document.getElementById("toast");
        document.getElementById("toastMsg").textContent = msg;
        t.className = "toast toast-" + type + " show";
        clearTimeout(showToast._t);
        showToast._t = setTimeout(() => t.classList.remove("show"), 3200);
      }
      /* Validation helpers */ const showErr = (inputId, errId, msg) => {
        document.getElementById(inputId)?.classList.add("err");
        const e = document.getElementById(errId);
        if (e) {
          e.textContent = msg;
          e.classList.add("show");
        }
      };
      const clearErr = (inputId, errId) => {
        document.getElementById(inputId)?.classList.remove("err");
        const e = document.getElementById(errId);
        if (e) {
          e.textContent = "";
          e.classList.remove("show");
        }
      };
      /* Form Info submit */ document
        .getElementById("formInfo")
        .addEventListener("submit", (e) => {
          e.preventDefault();
          let ok = true;
          ["firstName", "lastName", "email"].forEach((f) =>
            clearErr(f, f + "Err"),
          );
          document.getElementById("infoAlert").classList.remove("show");
          if (!document.getElementById("firstName").value.trim()) {
            showErr("firstName", "firstNameErr", "Vui lòng nhập họ.");
            ok = false;
          }
          if (!document.getElementById("lastName").value.trim()) {
            showErr("lastName", "lastNameErr", "Vui lòng nhập tên.");
            ok = false;
          }
          const em = document.getElementById("email").value.trim();
          if (!em) {
            showErr("email", "emailErr", "Vui lòng nhập email.");
            ok = false;
          } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(em)) {
            showErr("email", "emailErr", "Email không hợp lệ.");
            ok = false;
          }
          if (ok) {
            const payload = {
              firstName: document.getElementById("firstName").value.trim(),
              lastName: document.getElementById("lastName").value.trim(),
              email: em,
              phone: document.getElementById("phone").value.trim(),
              school: document.getElementById("school").value.trim(),
              major: document.getElementById("major").value.trim(),
              bio: document.getElementById("bio").value.trim(),
            };
            if (typeof updateProfile === "function") updateProfile(payload);
            else showToast("Thông tin đã được lưu!");
          }
        });
      /* Form Password submit */ document
        .getElementById("formPassword")
        .addEventListener("submit", (e) => {
          e.preventDefault();
          let ok = true;
          ["currentPwd", "newPwd", "confirmPwd"].forEach((f) =>
            clearErr(f, f + "Err"),
          );
          document.getElementById("pwdAlert").classList.remove("show");
          const cur = document.getElementById("currentPwd").value;
          const np = document.getElementById("newPwd").value;
          const cp = document.getElementById("confirmPwd").value;
          if (!cur) {
            showErr("currentPwd", "currentPwdErr", "Nhập mật khẩu hiện tại.");
            ok = false;
          }
          if (!np) {
            showErr("newPwd", "newPwdErr", "Nhập mật khẩu mới.");
            ok = false;
          } else if (np.length < 8) {
            showErr("newPwd", "newPwdErr", "Tối thiểu 8 ký tự.");
            ok = false;
          }
          if (np && cp !== np) {
            showErr("confirmPwd", "confirmPwdErr", "Mật khẩu không khớp.");
            ok = false;
          }
          if (ok) {
            if (typeof changePassword === "function") changePassword(cur, np);
            else showToast("Mật khẩu đã được cập nhật!");
          }
        });
      /* Notify save */ document
        .getElementById("btnSaveNotify")
        .addEventListener("click", () => {
          if (typeof saveNotifications === "function") saveNotifications();
          else showToast("Cài đặt thông báo đã lưu!");
        });
      /* Reset info form */ document
        .getElementById("btnResetInfo")
        .addEventListener("click", () => {
          if (typeof loadProfile === "function") loadProfile();
          else document.getElementById("formInfo").reset();
        });
      /* Delete modal */ document
        .getElementById("btnDeleteAccount")
        .addEventListener("click", () =>
          document.getElementById("deleteModal").classList.add("show"),
        );
      document
        .getElementById("btnCancelDelete")
        .addEventListener("click", () =>
          document.getElementById("deleteModal").classList.remove("show"),
        );
      document
        .getElementById("btnConfirmDelete")
        .addEventListener("click", () => {
          if (typeof deleteAccount === "function") deleteAccount();
          else showToast("Tính năng này cần kết nối backend.", "error");
        });

      /* Scroll reveal */ const reveals = document.querySelectorAll(".reveal");
      const io = new IntersectionObserver(
        (entries) => {
          entries.forEach((e) => {
            if (e.isIntersecting) e.target.classList.add("visible");
          });
        },
        { threshold: 0.12 },
      );
      reveals.forEach((el) => io.observe(el));
      /* Init demo data nếu chưa có token */ document.addEventListener(
        "DOMContentLoaded",
        () => {
          applyAvatar(readSavedAvatar());

          if (
            typeof loadProfile === "function" &&
            localStorage.getItem("token")
          ) {
            loadProfile();
          } else {
            const set = (id, v) => {
              const el = document.getElementById(id);
              if (el) el.value = v;
            };
            set("firstName", "");
            set("lastName", "");
            set("email", "");
            set("phone", "");
            set("school", "");
            set("major", "");
            set("bio", "");
            document.getElementById("displayName").textContent = "";
            document.getElementById("displayEmail").textContent = "";
            document.getElementById("statUploads").textContent = "";
            document.getElementById("statTests").textContent = "";
            document.getElementById("statAvg").textContent = "";
            document.getElementById("statDays").textContent = "";
          }
        },
      );

