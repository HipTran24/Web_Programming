(function () {
  let scrollListener = null;
  let revealObserver = null;

  if (window.AuthClient?.whenReady) {
    window.AuthClient.whenReady().then(() => {
      const current = window.AuthClient.getCurrentUser?.();
      if (current) {
        window.AuthClient.bindUserUi(current);
      }
    });
  }

  const navbar = document.getElementById("navbar");
  if (navbar) {
    scrollListener = () => {
      navbar.classList.toggle("scrolled", window.scrollY > 40);
    };

    window.addEventListener("scroll", scrollListener);
  }

  const reveals = document.querySelectorAll(".reveal");
  if (reveals.length > 0) {
    revealObserver = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add("visible");
          }
        });
      },
      { threshold: 0.12 }
    );

    reveals.forEach((element) => revealObserver.observe(element));
  }

  const disposePage = () => {
    if (scrollListener) {
      window.removeEventListener("scroll", scrollListener);
      scrollListener = null;
    }

    if (revealObserver) {
      revealObserver.disconnect();
      revealObserver = null;
    }
  };

  document.addEventListener("ajax:before-swap", disposePage, { once: true });
})();

