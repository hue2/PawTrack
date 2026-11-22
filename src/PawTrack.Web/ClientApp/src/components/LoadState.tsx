import type { ApiState } from '../api';

// Renders the loading and error states; returns null once data has loaded.
export default function LoadState({ state }: { state: ApiState<unknown> }) {
  if (state.status === 'loading') {
    return <p className="text-muted">Loading&hellip;</p>;
  }
  if (state.status === 'error') {
    return <div className="alert alert-danger">Couldn't load data: {state.message}</div>;
  }
  return null;
}
