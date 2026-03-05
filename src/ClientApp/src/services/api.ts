import axios from 'axios';

const api = axios.create({
  baseURL: '/api',
  headers: {
    'Content-Type': 'application/json',
  },
  timeout: 15000,
});

api.interceptors.request.use(
  config => {
    const token = localStorage.getItem('auth_token');
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
    return config;
  },
  error => {
    return Promise.reject(error);
  }
);

api.interceptors.response.use(
  response => response,
  async error => {
    const originalRequest = error.config;

    // Skip token refresh for auth endpoints -- let callers handle their own errors
    const isAuthEndpoint = originalRequest?.url?.startsWith('/auth/');
    if (error.response?.status === 401 && !originalRequest._retry && !isAuthEndpoint) {
      originalRequest._retry = true;

      try {
        const authStorage = localStorage.getItem('auth-storage');
        if (authStorage) {
          const parsed = JSON.parse(authStorage);
          const { user, refreshToken } = parsed.state;

          if (user && refreshToken) {
            const res = await axios.post('/api/auth/refresh', {
              userId: user.id,
              tenantId: user.tenantId,
              refreshToken,
            });

            if (res.data?.data) {
              const newToken = res.data.data.accessToken;
              const newRefreshToken = res.data.data.refreshToken;

              localStorage.setItem('auth_token', newToken);
              parsed.state.token = newToken;
              parsed.state.refreshToken = newRefreshToken;
              localStorage.setItem('auth-storage', JSON.stringify(parsed));

              originalRequest.headers.Authorization = `Bearer ${newToken}`;
              return api(originalRequest);
            }
          }
        }
      } catch {
        // Refresh failed, fall through to redirect
      }

      localStorage.removeItem('auth_token');
      localStorage.removeItem('auth-storage');
      window.location.href = '/login';
    }

    return Promise.reject(error);
  }
);

export default api;
