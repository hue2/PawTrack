import { useEffect, useState } from 'react';

// Mirrors the records in Queries/Models.cs, serialized as camelCase JSON.
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface VisitRow {
  visitId: number;
  visitDate: string;
  ownerName: string;
  ownerEmail: string;
  city: string;
  petName: string;
  species: string;
  breed: string;
  vetName: string;
  clinic: string;
  reason: string;
  diagnosis: string;
  cost: number;
}

export interface OwnerSummaryRow {
  userId: number;
  ownerName: string;
  city: string;
  petCount: number;
  visitCount: number;
  totalSpend: number;
  lastVisit: string | null;
}

export type ApiState<T> =
  | { status: 'loading' }
  | { status: 'error'; message: string }
  | { status: 'success'; data: T };

export function useApi<T>(path: string, params: Record<string, string | number | undefined>): ApiState<T> {
  const query = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== '') query.set(key, String(value));
  }
  const url = `${path}?${query}`;

  const [state, setState] = useState<ApiState<T>>({ status: 'loading' });

  useEffect(() => {
    const controller = new AbortController();
    setState({ status: 'loading' });

    fetch(url, { signal: controller.signal })
      .then(async (res) => {
        if (!res.ok) throw new Error(`Request failed with status ${res.status}`);
        setState({ status: 'success', data: (await res.json()) as T });
      })
      .catch((err: Error) => {
        if (err.name !== 'AbortError') setState({ status: 'error', message: err.message });
      });

    return () => controller.abort();
  }, [url]);

  return state;
}
