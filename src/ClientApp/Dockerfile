# syntax=docker/dockerfile:1

# ---- build stage ----
FROM node:20-alpine AS build
WORKDIR /app

# Install dependencies first for better layer caching
COPY src/ClientApp/package.json src/ClientApp/package-lock.json ./
RUN npm ci

# Copy source and build
COPY src/ClientApp/ ./
RUN npm run build

# ---- runtime stage ----
FROM nginx:alpine AS runtime

# Remove the default nginx config
RUN rm /etc/nginx/conf.d/default.conf

COPY src/ClientApp/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html

EXPOSE 80

HEALTHCHECK --interval=30s --timeout=10s --start-period=10s --retries=3 \
    CMD wget -qO- http://localhost:80/ || exit 1
