const API_BASE = import.meta.env.VITE_API_BASE || '';

export function getToken() {
  return localStorage.getItem('mesa_token');
}

export function setSession(auth) {
  localStorage.setItem('mesa_token', auth.accessToken);
  localStorage.setItem('mesa_user', JSON.stringify(auth));
}

export function clearSession() {
  localStorage.removeItem('mesa_token');
  localStorage.removeItem('mesa_user');
}

export function currentUser() {
  const raw = localStorage.getItem('mesa_user');
  return raw ? JSON.parse(raw) : null;
}

export async function api(path, options = {}) {
  const headers = { ...(options.headers || {}) };
  if (!(options.body instanceof FormData)) {
    headers['Content-Type'] = headers['Content-Type'] || 'application/json';
  }
  const token = getToken();
  if (token) headers.Authorization = `Bearer ${token}`;

  const res = await fetch(`${API_BASE}${path}`, { ...options, headers });
  if (res.status === 401) {
    clearSession();
    window.location.href = '/login';
    throw new Error('Unauthorized');
  }
  if (!res.ok) {
    const err = await res.json().catch(() => ({ message: res.statusText }));
    throw new Error(err.message || 'Request failed');
  }
  if (res.status === 204) return null;
  const ct = res.headers.get('content-type') || '';
  if (ct.includes('application/json')) return res.json();
  return res.blob();
}
