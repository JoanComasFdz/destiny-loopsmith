// Loopsmith web host — the only browser side effects the app needs: download, clipboard, confirm, URL hash, scroll
// and focus, a step card as its drag image. Called from Hosting/BrowserInterop.cs. No app logic lives here, and
// nothing is stored in the browser.
window.loopsmith = {
  downloadText(fileName, text, mimeType) {
    const blob = new Blob([text], { type: mimeType || "text/plain" });
    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = fileName;
    anchor.rel = "noopener";
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  },

  async copyText(text) {
    try {
      if (navigator.clipboard && window.isSecureContext) {
        await navigator.clipboard.writeText(text);
        return true;
      }
    } catch (_) {
      // fall through to the legacy path
    }
    try {
      const area = document.createElement("textarea");
      area.value = text;
      area.setAttribute("readonly", "");
      area.style.position = "fixed";
      area.style.opacity = "0";
      document.body.appendChild(area);
      area.select();
      const copied = document.execCommand("copy");
      area.remove();
      return copied;
    } catch (_) {
      return false;
    }
  },

  confirmAction(message) {
    return window.confirm(message);
  },

  revealElement(id) {
    const element = document.getElementById(id);
    if (element) {
      element.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }
  },

  focusElement(id) {
    const element = document.getElementById(id);
    if (element) {
      element.focus({ preventScroll: true });
      element.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }
  },

  clearHash() {
    if (window.location.hash) {
      history.replaceState(history.state, "", window.location.pathname + window.location.search);
    }
  },
};

// A step dragged by its handle (Components/LoopTimeline.razor) shows its whole card while it moves. Firefox starts a
// drag only when it carries data: a type of Loopsmith's own, so a text field it is dropped on takes nothing.
document.addEventListener("dragstart", (event) => {
  const handle = event.target instanceof Element ? event.target.closest(".step-drag") : null;
  if (!handle || !event.dataTransfer) return;
  event.dataTransfer.effectAllowed = "move";
  event.dataTransfer.setData("application/x-loopsmith-step", handle.dataset.step || "");
  const card = handle.closest(".step-card");
  if (card) event.dataTransfer.setDragImage(card, 16, 16);
});

// A weapon's preview card opens on hover and focus by CSS; a long tap (touch) opens it here and keeps it open until a
// tap elsewhere. The long press's own click and the browser's image/link menu are swallowed.
(() => {
  const anchorOf = (target) => target.closest && target.closest(".weapon-preview-anchor");
  let timer = 0;
  let from = null;
  let swallowClick = false;
  const closeAll = (except) => document.querySelectorAll(".weapon-preview-anchor.preview-open").forEach((anchor) => {
    if (anchor !== except) anchor.classList.remove("preview-open");
  });
  const cancel = () => { clearTimeout(timer); timer = 0; };
  document.addEventListener("pointerdown", (event) => {
    const anchor = anchorOf(event.target);
    closeAll(anchor && event.target.closest(".weapon-card") ? anchor : null);
    cancel();
    if (event.pointerType !== "touch" || !anchor || event.target.closest(".weapon-card")) return;
    from = { x: event.clientX, y: event.clientY };
    timer = setTimeout(() => {
      timer = 0;
      anchor.classList.add("preview-open");
      swallowClick = true;
    }, 500);
  });
  document.addEventListener("pointermove", (event) => {
    if (timer && from && Math.hypot(event.clientX - from.x, event.clientY - from.y) > 10) cancel();
  });
  document.addEventListener("pointerup", cancel);
  document.addEventListener("pointercancel", cancel);
  document.addEventListener("contextmenu", (event) => {
    if (anchorOf(event.target)) event.preventDefault();
  });
  document.addEventListener("click", (event) => {
    if (swallowClick) {
      swallowClick = false;
      if (anchorOf(event.target)) { event.preventDefault(); event.stopImmediatePropagation(); }
    }
  }, true);
})();
