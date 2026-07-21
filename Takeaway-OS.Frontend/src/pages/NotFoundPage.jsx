import { useNavigate } from 'react-router-dom';

// Rendered by the "*" catch-all route -> any URL no other route claims lands here.
export default function NotFoundPage() {
  const navigate = useNavigate(); // hook: must be called at the top level, never conditionally

  return (
    <div style={{ textAlign: 'center' }}>
      <h1>Page not found</h1>
      <p>That page doesn&apos;t exist or has moved.</p>
      {/* A button, not a Link -> this is an action, not a bookmarkable destination. */}
      <button type="button" onClick={() => navigate('/')}>
        Back to menu
      </button>
    </div>
  );
}
