# Serves the extension on http://localhost:5000 for vscode.dev's "Developer: Install Extension from Location…"
# (README.md, "Install"). Built straight from GitHub, no clone:
#   docker build -t loopsmith-vscode-extension "https://github.com/JoanComasFdz/destiny-loopsmith.git#main:src/Loopsmith.VsCodeExtension"
#   docker run -d --name loopsmith-vscode-extension --restart unless-stopped -p 127.0.0.1:5000:5000 loopsmith-vscode-extension
FROM node:22-alpine
WORKDIR /ext
COPY package.json extension.js README.md ./
COPY serve.js /serve.js
EXPOSE 5000
CMD ["node", "/serve.js"]
