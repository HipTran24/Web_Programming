document.documentElement.classList.add("js");

let pageRevealed = false;

function revealPageContent() {
  if (pageRevealed) {
    return;
  }

  pageRevealed = true;

  const loader = document.getElementById("page-loader");
  const content = document.querySelector(".page-content");

  if (content) {
    requestAnimationFrame(() => {
      content.classList.add("show");
    });
  }

  if (loader) {
    loader.classList.add("hide");
  }
}

document.addEventListener("DOMContentLoaded", function () {
  const deleteButtons = document.querySelectorAll(".btn-danger");
  deleteButtons.forEach((btn) => {
    btn.addEventListener("click", function (e) {
      const ok = confirm("Bạn có chắc muốn xóa mục này không?");
      if (!ok) e.preventDefault();
    });
  });

  const saveButtons = document.querySelectorAll("[data-demo-save]");
  saveButtons.forEach((btn) => {
    btn.addEventListener("click", function () {
      alert("Đã lưu cấu hình thành công (demo).");
    });
  });

  const fakeSubmitButtons = document.querySelectorAll("[data-demo-submit]");
  fakeSubmitButtons.forEach((btn) => {
    btn.addEventListener("click", function () {
      alert("Thao tác thành công (demo).");
    });
  });

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

  // Fallback in case window "load" is delayed by external resources.
  window.setTimeout(revealPageContent, 1400);
});

window.addEventListener("load", function () {
  window.setTimeout(revealPageContent, 1200);
});

// Hard fallback: never allow loader to block interaction forever.
window.setTimeout(revealPageContent, 3000);