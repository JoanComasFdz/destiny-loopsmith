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
