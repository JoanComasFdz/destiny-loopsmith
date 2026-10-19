// Loopsmith web host — Bungie's login in a window of its own. Bungie sends the player back to the app's registered
// redirect page (login.html, at …/destiny-loopsmith/login), which hands Bungie's answer (the code and the state, or
// the error) to the tab that asked over a same-origin BroadcastChannel, and closes. A PR preview's tab is on the same
// origin, so it hears the live site's page too. Core checks the state and reads the answer: no logic lives here.
// Called from Hosting/BrowserInterop.cs and from login.html.
window.loopsmithBungie = (() => {
  const CHANNEL = "loopsmith-bungie-login";
  const WAIT_MS = 10 * 60 * 1000;

  return {
    // Opens Bungie's login and waits for the login page's answer (JSON), or { blocked } when the browser didn't open
    // the window, { timedOut } after ten minutes, { cancelled } when the tab cancels.
    login(url) {
      return new Promise((resolve) => {
        const channel = new BroadcastChannel(CHANNEL);
        let timer = 0;
        const finish = (answer) => {
          clearTimeout(timer);
          channel.close();
          resolve(JSON.stringify(answer || {}));
        };
        channel.onmessage = (event) => finish(event.data);
        const opened = window.open(url, "loopsmith-bungie-login", "popup,width=520,height=760");
        if (!opened) {
          finish({ blocked: true });
          return;
        }
        timer = setTimeout(() => finish({ timedOut: true }), WAIT_MS);
      });
    },

    // Stops waiting for a login (the player closed Bungie's window).
    cancel() {
      const channel = new BroadcastChannel(CHANNEL);
      channel.postMessage({ cancelled: true });
      channel.close();
    },

    // The login page: Bungie's answer from the query to the tab that asked, then close this window.
    relay(note) {
      const query = new URLSearchParams(window.location.search);
      const answer = {};
      for (const key of ["code", "state", "error", "error_description"]) {
        const value = query.get(key);
        if (value !== null) answer[key] = value;
      }
      const channel = new BroadcastChannel(CHANNEL);
      channel.postMessage(answer);
      channel.close();
      if (note) {
        note.textContent = answer.code
          ? "Logged in with Bungie. You can close this window and go back to Loopsmith."
          : "Bungie's login didn't finish. You can close this window and go back to Loopsmith.";
      }
      setTimeout(() => window.close(), 400);
    },
  };
})();
