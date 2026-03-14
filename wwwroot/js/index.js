function initAuthNav() {
  if (!window.AuthClient || !window.AuthClient.isAuthenticated()) {
    return;
  }

  const guest = document.querySelector("[data-auth-guest]");
  const user = document.querySelector("[data-auth-user]");
  if (guest) guest.style.display = "none";
  if (user) user.style.display = "inline-flex";

  const current = window.AuthClient.getCurrentUser();
  if (current) {
    window.AuthClient.bindUserUi(current);
  }
}

initAuthNav();

const navbar = document.getElementById("navbar");
if (navbar) {
  window.addEventListener("scroll", () => {
    navbar.classList.toggle("scrolled", window.scrollY > 40);
  });
}

const reveals = document.querySelectorAll(".reveal");
const io = new IntersectionObserver(
  (entries) => {
    entries.forEach((e) => {
      if (e.isIntersecting) e.target.classList.add("visible");
    });
  },
  { threshold: 0.12 }
);

reveals.forEach((el) => io.observe(el));
