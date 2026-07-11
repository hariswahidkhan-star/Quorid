const API_URL = import.meta.env.VITE_API_URL ?? 'http://localhost:5080';

const TOKEN_KEY = 'quorid.accessToken';

export interface UserSummary {
  id: string;
  email: string;
  fullName: string;
  tenantId: string;
  entityId: string;
}

export interface AuthResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserSummary;
}

export class ApiError extends Error {
  status: number;

  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

export function getToken(): string | null {
  return localStorage.getItem(TOKEN_KEY);
}

export function setToken(token: string | null): void {
  if (token) localStorage.setItem(TOKEN_KEY, token);
  else localStorage.removeItem(TOKEN_KEY);
}

export async function apiFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  headers.set('Content-Type', 'application/json');

  const token = getToken();
  if (token) headers.set('Authorization', `Bearer ${token}`);

  const res = await fetch(`${API_URL}${path}`, { ...options, headers });

  if (!res.ok) {
    let message = `Request failed (${res.status})`;
    try {
      const body = await res.json();
      if (body?.error) message = body.error as string;
    } catch {
      /* non-JSON error body — keep default message */
    }
    throw new ApiError(message, res.status);
  }

  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

export interface BatchItemResult {
  fileName: string;
  success: boolean;
  documentId: string | null;
  error: string | null;
}

export interface BatchUploadResult {
  batchId: string;
  total: number;
  completed: number;
  failed: number;
  items: BatchItemResult[];
}

/** Uploads files via multipart/form-data (browser sets the boundary). */
export async function uploadDocuments(files: File[]): Promise<BatchUploadResult> {
  const form = new FormData();
  files.forEach((f) => form.append('files', f));

  const token = getToken();
  const res = await fetch(`${API_URL}/api/documents/upload-batch`, {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    body: form,
  });

  if (!res.ok) {
    let message = `Upload failed (${res.status})`;
    try {
      const body = await res.json();
      if (body?.error) message = body.error as string;
    } catch {
      /* ignore */
    }
    throw new ApiError(message, res.status);
  }

  return (await res.json()) as BatchUploadResult;
}

// ---- Identity Vault (Module 2) ----

export interface VaultField {
  fieldName: string;
  value: string | null;
  tier: string;
  sourceDocumentCount: number;
  sourceDocumentIds: string[];
  hasConflict: boolean;
}

export interface DomainCard {
  domainCode: string;
  domainName: string;
  documentCount: number;
  fieldCount: number;
  completionPercent: number;
  fields: VaultField[];
}

export interface TierBreakdown {
  t1: number;
  t2: number;
  t3: number;
  t4: number;
}

export interface VaultProfile {
  entityId: string;
  healthScore: number;
  documentCount: number;
  tiers: TierBreakdown;
  domains: DomainCard[];
}

export interface CrossValidationResult {
  ruleCode: string;
  ruleName: string;
  severity: string;
  passed: boolean;
  message: string;
}

export interface CrossValidationReport {
  entityId: string;
  total: number;
  passed: number;
  failed: number;
  passRatePercent: number;
  results: CrossValidationResult[];
}

export function getVaultProfile(): Promise<VaultProfile> {
  return apiFetch<VaultProfile>('/api/vault');
}

export function runCrossValidation(): Promise<CrossValidationReport> {
  return apiFetch<CrossValidationReport>('/api/vault/cross-validate', { method: 'POST' });
}

// ---- Document Management (Module 3) ----

export interface DocumentListItem {
  id: string;
  fileName: string;
  title: string | null;
  fileType: string;
  fileSizeBytes: number;
  status: string;
  domainCode: string | null;
  verificationTier: string;
  privacyLevel: string;
  entityId: string;
  expiryDate: string | null;
  createdAt: string;
}

export interface DocumentList {
  page: number;
  pageSize: number;
  total: number;
  items: DocumentListItem[];
}

export interface ExtractedFieldDetail {
  id: string;
  fieldName: string;
  fieldValue: string | null;
  overrideValue: string | null;
  confidence: number | null;
  sourcePage: number | null;
}

export interface DocumentDetail {
  id: string;
  fileName: string;
  title: string | null;
  description: string | null;
  fileType: string;
  fileSizeBytes: number;
  version: number;
  status: string;
  domainCode: string | null;
  categoryCode: string | null;
  typeCode: string | null;
  subtypeCode: string | null;
  privacyLevel: string;
  verificationTier: string;
  expiryDate: string | null;
  entityId: string;
  classificationConfidence: number | null;
  createdAt: string;
  updatedAt: string;
  extractedFields: ExtractedFieldDetail[];
  tags: { tagName: string; source: string }[];
}

export interface DocumentVersion {
  id: string;
  versionNumber: number;
  fileName: string;
  fileSizeBytes: number;
  changeNote: string | null;
  aiDiffSummary: string | null;
  isCurrent: boolean;
  uploadedBy: string;
  createdAt: string;
}

export interface ActivityEntry {
  action: string;
  userId: string | null;
  createdAt: string;
  details: string | null;
}

export interface ExpiringDocument {
  id: string;
  fileName: string;
  title: string | null;
  domainCode: string | null;
  expiryDate: string;
  daysUntilExpiry: number;
}

export interface DocumentFilters {
  domain?: string;
  status?: string;
  search?: string;
}

export type DocumentUpdate = Partial<{
  title: string;
  description: string;
  domainCode: string;
  categoryCode: string;
  typeCode: string;
  subtypeCode: string;
  privacyLevel: string;
  expiryDate: string;
  tags: string[];
}>;

export function listDocuments(filters: DocumentFilters = {}): Promise<DocumentList> {
  const params = new URLSearchParams();
  if (filters.domain) params.set('domain', filters.domain);
  if (filters.status) params.set('status', filters.status);
  if (filters.search) params.set('search', filters.search);
  const qs = params.toString();
  return apiFetch<DocumentList>(`/api/documents${qs ? `?${qs}` : ''}`);
}

export function getDocument(id: string): Promise<DocumentDetail> {
  return apiFetch<DocumentDetail>(`/api/documents/${id}`);
}

export function updateDocument(id: string, body: DocumentUpdate): Promise<unknown> {
  return apiFetch(`/api/documents/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function archiveDocument(id: string): Promise<unknown> {
  return apiFetch(`/api/documents/${id}`, { method: 'DELETE' });
}

export function listVersions(id: string): Promise<DocumentVersion[]> {
  return apiFetch<DocumentVersion[]>(`/api/documents/${id}/versions`);
}

export function restoreVersion(id: string, versionId: string): Promise<unknown> {
  return apiFetch(`/api/documents/${id}/versions/${versionId}/restore`, { method: 'POST' });
}

export async function uploadVersion(id: string, file: File, changeNote: string): Promise<DocumentVersion> {
  const form = new FormData();
  form.append('file', file);
  form.append('changeNote', changeNote);

  const token = getToken();
  const res = await fetch(`${API_URL}/api/documents/${id}/versions`, {
    method: 'POST',
    headers: token ? { Authorization: `Bearer ${token}` } : {},
    body: form,
  });

  if (!res.ok) {
    let message = `Upload failed (${res.status})`;
    try {
      const body = await res.json();
      if (body?.error) message = body.error as string;
    } catch {
      /* ignore */
    }
    throw new ApiError(message, res.status);
  }

  return (await res.json()) as DocumentVersion;
}

export function getActivity(id: string): Promise<ActivityEntry[]> {
  return apiFetch<ActivityEntry[]>(`/api/documents/${id}/activity`);
}

export function getExpiring(days = 90): Promise<ExpiringDocument[]> {
  return apiFetch<ExpiringDocument[]>(`/api/documents/expiring?days=${days}`);
}

// ---- Sharing & Access (Module 5) ----

export interface Share {
  id: string;
  documentId: string;
  documentTitle: string | null;
  recipientEmail: string;
  recipientName: string | null;
  permissionLevel: string;
  status: string;
  expiryDate: string | null;
  watermark: boolean;
  trackViews: boolean;
  viewCount: number;
  lastViewedAt: string | null;
  accessToken: string;
  createdAt: string;
}

export interface CreateShareInput {
  recipientEmail: string;
  recipientName?: string;
  permissionLevel: string;
  expiryDate?: string;
  watermark?: boolean;
  trackViews?: boolean;
  message?: string;
}

export interface ShareView {
  ipAddress: string;
  userAgent: string | null;
  viewedAt: string;
}

export interface ShareAnalytics {
  shareId: string;
  viewCount: number;
  lastViewedAt: string | null;
  views: ShareView[];
}

export interface PublicShare {
  documentTitle: string;
  fileType: string;
  permissionLevel: string;
  watermark: boolean;
  message: string;
}

export function createShare(documentId: string, input: CreateShareInput): Promise<Share> {
  return apiFetch<Share>(`/api/documents/${documentId}/share`, {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function listShares(): Promise<Share[]> {
  return apiFetch<Share[]>('/api/shares');
}

export function revokeShare(id: string): Promise<unknown> {
  return apiFetch(`/api/shares/${id}/revoke`, { method: 'POST' });
}

export function getShareAnalytics(id: string): Promise<ShareAnalytics> {
  return apiFetch<ShareAnalytics>(`/api/shares/${id}/analytics`);
}

/** Public tokenized share view — no auth required. */
export async function getPublicShare(token: string): Promise<PublicShare> {
  return unauthenticatedFetch<PublicShare>(`/api/share/${token}`);
}

/** Fetch helper for public (unauthenticated) endpoints. */
async function unauthenticatedFetch<T>(path: string, options: RequestInit = {}): Promise<T> {
  const headers = new Headers(options.headers);
  if (options.body) headers.set('Content-Type', 'application/json');
  const res = await fetch(`${API_URL}${path}`, { ...options, headers });
  if (!res.ok) {
    let message = `Unavailable (${res.status})`;
    try {
      const body = await res.json();
      if (body?.error) message = body.error as string;
    } catch {
      /* ignore */
    }
    throw new ApiError(message, res.status);
  }
  if (res.status === 204) return undefined as T;
  return (await res.json()) as T;
}

// ---- Data Rooms ----

export interface RoomType {
  id: string;
  code: string;
  name: string;
  description: string | null;
  folders: string[];
}

export interface RoomListItem {
  id: string;
  name: string;
  status: string;
  roomTypeCode: string;
  expiryDate: string;
  documentCount: number;
  guestCount: number;
  createdAt: string;
}

export interface RoomFolder {
  id: string;
  name: string;
  displayOrder: number;
}

export interface RoomDocument {
  id: string;
  documentId: string;
  fileName: string;
  title: string | null;
  folderId: string;
  permissionLevel: string;
}

export interface RoomGuest {
  id: string;
  email: string;
  name: string | null;
  role: string;
  permissionLevel: string;
  status: string;
  ndaSigned: boolean;
  views: number;
  lastAccessAt: string | null;
  accessToken: string;
}

export interface RoomDetail {
  id: string;
  name: string;
  description: string | null;
  status: string;
  roomTypeCode: string;
  expiryDate: string;
  ndaRequired: boolean;
  watermark: boolean;
  watermarkText: string | null;
  qaEnabled: boolean;
  downloadPolicy: string;
  folders: RoomFolder[];
  documents: RoomDocument[];
  guests: RoomGuest[];
}

export interface GuestInput {
  email: string;
  name?: string;
  role?: string;
  permissionLevel?: string;
}

export interface CreateRoomInput {
  name: string;
  roomTypeCode: string;
  expiryDate: string;
  ndaRequired?: boolean;
  watermark?: boolean;
  watermarkText?: string;
  qaEnabled?: boolean;
  downloadPolicy?: string;
  folders?: string[];
  documentIds?: string[];
  guests?: GuestInput[];
}

export interface RoomQuestion {
  id: string;
  guestEmail: string;
  category: string;
  questionText: string;
  status: string;
  answerText: string | null;
  isPublic: boolean;
  createdAt: string;
}

export interface GuestEngagement {
  guestId: string;
  email: string;
  name: string | null;
  views: number;
  lastAccessAt: string | null;
  engagement: string;
}

export interface RoomAnalytics {
  roomId: string;
  guestCount: number;
  totalViews: number;
  guests: GuestEngagement[];
}

export function getRoomTypes(): Promise<RoomType[]> {
  return apiFetch<RoomType[]>('/api/rooms/room-types');
}

export function listRooms(): Promise<RoomListItem[]> {
  return apiFetch<RoomListItem[]>('/api/rooms');
}

export function createRoom(input: CreateRoomInput): Promise<RoomDetail> {
  return apiFetch<RoomDetail>('/api/rooms', { method: 'POST', body: JSON.stringify(input) });
}

export function getRoom(id: string): Promise<RoomDetail> {
  return apiFetch<RoomDetail>(`/api/rooms/${id}`);
}

export function updateRoom(
  id: string,
  body: Partial<{ name: string; expiryDate: string; status: string; downloadPolicy: string; qaEnabled: boolean; watermark: boolean }>,
): Promise<unknown> {
  return apiFetch(`/api/rooms/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function deleteRoom(id: string): Promise<unknown> {
  return apiFetch(`/api/rooms/${id}`, { method: 'DELETE' });
}

export function inviteGuests(id: string, guests: GuestInput[]): Promise<RoomDetail> {
  return apiFetch<RoomDetail>(`/api/rooms/${id}/guests`, { method: 'POST', body: JSON.stringify({ guests }) });
}

export function revokeGuest(roomId: string, guestId: string): Promise<unknown> {
  return apiFetch(`/api/rooms/${roomId}/guests/${guestId}`, { method: 'DELETE' });
}

export function getRoomAnalytics(id: string): Promise<RoomAnalytics> {
  return apiFetch<RoomAnalytics>(`/api/rooms/${id}/analytics`);
}

export function listRoomQuestions(id: string): Promise<RoomQuestion[]> {
  return apiFetch<RoomQuestion[]>(`/api/rooms/${id}/qa`);
}

export function answerQuestion(roomId: string, questionId: string, answerText: string, isPublic: boolean): Promise<unknown> {
  return apiFetch(`/api/rooms/${roomId}/qa/${questionId}/answer`, {
    method: 'PUT',
    body: JSON.stringify({ answerText, isPublic }),
  });
}

// ---- Guest portal (public) ----

export interface GuestDoc {
  documentId: string;
  fileName: string;
  fileType: string;
  fileSizeBytes: number;
}

export interface GuestFolder {
  id: string;
  name: string;
  documents: GuestDoc[];
}

export interface GuestRoom {
  roomName: string;
  hostEntity: string;
  expiryDate: string;
  ndaRequired: boolean;
  ndaSigned: boolean;
  watermark: boolean;
  watermarkText: string | null;
  downloadPolicy: string;
  qaEnabled: boolean;
  guestName: string;
  guestEmail: string;
  permissionLevel: string;
  folders: GuestFolder[];
}

export interface GuestQuestion {
  id: string;
  category: string;
  questionText: string;
  status: string;
  answerText: string | null;
  createdAt: string;
}

export function getGuestRoom(token: string): Promise<GuestRoom> {
  return unauthenticatedFetch<GuestRoom>(`/api/guest/room/${token}`);
}

export function signNda(token: string, legalName: string): Promise<unknown> {
  return unauthenticatedFetch(`/api/guest/room/${token}/sign-nda`, {
    method: 'POST',
    body: JSON.stringify({ legalName }),
  });
}

export function logGuestView(token: string, documentId: string, page?: number): Promise<unknown> {
  return unauthenticatedFetch(`/api/guest/room/${token}/view`, {
    method: 'POST',
    body: JSON.stringify({ documentId, page }),
  });
}

export function getGuestQuestions(token: string): Promise<GuestQuestion[]> {
  return unauthenticatedFetch<GuestQuestion[]>(`/api/guest/room/${token}/qa`);
}

export function submitGuestQuestion(token: string, category: string, questionText: string): Promise<unknown> {
  return unauthenticatedFetch(`/api/guest/room/${token}/qa`, {
    method: 'POST',
    body: JSON.stringify({ category, questionText }),
  });
}

// ---- Compliance (Module 6) ----

export interface Framework {
  id: string;
  code: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  requirementCount: number;
}

export interface RequirementStatus {
  requirementId: string;
  name: string;
  status: string;
  documentId: string | null;
  documentTitle: string | null;
}

export interface FrameworkStatus {
  frameworkId: string;
  code: string;
  name: string;
  score: number;
  present: number;
  total: number;
  requirements: RequirementStatus[];
}

export interface FrameworkOverview {
  frameworkId: string;
  code: string;
  name: string;
  score: number;
  present: number;
  total: number;
}

export interface ComplianceOverview {
  entityId: string;
  overallScore: number;
  frameworks: FrameworkOverview[];
}

export interface ComplianceCalendarItem {
  frameworkName: string;
  requirementName: string;
  documentId: string;
  documentTitle: string;
  expiryDate: string;
  daysUntilExpiry: number;
}

export function getComplianceOverview(): Promise<ComplianceOverview> {
  return apiFetch<ComplianceOverview>('/api/compliance/overview');
}

export function getFrameworks(): Promise<Framework[]> {
  return apiFetch<Framework[]>('/api/compliance/frameworks');
}

export function getFrameworkStatus(id: string): Promise<FrameworkStatus> {
  return apiFetch<FrameworkStatus>(`/api/compliance/frameworks/${id}/status`);
}

export function getComplianceCalendar(): Promise<ComplianceCalendarItem[]> {
  return apiFetch<ComplianceCalendarItem[]>('/api/compliance/calendar');
}

// ---- Engagements: Projects / Vendors / Clients (Modules 7–9) ----

export interface EngagementListItem {
  id: string;
  type: string;
  name: string;
  contactEmail: string | null;
  status: string;
  dueDate: string | null;
  total: number;
  received: number;
  progress: number;
  createdAt: string;
}

export interface ChecklistItemDto {
  id: string;
  name: string;
  domainCode: string | null;
  isRequired: boolean;
  status: string;
  documentId: string | null;
  documentTitle: string | null;
  receivedAt: string | null;
}

export interface EngagementDetail {
  id: string;
  type: string;
  name: string;
  contactEmail: string | null;
  status: string;
  dueDate: string | null;
  portalToken: string | null;
  total: number;
  received: number;
  progress: number;
  checklist: ChecklistItemDto[];
}

export interface ChecklistInput {
  name: string;
  domainCode?: string;
  isRequired?: boolean;
}

export interface CreateEngagementInput {
  type: string;
  name: string;
  contactEmail?: string;
  dueDate?: string;
  checklist?: ChecklistInput[];
}

export function listEngagements(type: string): Promise<EngagementListItem[]> {
  return apiFetch<EngagementListItem[]>(`/api/engagements?type=${type}`);
}

export function createEngagement(input: CreateEngagementInput): Promise<EngagementDetail> {
  return apiFetch<EngagementDetail>('/api/engagements', { method: 'POST', body: JSON.stringify(input) });
}

export function getEngagement(id: string): Promise<EngagementDetail> {
  return apiFetch<EngagementDetail>(`/api/engagements/${id}`);
}

export function updateEngagement(
  id: string,
  body: Partial<{ name: string; status: string; dueDate: string }>,
): Promise<unknown> {
  return apiFetch(`/api/engagements/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function deleteEngagement(id: string): Promise<unknown> {
  return apiFetch(`/api/engagements/${id}`, { method: 'DELETE' });
}

export function addChecklistItems(id: string, items: ChecklistInput[]): Promise<EngagementDetail> {
  return apiFetch<EngagementDetail>(`/api/engagements/${id}/checklist`, {
    method: 'POST',
    body: JSON.stringify({ items }),
  });
}

export function assignChecklistDoc(id: string, itemId: string, documentId: string | null): Promise<EngagementDetail> {
  return apiFetch<EngagementDetail>(`/api/engagements/${id}/checklist/${itemId}/assign`, {
    method: 'PUT',
    body: JSON.stringify({ documentId }),
  });
}

export function removeChecklistItem(id: string, itemId: string): Promise<unknown> {
  return apiFetch(`/api/engagements/${id}/checklist/${itemId}`, { method: 'DELETE' });
}

export function sendCollectionRequest(id: string): Promise<{ token: string }> {
  return apiFetch<{ token: string }>(`/api/engagements/${id}/collection-request`, { method: 'POST' });
}

// ---- Collection portal (public) ----

export interface PortalChecklist {
  id: string;
  name: string;
  status: string;
}

export interface PortalInfo {
  engagementName: string;
  hostEntity: string;
  type: string;
  checklist: PortalChecklist[];
}

export function getPortalInfo(token: string): Promise<PortalInfo> {
  return unauthenticatedFetch<PortalInfo>(`/api/portal/${token}`);
}

export async function portalUpload(
  token: string,
  file: File,
  checklistItemId?: string,
): Promise<{ uploaded: boolean }> {
  const form = new FormData();
  form.append('file', file);
  if (checklistItemId) form.append('checklistItemId', checklistItemId);

  const res = await fetch(`${API_URL}/api/portal/${token}/upload`, { method: 'POST', body: form });
  if (!res.ok) {
    let message = `Upload failed (${res.status})`;
    try {
      const body = await res.json();
      if (body?.error) message = body.error as string;
    } catch {
      /* ignore */
    }
    throw new ApiError(message, res.status);
  }
  return (await res.json()) as { uploaded: boolean };
}

// ---- Proposals (Module 10) ----

export interface ProposalListItem {
  id: string;
  title: string;
  recipientName: string | null;
  stage: string;
  value: number | null;
  dueDate: string | null;
  documentCount: number;
  createdAt: string;
}

export interface ProposalDocumentItem {
  id: string;
  documentId: string;
  fileName: string;
  title: string | null;
  displayOrder: number;
}

export interface ProposalDetail {
  id: string;
  title: string;
  recipientName: string | null;
  stage: string;
  value: number | null;
  coverLetter: string | null;
  dueDate: string | null;
  documents: ProposalDocumentItem[];
}

export interface ProposalAnalytics {
  total: number;
  won: number;
  lost: number;
  open: number;
  winRatePercent: number;
  pipelineValue: number;
  wonValue: number;
}

export function listProposals(): Promise<ProposalListItem[]> {
  return apiFetch<ProposalListItem[]>('/api/proposals');
}

export function getProposalAnalytics(): Promise<ProposalAnalytics> {
  return apiFetch<ProposalAnalytics>('/api/proposals/analytics');
}

export function createProposal(input: {
  title: string;
  recipientName?: string;
  value?: number;
  dueDate?: string;
}): Promise<ProposalDetail> {
  return apiFetch<ProposalDetail>('/api/proposals', { method: 'POST', body: JSON.stringify(input) });
}

export function getProposal(id: string): Promise<ProposalDetail> {
  return apiFetch<ProposalDetail>(`/api/proposals/${id}`);
}

export function updateProposal(
  id: string,
  body: Partial<{ title: string; recipientName: string; stage: string; value: number; coverLetter: string; dueDate: string }>,
): Promise<unknown> {
  return apiFetch(`/api/proposals/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function deleteProposal(id: string): Promise<unknown> {
  return apiFetch(`/api/proposals/${id}`, { method: 'DELETE' });
}

export function addProposalDocuments(id: string, documentIds: string[]): Promise<ProposalDetail> {
  return apiFetch<ProposalDetail>(`/api/proposals/${id}/documents`, {
    method: 'POST',
    body: JSON.stringify({ documentIds }),
  });
}

export function reorderProposalDocuments(id: string, proposalDocumentIds: string[]): Promise<ProposalDetail> {
  return apiFetch<ProposalDetail>(`/api/proposals/${id}/documents/reorder`, {
    method: 'PUT',
    body: JSON.stringify({ proposalDocumentIds }),
  });
}

export function removeProposalDocument(id: string, proposalDocId: string): Promise<unknown> {
  return apiFetch(`/api/proposals/${id}/documents/${proposalDocId}`, { method: 'DELETE' });
}

export function generateCoverLetter(id: string): Promise<{ coverLetter: string }> {
  return apiFetch<{ coverLetter: string }>(`/api/proposals/${id}/cover-letter`, { method: 'POST' });
}

// ---- Module 11: Analytics ----

export interface Kpi {
  key: string;
  label: string;
  value: string;
  sublabel: string | null;
  tone: string;
}

export interface BreakdownSlice {
  label: string;
  count: number;
  value: number | null;
}

export interface Breakdown {
  key: string;
  title: string;
  slices: BreakdownSlice[];
}

export interface ActivityEntry {
  action: string;
  resourceType: string;
  at: string;
}

export interface Dashboard {
  kpis: Kpi[];
  breakdowns: Breakdown[];
  recentActivity: ActivityEntry[];
}

export interface AskResponse {
  answer: string;
  metric: string | null;
  value: string | null;
  breakdown: BreakdownSlice[];
  suggestions: string[];
}

export interface ReportSummary {
  key: string;
  title: string;
  category: string;
  description: string;
}

export interface ReportResult {
  key: string;
  title: string;
  category: string;
  columns: string[];
  rows: string[][];
  totalRows: number;
}

export function getDashboard(): Promise<Dashboard> {
  return apiFetch<Dashboard>('/api/analytics/dashboard');
}

export function askQuorid(question: string): Promise<AskResponse> {
  return apiFetch<AskResponse>('/api/analytics/ask', {
    method: 'POST',
    body: JSON.stringify({ question }),
  });
}

export function listReports(): Promise<ReportSummary[]> {
  return apiFetch<ReportSummary[]>('/api/analytics/reports');
}

export function runReport(key: string): Promise<ReportResult> {
  return apiFetch<ReportResult>(`/api/analytics/reports/${key}`);
}

// ---- Module 4: Document Creation studio ----

export interface DocTemplate {
  key: string;
  name: string;
  category: string;
  description: string | null;
  isSystem: boolean;
}

export interface DraftListItem {
  id: string;
  title: string;
  templateKey: string | null;
  status: string;
  finalized: boolean;
  updatedAt: string;
}

export interface DraftDetail {
  id: string;
  title: string;
  templateKey: string | null;
  prompt: string | null;
  body: string;
  status: string;
  documentId: string | null;
  updatedAt: string;
}

export interface GenerateDraftInput {
  templateKey?: string;
  documentType: string;
  title?: string;
  prompt: string;
  recipient?: string;
  variables?: Record<string, string>;
}

export function listTemplates(): Promise<DocTemplate[]> {
  return apiFetch<DocTemplate[]>('/api/studio/templates');
}

export function createTemplate(input: {
  name: string;
  category: string;
  description?: string;
  body: string;
}): Promise<DocTemplate> {
  return apiFetch<DocTemplate>('/api/studio/templates', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function listDrafts(): Promise<DraftListItem[]> {
  return apiFetch<DraftListItem[]>('/api/studio/drafts');
}

export function generateDraft(input: GenerateDraftInput): Promise<DraftDetail> {
  return apiFetch<DraftDetail>('/api/studio/drafts', {
    method: 'POST',
    body: JSON.stringify(input),
  });
}

export function getDraft(id: string): Promise<DraftDetail> {
  return apiFetch<DraftDetail>(`/api/studio/drafts/${id}`);
}

export function updateDraft(
  id: string,
  body: Partial<{ title: string; body: string; status: string }>,
): Promise<DraftDetail> {
  return apiFetch<DraftDetail>(`/api/studio/drafts/${id}`, {
    method: 'PUT',
    body: JSON.stringify(body),
  });
}

export function deleteDraft(id: string): Promise<unknown> {
  return apiFetch(`/api/studio/drafts/${id}`, { method: 'DELETE' });
}

export function improveDraft(id: string, instruction: string): Promise<DraftDetail> {
  return apiFetch<DraftDetail>(`/api/studio/drafts/${id}/improve`, {
    method: 'POST',
    body: JSON.stringify({ instruction }),
  });
}

export function finalizeDraft(id: string): Promise<{ id: string; documentId: string; status: string }> {
  return apiFetch(`/api/studio/drafts/${id}/finalize`, { method: 'POST' });
}

// ---- Module 12: Admin ----

export interface AdminUser {
  id: string;
  email: string;
  fullName: string;
  roleId: string;
  roleName: string | null;
  status: string;
  lastLoginAt: string | null;
  mfaEnabled: boolean;
}

export interface InviteUserResult {
  user: AdminUser;
  temporaryPassword: string;
}

export interface AdminRole {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  permissions: Record<string, boolean>;
  userCount: number;
}

export interface AdminPermission {
  key: string;
  label: string;
}

export interface Organization {
  tenantId: string;
  name: string;
  domain: string | null;
  plan: string;
  status: string;
  entityCount: number;
  userCount: number;
}

export interface AdminEntity {
  id: string;
  name: string;
  code: string;
  type: string;
  state: string | null;
  ein: string | null;
  status: string;
}

export interface OnboardingStep {
  key: string;
  title: string;
  description: string;
  done: boolean;
  actionPath: string;
}

export interface Onboarding {
  completionPercent: number;
  steps: OnboardingStep[];
}

export interface Plan {
  key: string;
  name: string;
  pricePerMonth: number;
  userLimit: number;
  documentLimit: number;
  storageGb: number;
  roomLimit: number;
}

export interface Usage {
  users: number;
  documents: number;
  storageBytes: number;
  rooms: number;
}

export interface Invoice {
  number: string;
  date: string;
  amount: number;
  status: string;
}

export interface Billing {
  currentPlan: string;
  currentPlanDetail: Plan;
  plans: Plan[];
  usage: Usage;
  invoices: Invoice[];
}

export function listUsers(): Promise<AdminUser[]> {
  return apiFetch<AdminUser[]>('/api/admin/users');
}

export function inviteUser(input: {
  email: string;
  fullName: string;
  roleId: string;
  departmentId?: string;
}): Promise<InviteUserResult> {
  return apiFetch<InviteUserResult>('/api/admin/users', { method: 'POST', body: JSON.stringify(input) });
}

export function updateAdminUser(
  id: string,
  body: Partial<{ fullName: string; roleId: string; status: string; departmentId: string }>,
): Promise<AdminUser> {
  return apiFetch<AdminUser>(`/api/admin/users/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function listRoles(): Promise<AdminRole[]> {
  return apiFetch<AdminRole[]>('/api/admin/roles');
}

export function listPermissions(): Promise<AdminPermission[]> {
  return apiFetch<AdminPermission[]>('/api/admin/permissions');
}

export function createRole(input: {
  name: string;
  description?: string;
  permissions: Record<string, boolean>;
}): Promise<AdminRole> {
  return apiFetch<AdminRole>('/api/admin/roles', { method: 'POST', body: JSON.stringify(input) });
}

export function updateRole(
  id: string,
  body: Partial<{ description: string; permissions: Record<string, boolean> }>,
): Promise<AdminRole> {
  return apiFetch<AdminRole>(`/api/admin/roles/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function deleteRole(id: string): Promise<unknown> {
  return apiFetch(`/api/admin/roles/${id}`, { method: 'DELETE' });
}

export function getOrganization(): Promise<Organization> {
  return apiFetch<Organization>('/api/admin/organization');
}

export function updateOrganization(body: Partial<{ name: string; domain: string }>): Promise<Organization> {
  return apiFetch<Organization>('/api/admin/organization', { method: 'PUT', body: JSON.stringify(body) });
}

export function listEntities(): Promise<AdminEntity[]> {
  return apiFetch<AdminEntity[]>('/api/admin/entities');
}

export function updateEntity(
  id: string,
  body: Partial<{
    name: string;
    type: string;
    state: string;
    ein: string;
    address: string;
    phone: string;
    website: string;
    status: string;
  }>,
): Promise<AdminEntity> {
  return apiFetch<AdminEntity>(`/api/admin/entities/${id}`, { method: 'PUT', body: JSON.stringify(body) });
}

export function getOnboarding(): Promise<Onboarding> {
  return apiFetch<Onboarding>('/api/admin/onboarding');
}

export function getBilling(): Promise<Billing> {
  return apiFetch<Billing>('/api/admin/billing');
}

export function changePlan(plan: string): Promise<{ plan: string }> {
  return apiFetch('/api/admin/billing/plan', { method: 'PUT', body: JSON.stringify({ plan }) });
}
