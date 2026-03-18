      (async function () {
        if (!window.AuthClient) {
          return;
        }

        const me = await window.AuthClient.requireAuth();
        if (!me) {
          return;
        }

        window.AuthClient.bindUserUi(me);
      })();

