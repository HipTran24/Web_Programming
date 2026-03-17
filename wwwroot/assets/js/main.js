document.documentElement.classList.add("js");

let pageRevealed = false;

function revealPageContent() {
  if (pageRevealed) return;

  pageRevealed = true;

  const loader = document.getElementById("page-loader");
  const content = document.querySelector(".page-content");

  // Hiện nội dung
  if (content) {
    requestAnimationFrame(() => {
      content.classList.add("show");
    });
  }

  // Ẩn loader
  if (loader) {
    loader.classList.add("hide");

    // remove hẳn khỏi DOM để tránh chặn click
    setTimeout(() => {
      loader.remove();
    }, 600);
  }
}

document.addEventListener("DOMContentLoaded", function () {
  /* ================= DELETE CONFIRM ================= */
  document.querySelectorAll(".btn-danger").forEach((btn) => {
    btn.addEventListener("click", function (e) {
      if (!confirm("Bạn có chắc muốn xóa mục này không?")) {
        e.preventDefault();
      }
    });
  });

  /* ================= DEMO BUTTON ================= */
  document.querySelectorAll("[data-demo-save]").forEach((btn) => {
    btn.addEventListener("click", () => {
      alert("Đã lưu cấu hình thành công (demo).");
    });
  });

  document.querySelectorAll("[data-demo-submit]").forEach((btn) => {
    btn.addEventListener("click", () => {
      alert("Thao tác thành công (demo).");
    });
  });

  /* ================= TOGGLE PASSWORD ================= */
  const passwordInput = document.getElementById("password");
  const togglePassword = document.getElementById("togglePassword");
  const eyeOpen = document.getElementById("eyeOpen");
  const eyeClosed = document.getElementById("eyeClosed");

  if (passwordInput && togglePassword) {
    togglePassword.addEventListener("click", function () {
      const isPassword = passwordInput.type === "password";
      passwordInput.type = isPassword ? "text" : "password";

      if (eyeOpen && eyeClosed) {
        eyeOpen.style.display = isPassword ? "none" : "block";
        eyeClosed.style.display = isPassword ? "block" : "none";
      }
    });
  }

  /* ================= SCROLL REVEAL ================= */
  const reveals = document.querySelectorAll(".reveal");

  if (reveals.length > 0) {
    const io = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("visible");
          }
        });
      },
      { threshold: 0.15 }
    );

    reveals.forEach((el) => io.observe(el));
  }

  /* ================= FALLBACK ================= */
  setTimeout(revealPageContent, 1200);
});

window.addEventListener("load", function () {
  setTimeout(revealPageContent, 800);
});

// HARD fallback (chống trắng trang)
setTimeout(revealPageContent, 2500);