# Frontend (spec §11.1): build multi-stage, Node (build do Vite) → nginx (estáticos + proxy /api → api:8080).
# Contexto de build: a pasta frontend/ (docker-compose.yml: build.context ./frontend). Imagens oficiais com versão
# fixada (spec §11.7).

FROM node:24.22.3-alpine3.24 AS build
WORKDIR /app

# Dependências em camada própria, exatamente as do package-lock.json.
COPY package.json package-lock.json ./
RUN npm ci --no-audit --no-fund

# tsc -b (typecheck) + vite build.
COPY . .
RUN npm run build

FROM nginx:1.30.6-alpine3.24 AS runtime

# Só o build estático e a configuração do servidor: sem código-fonte, node_modules ou Node. As páginas padrão da
# imagem (index.html e 50x.html de exemplo) saem antes.
RUN rm -rf /usr/share/nginx/html/*
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80
