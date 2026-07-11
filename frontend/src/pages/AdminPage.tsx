import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  changePlan,
  createRole,
  deleteRole,
  getBilling,
  getOnboarding,
  getOrganization,
  inviteUser,
  listEntities,
  listPermissions,
  listRoles,
  listUsers,
  updateAdminUser,
  updateEntity,
  updateOrganization,
  updateRole,
  type AdminEntity,
  type AdminPermission,
  type AdminRole,
  type AdminUser,
  type Billing,
  type Onboarding,
  type Organization,
  type Plan,
} from '../api/client';

type Tab = 'onboarding' | 'users' | 'roles' | 'organization' | 'billing';
const TABS: { key: Tab; label: string }[] = [
  { key: 'onboarding', label: 'Onboarding' },
  { key: 'users', label: 'Users' },
  { key: 'roles', label: 'Roles' },
  { key: 'organization', label: 'Organization' },
  { key: 'billing', label: 'Billing' },
];

export function AdminPage() {
  const [tab, setTab] = useState<Tab>('onboarding');

  return (
    <main className="page" style={{ maxWidth: 1100 }}>
      <div className="toolbar">
        <div>
          <h1 className="page__title">Administration</h1>
          <p className="page__subtitle" style={{ marginBottom: 0 }}>
            Users, roles, organization settings, onboarding, and billing.
          </p>
        </div>
      </div>

      <div className="tabs">
        {TABS.map((t) => (
          <button
            key={t.key}
            className={`tab${tab === t.key ? ' tab--active' : ''}`}
            onClick={() => setTab(t.key)}
          >
            {t.label}
          </button>
        ))}
      </div>

      {tab === 'onboarding' && <OnboardingTab />}
      {tab === 'users' && <UsersTab />}
      {tab === 'roles' && <RolesTab />}
      {tab === 'organization' && <OrganizationTab />}
      {tab === 'billing' && <BillingTab />}
    </main>
  );
}

// ---- Onboarding ----

function OnboardingTab() {
  const navigate = useNavigate();
  const [data, setData] = useState<Onboarding | null>(null);

  useEffect(() => {
    getOnboarding().then(setData).catch(() => undefined);
  }, []);

  if (!data) return <p className="muted">Loading…</p>;

  return (
    <div style={{ maxWidth: 720 }}>
      <div className="card" style={{ padding: 20, marginBottom: 16 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 8 }}>
          <strong>Setup progress</strong>
          <span className="muted">{data.completionPercent}% complete</span>
        </div>
        <div className="completion">
          <div className="completion__fill" style={{ width: `${data.completionPercent}%` }} />
        </div>
      </div>

      {data.steps.map((s) => (
        <div key={s.key} className="activity-row" style={{ padding: '14px 4px' }}>
          <span style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
            <span className={`onboard-check${s.done ? ' onboard-check--done' : ''}`}>{s.done ? '✓' : ''}</span>
            <span>
              <div style={{ fontWeight: 600, textDecoration: s.done ? 'line-through' : 'none' }}>{s.title}</div>
              <div className="muted" style={{ fontSize: 12 }}>{s.description}</div>
            </span>
          </span>
          {!s.done && (
            <button className="btn btn--ghost btn--inline" onClick={() => navigate(s.actionPath)}>
              Go →
            </button>
          )}
        </div>
      ))}
    </div>
  );
}

// ---- Users ----

function UsersTab() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [showInvite, setShowInvite] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function load() {
    listUsers().then(setUsers).catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'));
    listRoles().then(setRoles).catch(() => undefined);
  }
  useEffect(load, []);

  async function setRoleFor(id: string, roleId: string) {
    setUsers((prev) => prev.map((u) => (u.id === id ? { ...u, roleId } : u)));
    await updateAdminUser(id, { roleId });
  }
  async function setStatusFor(id: string, status: string) {
    const u = await updateAdminUser(id, { status });
    setUsers((prev) => prev.map((x) => (x.id === id ? u : x)));
  }

  return (
    <div>
      <div className="toolbar">
        <div className="muted">{users.length} user{users.length === 1 ? '' : 's'}</div>
        <button className="btn btn--inline" onClick={() => setShowInvite(true)}>
          + Invite user
        </button>
      </div>
      {error && <div className="error-text">{error}</div>}

      <div className="card" style={{ padding: 0 }}>
        <table className="table">
          <thead>
            <tr>
              <th>Name</th>
              <th>Email</th>
              <th>Role</th>
              <th>Status</th>
              <th>Last login</th>
            </tr>
          </thead>
          <tbody>
            {users.map((u) => (
              <tr key={u.id}>
                <td>{u.fullName}</td>
                <td className="muted">{u.email}</td>
                <td>
                  <select value={u.roleId} onChange={(e) => setRoleFor(u.id, e.target.value)}>
                    {roles.map((r) => (
                      <option key={r.id} value={r.id}>{r.name}</option>
                    ))}
                  </select>
                </td>
                <td>
                  <select value={u.status} onChange={(e) => setStatusFor(u.id, e.target.value)}>
                    {['Invited', 'Active', 'Suspended', 'Deactivated'].map((s) => (
                      <option key={s}>{s}</option>
                    ))}
                  </select>
                </td>
                <td className="muted">{u.lastLoginAt ? formatDate(u.lastLoginAt) : 'never'}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {showInvite && (
        <InviteModal roles={roles} onClose={() => setShowInvite(false)} onInvited={load} />
      )}
    </div>
  );
}

function InviteModal({
  roles,
  onClose,
  onInvited,
}: {
  roles: AdminRole[];
  onClose: () => void;
  onInvited: () => void;
}) {
  const [email, setEmail] = useState('');
  const [fullName, setFullName] = useState('');
  const [roleId, setRoleId] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [tempPassword, setTempPassword] = useState<string | null>(null);

  async function invite() {
    if (!email || !fullName || !roleId) {
      setError('Email, name, and role are required.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const r = await inviteUser({ email, fullName, roleId });
      setTempPassword(r.temporaryPassword);
      onInvited();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Invite failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>Invite user</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">✕</button>
        </div>

        {tempPassword ? (
          <div>
            <p>
              Invitation created. Share this one-time temporary password with the user — they'll be
              prompted to change it after activation.
            </p>
            <div className="temp-password">{tempPassword}</div>
            <div className="modal__footer">
              <button className="btn" onClick={onClose}>Done</button>
            </div>
          </div>
        ) : (
          <>
            <div className="field">
              <label>Full name</label>
              <input value={fullName} onChange={(e) => setFullName(e.target.value)} />
            </div>
            <div className="field">
              <label>Email</label>
              <input value={email} onChange={(e) => setEmail(e.target.value)} />
            </div>
            <div className="field">
              <label>Role</label>
              <select value={roleId} onChange={(e) => setRoleId(e.target.value)}>
                <option value="">Select a role…</option>
                {roles.map((r) => (
                  <option key={r.id} value={r.id}>{r.name}</option>
                ))}
              </select>
            </div>
            {error && <div className="error-text">{error}</div>}
            <div className="modal__footer">
              <button className="btn btn--ghost" onClick={onClose} disabled={busy}>Cancel</button>
              <button className="btn" onClick={invite} disabled={busy}>
                {busy ? 'Inviting…' : 'Send invite'}
              </button>
            </div>
          </>
        )}
      </div>
    </div>
  );
}

// ---- Roles ----

function RolesTab() {
  const [roles, setRoles] = useState<AdminRole[]>([]);
  const [perms, setPerms] = useState<AdminPermission[]>([]);
  const [showCreate, setShowCreate] = useState(false);
  const [error, setError] = useState<string | null>(null);

  function load() {
    listRoles().then(setRoles).catch((e) => setError(e instanceof Error ? e.message : 'Failed to load'));
    listPermissions().then(setPerms).catch(() => undefined);
  }
  useEffect(load, []);

  async function toggle(role: AdminRole, key: string) {
    if (role.isSystem) return;
    const permissions = { ...role.permissions, [key]: !role.permissions[key] };
    setRoles((prev) => prev.map((r) => (r.id === role.id ? { ...r, permissions } : r)));
    await updateRole(role.id, { permissions });
  }

  async function remove(role: AdminRole) {
    if (!confirm(`Delete role "${role.name}"?`)) return;
    try {
      await deleteRole(role.id);
      load();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Delete failed');
    }
  }

  return (
    <div>
      <div className="toolbar">
        <div className="muted">{roles.length} roles</div>
        <button className="btn btn--inline" onClick={() => setShowCreate(true)}>+ New role</button>
      </div>
      {error && <div className="error-text">{error}</div>}

      {roles.map((role) => (
        <div key={role.id} className="card" style={{ padding: 20, marginBottom: 12 }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <div>
              <strong>{role.name}</strong>{' '}
              {role.isSystem && <span className="badge badge--neutral">system</span>}
              <div className="muted" style={{ fontSize: 12 }}>
                {role.description} · {role.userCount} user{role.userCount === 1 ? '' : 's'}
              </div>
            </div>
            {!role.isSystem && (
              <button className="btn btn--ghost btn--inline" onClick={() => remove(role)}>Delete</button>
            )}
          </div>
          <div className="perm-grid">
            {perms.map((p) => (
              <label key={p.key} className={`perm${role.isSystem ? ' perm--locked' : ''}`}>
                <input
                  type="checkbox"
                  checked={!!role.permissions[p.key]}
                  disabled={role.isSystem}
                  onChange={() => toggle(role, p.key)}
                />
                {p.label}
              </label>
            ))}
          </div>
        </div>
      ))}

      {showCreate && (
        <CreateRoleModal perms={perms} onClose={() => setShowCreate(false)} onCreated={load} />
      )}
    </div>
  );
}

function CreateRoleModal({
  perms,
  onClose,
  onCreated,
}: {
  perms: AdminPermission[];
  onClose: () => void;
  onCreated: () => void;
}) {
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [permissions, setPermissions] = useState<Record<string, boolean>>({});
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  async function create() {
    if (!name) {
      setError('Role name is required.');
      return;
    }
    setBusy(true);
    setError(null);
    try {
      await createRole({ name, description: description || undefined, permissions });
      onCreated();
      onClose();
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Create failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>New role</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">✕</button>
        </div>
        <div className="field">
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="field">
          <label>Description</label>
          <input value={description} onChange={(e) => setDescription(e.target.value)} />
        </div>
        <div className="field">
          <label>Permissions</label>
          <div className="perm-grid">
            {perms.map((p) => (
              <label key={p.key} className="perm">
                <input
                  type="checkbox"
                  checked={!!permissions[p.key]}
                  onChange={() => setPermissions((prev) => ({ ...prev, [p.key]: !prev[p.key] }))}
                />
                {p.label}
              </label>
            ))}
          </div>
        </div>
        {error && <div className="error-text">{error}</div>}
        <div className="modal__footer">
          <button className="btn btn--ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button className="btn" onClick={create} disabled={busy}>
            {busy ? 'Creating…' : 'Create role'}
          </button>
        </div>
      </div>
    </div>
  );
}

// ---- Organization ----

function OrganizationTab() {
  const [org, setOrg] = useState<Organization | null>(null);
  const [entities, setEntities] = useState<AdminEntity[]>([]);
  const [name, setName] = useState('');
  const [domain, setDomain] = useState('');
  const [saved, setSaved] = useState(false);
  const [editing, setEditing] = useState<AdminEntity | null>(null);

  function load() {
    getOrganization().then((o) => {
      setOrg(o);
      setName(o.name);
      setDomain(o.domain ?? '');
    });
    listEntities().then(setEntities).catch(() => undefined);
  }
  useEffect(load, []);

  async function saveOrg() {
    setSaved(false);
    const o = await updateOrganization({ name, domain });
    setOrg(o);
    setSaved(true);
  }

  if (!org) return <p className="muted">Loading…</p>;

  return (
    <div style={{ maxWidth: 760 }}>
      <div className="card" style={{ padding: 20, marginBottom: 16 }}>
        <strong>Organization</strong>
        <div className="form-grid" style={{ marginTop: 12 }}>
          <div className="field">
            <label>Name</label>
            <input value={name} onChange={(e) => setName(e.target.value)} />
          </div>
          <div className="field">
            <label>Domain</label>
            <input value={domain} onChange={(e) => setDomain(e.target.value)} placeholder="acme.com" />
          </div>
        </div>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <button className="btn btn--inline" onClick={saveOrg}>Save</button>
          {saved && <span className="muted">Saved.</span>}
          <span className="muted" style={{ marginLeft: 'auto' }}>
            Plan: <strong>{org.plan}</strong> · {org.userCount} users · {org.entityCount} entities
          </span>
        </div>
      </div>

      <div className="card" style={{ padding: 0 }}>
        <table className="table">
          <thead>
            <tr>
              <th>Entity</th>
              <th>Code</th>
              <th>Type</th>
              <th>State</th>
              <th>Status</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {entities.map((e) => (
              <tr key={e.id}>
                <td>{e.name}</td>
                <td className="muted">{e.code}</td>
                <td>{e.type}</td>
                <td>{e.state ?? '—'}</td>
                <td><span className="badge badge--neutral">{e.status}</span></td>
                <td>
                  <button className="btn btn--ghost btn--inline" onClick={() => setEditing(e)}>Edit</button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {editing && (
        <EditEntityModal entity={editing} onClose={() => setEditing(null)} onSaved={load} />
      )}
    </div>
  );
}

function EditEntityModal({
  entity,
  onClose,
  onSaved,
}: {
  entity: AdminEntity;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(entity.name);
  const [state, setState] = useState(entity.state ?? '');
  const [ein, setEin] = useState(entity.ein ?? '');
  const [address, setAddress] = useState('');
  const [busy, setBusy] = useState(false);

  async function save() {
    setBusy(true);
    try {
      await updateEntity(entity.id, { name, state, ein, address });
      onSaved();
      onClose();
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>Edit entity</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">✕</button>
        </div>
        <div className="field">
          <label>Name</label>
          <input value={name} onChange={(e) => setName(e.target.value)} />
        </div>
        <div className="form-grid">
          <div className="field">
            <label>State</label>
            <input value={state} onChange={(e) => setState(e.target.value)} />
          </div>
          <div className="field">
            <label>EIN</label>
            <input value={ein} onChange={(e) => setEin(e.target.value)} />
          </div>
        </div>
        <div className="field">
          <label>Address</label>
          <input value={address} onChange={(e) => setAddress(e.target.value)} placeholder="Completes onboarding" />
        </div>
        <div className="modal__footer">
          <button className="btn btn--ghost" onClick={onClose} disabled={busy}>Cancel</button>
          <button className="btn" onClick={save} disabled={busy}>{busy ? 'Saving…' : 'Save'}</button>
        </div>
      </div>
    </div>
  );
}

// ---- Billing ----

function BillingTab() {
  const [data, setData] = useState<Billing | null>(null);
  const [busy, setBusy] = useState(false);

  function load() {
    getBilling().then(setData).catch(() => undefined);
  }
  useEffect(load, []);

  async function switchTo(plan: string) {
    if (!confirm(`Switch to the ${plan} plan?`)) return;
    setBusy(true);
    try {
      await changePlan(plan);
      load();
    } finally {
      setBusy(false);
    }
  }

  if (!data) return <p className="muted">Loading…</p>;

  const limit = (n: number) => (n >= 2147483647 ? '∞' : n.toLocaleString());
  const gb = (bytes: number) => (bytes / 1024 / 1024 / 1024).toFixed(2);

  return (
    <div>
      <div className="kpi-grid">
        <UsageCard label="Users" value={data.usage.users} max={data.currentPlanDetail.userLimit} />
        <UsageCard label="Documents" value={data.usage.documents} max={data.currentPlanDetail.documentLimit} />
        <div className="kpi">
          <div className="kpi__label">Storage</div>
          <div className="kpi__value">{gb(data.usage.storageBytes)}</div>
          <div className="kpi__sub">of {data.currentPlanDetail.storageGb} GB</div>
        </div>
        <UsageCard label="Data rooms" value={data.usage.rooms} max={data.currentPlanDetail.roomLimit} />
      </div>

      <h2 style={{ fontSize: 18, margin: '8px 0 12px' }}>Plans</h2>
      <div className="report-grid">
        {data.plans.map((p) => (
          <PlanCard
            key={p.key}
            plan={p}
            current={p.key === data.currentPlan}
            limitFmt={limit}
            busy={busy}
            onSwitch={() => switchTo(p.key)}
          />
        ))}
      </div>

      {data.invoices.length > 0 && (
        <>
          <h2 style={{ fontSize: 18, margin: '24px 0 12px' }}>Invoices</h2>
          <div className="card" style={{ padding: 0 }}>
            <table className="table">
              <thead>
                <tr>
                  <th>Invoice</th>
                  <th>Date</th>
                  <th>Amount</th>
                  <th>Status</th>
                </tr>
              </thead>
              <tbody>
                {data.invoices.map((inv) => (
                  <tr key={inv.number}>
                    <td>{inv.number}</td>
                    <td className="muted">{inv.date}</td>
                    <td>${inv.amount.toLocaleString()}</td>
                    <td><span className="badge badge--positive">{inv.status}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

function UsageCard({ label, value, max }: { label: string; value: number; max: number }) {
  const unlimited = max >= 2147483647;
  const pct = unlimited ? 0 : Math.min(100, Math.round((value / Math.max(1, max)) * 100));
  return (
    <div className="kpi">
      <div className="kpi__label">{label}</div>
      <div className="kpi__value">{value.toLocaleString()}</div>
      <div className="kpi__sub">of {unlimited ? '∞' : max.toLocaleString()}</div>
      {!unlimited && (
        <div className="bar-row__track" style={{ marginTop: 8 }}>
          <div className="bar-row__fill" style={{ width: `${pct}%` }} />
        </div>
      )}
    </div>
  );
}

function PlanCard({
  plan,
  current,
  limitFmt,
  busy,
  onSwitch,
}: {
  plan: Plan;
  current: boolean;
  limitFmt: (n: number) => string;
  busy: boolean;
  onSwitch: () => void;
}) {
  return (
    <div className="report-card" style={{ cursor: 'default', borderColor: current ? 'var(--brand)' : undefined }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
        <div className="report-card__title">{plan.name}</div>
        {current && <span className="badge badge--positive">Current</span>}
      </div>
      <div style={{ fontSize: 22, fontWeight: 700 }}>
        ${plan.pricePerMonth}
        <span className="muted" style={{ fontSize: 12, fontWeight: 400 }}>/mo</span>
      </div>
      <div className="muted" style={{ fontSize: 12, lineHeight: 1.7 }}>
        {limitFmt(plan.userLimit)} users · {limitFmt(plan.documentLimit)} docs<br />
        {plan.storageGb} GB · {limitFmt(plan.roomLimit)} rooms
      </div>
      {!current && (
        <button className="btn btn--inline" onClick={onSwitch} disabled={busy} style={{ marginTop: 8 }}>
          Switch
        </button>
      )}
    </div>
  );
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return Number.isNaN(d.getTime()) ? iso : d.toLocaleDateString();
}
