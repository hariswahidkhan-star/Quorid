import { useRef, useState } from 'react';
import { uploadDocuments, type BatchUploadResult } from '../api/client';

interface Props {
  onClose: () => void;
  onComplete: (result: BatchUploadResult) => void;
}

export function UploadModal({ onClose, onComplete }: Props) {
  const [files, setFiles] = useState<File[]>([]);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [dragOver, setDragOver] = useState(false);
  const inputRef = useRef<HTMLInputElement>(null);

  function addFiles(list: FileList | null) {
    if (!list) return;
    setFiles((prev) => [...prev, ...Array.from(list)].slice(0, 20));
  }

  async function handleUpload() {
    if (files.length === 0) return;
    setBusy(true);
    setError(null);
    try {
      const result = await uploadDocuments(files);
      onComplete(result);
    } catch (e) {
      setError(e instanceof Error ? e.message : 'Upload failed');
    } finally {
      setBusy(false);
    }
  }

  return (
    <div className="modal-backdrop" onClick={onClose}>
      <div className="modal" onClick={(e) => e.stopPropagation()}>
        <div className="modal__header">
          <h2>Upload documents</h2>
          <button className="modal__close" onClick={onClose} aria-label="Close">
            ✕
          </button>
        </div>

        <div
          className={`dropzone${dragOver ? ' dropzone--over' : ''}`}
          onDragOver={(e) => {
            e.preventDefault();
            setDragOver(true);
          }}
          onDragLeave={() => setDragOver(false)}
          onDrop={(e) => {
            e.preventDefault();
            setDragOver(false);
            addFiles(e.dataTransfer.files);
          }}
          onClick={() => inputRef.current?.click()}
        >
          <div className="dropzone__icon">⬆️</div>
          <div>Drop files here or click to browse</div>
          <div className="muted">PDF, DOCX, images, XLSX · up to 20 files · 50 MB each</div>
          <input
            ref={inputRef}
            type="file"
            multiple
            style={{ display: 'none' }}
            onChange={(e) => addFiles(e.target.files)}
          />
        </div>

        {files.length > 0 && (
          <ul className="file-list">
            {files.map((f, i) => (
              <li key={`${f.name}-${i}`}>
                <span>{f.name}</span>
                <span className="muted">{(f.size / 1024).toFixed(0)} KB</span>
              </li>
            ))}
          </ul>
        )}

        {error && <div className="error-text" style={{ marginTop: 12 }}>{error}</div>}

        <div className="modal__footer">
          <button className="btn btn--ghost" onClick={onClose} disabled={busy}>
            Cancel
          </button>
          <button className="btn" onClick={handleUpload} disabled={busy || files.length === 0}>
            {busy ? 'Uploading…' : `Upload ${files.length > 0 ? files.length : ''}`.trim()}
          </button>
        </div>
      </div>
    </div>
  );
}
