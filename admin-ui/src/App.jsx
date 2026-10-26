import { useEffect, useState } from 'react';
import { Link, Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import { api, clearSession, currentUser, setSession } from './api';

function RequireAuth({ children }) {
  return currentUser() ? children : <Navigate to="/login" replace />;
}

function Shell({ children }) {
  const loc = useLocation();
  const nav = useNavigate();
  const user = currentUser();
  const links = [
    ['/', 'Dashboard'],
    ['/campaigns', 'Campaigns'],
    ['/leads', 'Leads'],
    ['/inbox', 'Inbox'],
    ['/demos', 'Demos']
  ];

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="brand">
          MESA
          <span>WhatsApp outreach · UB Nexora</span>
        </div>
        <nav className="nav">
          {links.map(([to, label]) => (
            <Link key={to} to={to} className={loc.pathname === to ? 'active' : ''}>{label}</Link>
          ))}
        </nav>
        <div style={{ marginTop: 32 }}>
          <div className="muted" style={{ fontSize: '0.85rem' }}>{user?.displayName} · {user?.role}</div>
          <button className="btn secondary" style={{ marginTop: 10 }} onClick={() => { clearSession(); nav('/login'); }}>
            Sign out
          </button>
        </div>
      </aside>
      <main className="main">{children}</main>
    </div>
  );
}

function Login() {
  const nav = useNavigate();
  const [email, setEmail] = useState('owner@ubnexora.local');
  const [password, setPassword] = useState('ChangeMe!Owner1');
  const [error, setError] = useState('');

  async function onSubmit(e) {
    e.preventDefault();
    setError('');
    try {
      const auth = await api('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ email, password })
      });
      setSession(auth);
      nav('/');
    } catch (err) {
      setError(err.message);
    }
  }

  return (
    <div className="login-page">
      <form className="login-card" onSubmit={onSubmit}>
        <h1>MESA</h1>
        <p className="muted">Operating System for Restaurants — outreach console</p>
        <div className="field">
          <label>Email</label>
          <input value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>
        <div className="field">
          <label>Password</label>
          <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
        </div>
        {error && <div className="error">{error}</div>}
        <button className="btn" type="submit">Sign in</button>
      </form>
    </div>
  );
}

function Dashboard() {
  const [campaigns, setCampaigns] = useState([]);
  const [stats, setStats] = useState(null);

  useEffect(() => {
    api('/api/campaigns').then(async (list) => {
      setCampaigns(list);
      if (list[0]) setStats(await api(`/api/dashboard/campaigns/${list[0].id}/stats`));
    }).catch(console.error);
  }, []);

  return (
    <>
      <h1 className="page-title">Dashboard</h1>
      <p className="muted">Campaign performance for MESA outreach.</p>
      {stats && (
        <div className="grid">
          {[
            ['Sent', stats.sent],
            ['Delivered', stats.delivered],
            ['Read', stats.read],
            ['Replied', stats.replied],
            ['Opted out', stats.optedOut],
            ['Failed', stats.failed],
            ['Hot leads', stats.hotLeads],
            ['Demos', stats.demosRequested]
          ].map(([label, value], i) => (
            <div className="stat" key={label} style={{ animationDelay: `${i * 40}ms` }}>
              <strong>{value}</strong>
              <span>{label}</span>
            </div>
          ))}
        </div>
      )}
      <div className="panel">
        <h3>Campaigns</h3>
        <table>
          <thead>
            <tr><th>Name</th><th>Status</th><th>Sent</th><th>Replied</th><th>Hot</th></tr>
          </thead>
          <tbody>
            {campaigns.map((c) => (
              <tr key={c.id}>
                <td>{c.name}</td>
                <td>{c.status}</td>
                <td>{c.sentCount}</td>
                <td>{c.repliedCount}</td>
                <td>{c.hotLeadCount}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Campaigns() {
  const [campaigns, setCampaigns] = useState([]);
  const [templates, setTemplates] = useState([]);
  const [name, setName] = useState('Aurangabad Restaurant Wave');
  const [templateId, setTemplateId] = useState('');
  const [msg, setMsg] = useState('');

  async function refresh() {
    setCampaigns(await api('/api/campaigns'));
    const t = await api('/api/templates');
    setTemplates(t);
    if (!templateId && t[0]) setTemplateId(t[0].id);
  }

  useEffect(() => { refresh().catch(console.error); }, []);

  async function createCampaign() {
    setMsg('');
    await api('/api/campaigns', {
      method: 'POST',
      body: JSON.stringify({
        name,
        messageTemplateId: templateId,
        segments: ['Restaurant', 'Cafe'],
        windowStart: '11:00:00',
        windowEnd: '18:00:00',
        dailyCap: 200,
        pacingDelayMs: 3000,
        pacingJitterMs: 1500
      })
    });
    setMsg('Campaign created');
    await refresh();
  }

  async function start(id) {
    await api(`/api/campaigns/${id}/start`, { method: 'POST' });
    await refresh();
  }

  async function pause(id) {
    await api(`/api/campaigns/${id}/pause`, { method: 'POST', body: JSON.stringify({ reason: 'Manual pause' }) });
    await refresh();
  }

  return (
    <>
      <h1 className="page-title">Campaigns</h1>
      <div className="panel" style={{ marginBottom: 16 }}>
        <div className="field"><label>Name</label><input value={name} onChange={(e) => setName(e.target.value)} /></div>
        <div className="field">
          <label>Template</label>
          <select value={templateId} onChange={(e) => setTemplateId(e.target.value)}>
            {templates.map((t) => <option key={t.id} value={t.id}>{t.name} ({t.language}) — {t.status}</option>)}
          </select>
        </div>
        <button className="btn" onClick={createCampaign}>Create campaign</button>
        {msg && <p className="muted">{msg}</p>}
      </div>
      <div className="panel">
        <table>
          <thead><tr><th>Name</th><th>Status</th><th>Actions</th></tr></thead>
          <tbody>
            {campaigns.map((c) => (
              <tr key={c.id}>
                <td>{c.name}</td>
                <td>{c.status}{c.pauseReason ? ` — ${c.pauseReason}` : ''}</td>
                <td className="row">
                  <button className="btn" onClick={() => start(c.id)}>Start</button>
                  <button className="btn secondary" onClick={() => pause(c.id)}>Pause</button>
                  <a className="btn secondary" href={`/api/dashboard/campaigns/${c.id}/export.csv`}>CSV</a>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Leads() {
  const [data, setData] = useState({ items: [] });
  const [file, setFile] = useState(null);
  const [result, setResult] = useState(null);

  async function refresh() {
    setData(await api('/api/leads?pageSize=50'));
  }

  useEffect(() => { refresh().catch(console.error); }, []);

  async function importCsv() {
    if (!file) return;
    const form = new FormData();
    form.append('file', file);
    const res = await api('/api/leads/import', { method: 'POST', body: form });
    setResult(res);
    await refresh();
  }

  return (
    <>
      <h1 className="page-title">Leads</h1>
      <div className="panel" style={{ marginBottom: 16 }}>
        <div className="row">
          <input type="file" accept=".csv" onChange={(e) => setFile(e.target.files?.[0] || null)} />
          <button className="btn" onClick={importCsv}>Import CSV</button>
        </div>
        {result && <p className="muted">Imported {result.imported}, updated {result.updated}, invalid {result.invalid}</p>}
      </div>
      <div className="panel">
        <table>
          <thead><tr><th>Business</th><th>Phone</th><th>City</th><th>Segment</th><th>Status</th></tr></thead>
          <tbody>
            {data.items.map((l) => (
              <tr key={l.id}>
                <td>{l.businessName}</td>
                <td>{l.phone}</td>
                <td>{l.city}</td>
                <td>{l.segment}</td>
                <td>{l.status}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function Inbox() {
  const [items, setItems] = useState([]);
  const [selected, setSelected] = useState(null);
  const [messages, setMessages] = useState([]);
  const [body, setBody] = useState('');

  useEffect(() => {
    api('/api/inbox/handover').then(setItems).catch(console.error);
  }, []);

  async function open(leadId) {
    setSelected(leadId);
    setMessages(await api(`/api/inbox/${leadId}`));
  }

  async function reply() {
    await api(`/api/inbox/${selected}/reply`, { method: 'POST', body: JSON.stringify({ body }) });
    setBody('');
    setMessages(await api(`/api/inbox/${selected}`));
  }

  return (
    <>
      <h1 className="page-title">Inbox</h1>
      <div className="row" style={{ alignItems: 'stretch' }}>
        <div className="panel" style={{ flex: 1 }}>
          {items.map((c) => (
            <div key={c.leadId} style={{ padding: '10px 0', borderBottom: '1px solid var(--line)', cursor: 'pointer' }} onClick={() => open(c.leadId)}>
              <strong>{c.businessName}</strong>
              <div className="muted">{c.phoneMasked} · {c.lastMessagePreview}</div>
            </div>
          ))}
        </div>
        <div className="panel" style={{ flex: 2 }}>
          {selected ? (
            <>
              {messages.map((m) => (
                <div key={m.id} style={{ marginBottom: 10 }}>
                  <div className="muted">{m.direction} · {new Date(m.createdAtUtc).toLocaleString()}</div>
                  <div>{m.body}</div>
                </div>
              ))}
              <div className="field"><textarea rows={3} value={body} onChange={(e) => setBody(e.target.value)} placeholder="Manual reply (24h window)" /></div>
              <button className="btn" onClick={reply}>Send reply</button>
            </>
          ) : <p className="muted">Select a conversation</p>}
        </div>
      </div>
    </>
  );
}

function Demos() {
  const [items, setItems] = useState([]);
  async function refresh() { setItems(await api('/api/demos/pending')); }
  useEffect(() => { refresh().catch(console.error); }, []);

  return (
    <>
      <h1 className="page-title">Demo bookings</h1>
      <div className="panel">
        <table>
          <thead><tr><th>Business</th><th>Slot (IST)</th><th>Status</th><th></th></tr></thead>
          <tbody>
            {items.map((d) => (
              <tr key={d.id}>
                <td>{d.businessName}</td>
                <td>{new Date(d.slotStartIst).toLocaleString()}</td>
                <td>{d.status}</td>
                <td className="row">
                  <button className="btn" onClick={async () => { await api(`/api/demos/${d.id}/approve`, { method: 'POST' }); await refresh(); }}>Approve</button>
                  <button className="btn danger" onClick={async () => { await api(`/api/demos/${d.id}/reject`, { method: 'POST', body: JSON.stringify({ reason: 'Unavailable' }) }); await refresh(); }}>Reject</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

export default function App() {
  return (
    <Routes>
      <Route path="/login" element={<Login />} />
      <Route path="/" element={<RequireAuth><Shell><Dashboard /></Shell></RequireAuth>} />
      <Route path="/campaigns" element={<RequireAuth><Shell><Campaigns /></Shell></RequireAuth>} />
      <Route path="/leads" element={<RequireAuth><Shell><Leads /></Shell></RequireAuth>} />
      <Route path="/inbox" element={<RequireAuth><Shell><Inbox /></Shell></RequireAuth>} />
      <Route path="/demos" element={<RequireAuth><Shell><Demos /></Shell></RequireAuth>} />
    </Routes>
  );
}
