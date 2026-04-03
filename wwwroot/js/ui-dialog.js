(function () {
  if (window.UiDialog) {
    return;
  }

  const MODAL_ID = "appConfirmModal";

  const ensureModal = () => {
    const existing = document.getElementById(MODAL_ID);
    if (existing) {
      return existing;
    }

    const wrapper = document.createElement("div");
    wrapper.innerHTML = `
      <div class="modal fade" id="${MODAL_ID}" tabindex="-1" aria-hidden="true">
        <div class="modal-dialog modal-dialog-centered">
          <div class="modal-content app-confirm-modal-content">
            <div class="modal-header border-0 pb-1">
              <h5 id="appConfirmModalTitle" class="modal-title fw-bold">Xác nhận thao tác</h5>
              <button type="button" class="btn-close btn-close-white" data-bs-dismiss="modal" aria-label="Close"></button>
            </div>
            <div class="modal-body pt-2">
              <p id="appConfirmModalMessage" class="mb-0 text-light-emphasis">Bạn có chắc muốn tiếp tục?</p>
            </div>
            <div class="modal-footer border-0 pt-1">
              <button id="appConfirmModalCancel" type="button" class="btn btn-outline-light" data-bs-dismiss="modal">Huỷ</button>
              <button id="appConfirmModalConfirm" type="button" class="btn btn-danger">Xoá</button>
            </div>
          </div>
        </div>
      </div>
    `;

    const modal = wrapper.firstElementChild;
    if (!modal) {
      throw new Error("Không thể khởi tạo hộp xác nhận.");
    }

    document.body.appendChild(modal);
    return modal;
  };

  const confirmDanger = (options) => {
    const title = String(options?.title || "Xác nhận xóa nội dung").trim();
    const message = String(options?.message || "Bạn có chắc muốn xóa nội dung này không?").trim();
    const confirmText = String(options?.confirmText || "Xóa").trim();
    const cancelText = String(options?.cancelText || "Huỷ").trim();

    if (!window.bootstrap?.Modal) {
      return Promise.resolve(window.confirm(message));
    }

    const modalElement = ensureModal();
    const modalTitle = document.getElementById("appConfirmModalTitle");
    const modalMessage = document.getElementById("appConfirmModalMessage");
    const cancelButton = document.getElementById("appConfirmModalCancel");
    const confirmButton = document.getElementById("appConfirmModalConfirm");
    const modalInstance = window.bootstrap.Modal.getOrCreateInstance(modalElement);

    if (!modalTitle || !modalMessage || !cancelButton || !confirmButton) {
      return Promise.resolve(window.confirm(message));
    }

    modalTitle.textContent = title;
    modalMessage.textContent = message;
    cancelButton.textContent = cancelText;
    confirmButton.textContent = confirmText;

    return new Promise((resolve) => {
      let settled = false;

      const cleanup = () => {
        confirmButton.removeEventListener("click", onConfirm);
        modalElement.removeEventListener("hidden.bs.modal", onHidden);
      };

      const onConfirm = () => {
        settled = true;
        cleanup();
        modalInstance.hide();
        resolve(true);
      };

      const onHidden = () => {
        cleanup();
        if (!settled) {
          resolve(false);
        }
      };

      confirmButton.addEventListener("click", onConfirm);
      modalElement.addEventListener("hidden.bs.modal", onHidden);
      modalInstance.show();
    });
  };

  window.UiDialog = {
    confirmDanger,
  };
})();
