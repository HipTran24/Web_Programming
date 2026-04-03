      (async function () {
        const me = await window.AuthClient.requireAuth();
        if (!me) return;
        window.AuthClient.bindUserUi(me);
      })();

      document.querySelectorAll(".pill").forEach((p) => {
        p.addEventListener("click", () => {
          document
            .querySelectorAll(".pill")
            .forEach((x) => x.classList.remove("on"));
          p.classList.add("on");
        });
      });
      document.querySelectorAll(".type-opt").forEach((o) => {
        o.addEventListener("click", () => {
          document
            .querySelectorAll(".type-opt")
            .forEach((x) => x.classList.remove("on"));
          o.classList.add("on");
        });
      });
      document.querySelectorAll(".tab").forEach((t) => {
        t.addEventListener("click", () => {
          document
            .querySelectorAll(".tab")
            .forEach((x) => x.classList.remove("on"));
          t.classList.add("on");
        });
      });
      const dz = document.getElementById("dz");
      dz.addEventListener("dragover", (e) => {
        e.preventDefault();
        dz.style.borderColor = "rgba(79,140,255,0.5)";
      });
      dz.addEventListener("dragleave", () => {
        dz.style.borderColor = "";
      });
      dz.addEventListener("drop", (e) => {
        e.preventDefault();
        dz.style.borderColor = "";
      });
