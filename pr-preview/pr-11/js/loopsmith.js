// Loopsmith web host — the only browser side effects the app needs: download, clipboard, confirm, URL hash.
// Called from Hosting/BrowserInterop.cs. No app logic lives here, and nothing is stored in the browser.
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
    if (element && window.matchMedia("(min-width: 1100px)").matches) {
      element.scrollIntoView({ block: "nearest", behavior: "smooth" });
    }
  },

  clearHash() {
    if (window.location.hash) {
      history.replaceState(history.state, "", window.location.pathname + window.location.search);
    }
  },
};
