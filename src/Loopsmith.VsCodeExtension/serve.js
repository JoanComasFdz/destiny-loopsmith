// Serves the extension on http://localhost:5000, for vscode.dev's "Developer: Install Extension from Location…"
// (README.md, "Install"): vscode.dev only fetches from localhost and a few hosts of its own, so it can't install it from
// the site. Run by the Dockerfile next to it; nothing to install. Every answer allows any origin, which vscode.dev
// needs, and is never cached, so a rebuilt container's extension is the one vscode.dev loads.
'use strict';

const http = require('node:http');
const fs = require('node:fs');
const path = require('node:path');

const FOLDER = '/ext';
const PORT = 5000;
const TYPES = { '.json': 'application/json', '.js': 'application/javascript', '.md': 'text/markdown' };

// Only the folder's own files, by name: no path in a request reaches outside it.
const answer = (request, response) => {
  const name = path.basename(decodeURIComponent(new URL(request.url, 'http://localhost').pathname));
  fs.readFile(path.join(FOLDER, name), (error, body) => {
    response.writeHead(error ? 404 : 200, {
      'Access-Control-Allow-Origin': '*',
      'Content-Type': TYPES[path.extname(name)] ?? 'text/plain',
      'Cache-Control': 'no-store',
    });
    response.end(error ? '' : body);
  });
};

http.createServer(answer).listen(PORT, () => console.log(`Serving the Loopsmith e2e recordings extension at http://localhost:${PORT}`));
