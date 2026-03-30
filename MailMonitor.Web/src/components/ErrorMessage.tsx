interface ErrorMessageProps {
  message: string;
  onRetry?: () => void;
}

export function ErrorMessage({ message, onRetry }: ErrorMessageProps): JSX.Element {
  return (
    <div className="card error-box" role="alert">
      <p>{message}</p>
      {onRetry ? (
        <button type="button" className="btn secondary" onClick={onRetry}>
          Reintentar
        </button>
      ) : null}
    </div>
  );
}