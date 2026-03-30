import { useEffect } from "react";

export type ToastType = "success" | "error";

interface ToastProps {
  type: ToastType;
  message: string;
  onClose: () => void;
  durationMs?: number;
}

export function Toast({ type, message, onClose, durationMs = 4000 }: ToastProps): JSX.Element {
  useEffect(() => {
    const timer = window.setTimeout(onClose, durationMs);
    return () => window.clearTimeout(timer);
  }, [durationMs, onClose]);

  return (
    <div className={`toast ${type}`} role="status" aria-live="polite">
      <span>{message}</span>
      <button type="button" onClick={onClose} className="toast-close" aria-label="Cerrar">
        x
      </button>
    </div>
  );
}